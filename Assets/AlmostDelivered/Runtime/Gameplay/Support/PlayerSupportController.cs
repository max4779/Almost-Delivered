using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using AlmostDelivered.Gameplay.Payload;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Support
{
    /// <summary>Each player owns a wallet and ability cooldowns. A network server can call TryUseAbility after validating the owner.</summary>
    public sealed class PlayerSupportController : MonoBehaviour
    {
        [SerializeField] private SupportAbilityDefinition[] abilities = SupportAbilityDefinition.CreateDefaults();
        [SerializeField, Min(0)] private int mainMissionReward = 50;
        [SerializeField, Min(0)] private int sideMissionReward = 20;
        [SerializeField, Min(0)] private int evacuationReward = 10;
        private readonly Dictionary<string, float> nextReadyTimes = new Dictionary<string, float>();
        private readonly List<AutoTurret> turrets = new List<AutoTurret>();
        private readonly List<Material> shieldMaterials = new List<Material>();
        private PlayerTraitProfile profile;
        private PayloadMissionController mission;
        private SideMissionDirector sideMissions;
        private PayloadWaveSpawner waves;
        private ActorHealth health;
        private PlayerSupportWallet wallet;
        private Text hudText;
        private GameObject shieldVisual;
        private float shieldExpiresAt;

        public PlayerSupportWallet Wallet => wallet;
        public IReadOnlyList<SupportAbilityDefinition> Abilities => abilities;
        public int ActiveTurretCount
        {
            get { turrets.RemoveAll(turret => turret == null); return turrets.Count; }
        }

        private void Awake()
        {
            var catalog = Resources.Load<SupportAbilityCatalog>("SupportAbilityCatalog");
            if (catalog != null && catalog.Abilities != null)
                abilities = catalog.Abilities;
        }

        public void Configure(PlayerTraitProfile owner, PayloadMissionController payloadMission,
            SideMissionDirector objectives, PayloadWaveSpawner spawner,
            IPlayerSupportStore accountStore = null)
        {
            Unsubscribe();
            profile = owner;
            mission = payloadMission;
            sideMissions = objectives;
            waves = spawner;
            health = GetComponent<ActorHealth>();
            if (profile == null || !profile.IsLocalOwner || string.IsNullOrWhiteSpace(profile.AccountId)) return;
            wallet = new PlayerSupportWallet(profile.AccountId, accountStore ?? new LocalPlayerSupportStore());
            if (mission != null) mission.PhaseChanged += OnMissionPhaseChanged;
            if (sideMissions != null) sideMissions.ObjectiveCompleted += OnSideMissionCompleted;
            BuildHud();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (health != null && health.CurrentShield > 0f) health.SetShield(0f);
            foreach (var material in shieldMaterials)
                if (material != null) Destroy(material);
        }

        private void Unsubscribe()
        {
            if (mission != null) mission.PhaseChanged -= OnMissionPhaseChanged;
            if (sideMissions != null) sideMissions.ObjectiveCompleted -= OnSideMissionCompleted;
        }

        private void Update()
        {
            if (wallet == null) return;
            if (health != null && health.CurrentShield > 0f && Time.time >= shieldExpiresAt)
                health.SetShield(0f);
            if (shieldVisual != null) shieldVisual.SetActive(health != null && health.CurrentShield > 0f);
            if (shieldVisual != null && shieldVisual.activeSelf)
                shieldVisual.transform.Rotate(0f, Time.deltaTime * 40f, 0f, Space.Self);
            if (abilities != null)
                foreach (var ability in abilities)
                    if (ability != null && Input.GetKeyDown(ability.key)) TryUseAbility(ability.id);
            RefreshHud();
        }

        public bool TryUseAbility(string id)
        {
            var ability = FindAbility(id);
            if (wallet == null || profile == null || !profile.IsLocalOwner || ability == null ||
                health == null || health.IsDead || mission == null ||
                mission.Snapshot.Phase == PayloadMissionPhase.Completed ||
                mission.Snapshot.Phase == PayloadMissionPhase.Failed ||
                mission.Snapshot.Phase == PayloadMissionPhase.Escaped ||
                mission.Snapshot.IsRelayChoicePending ||
                nextReadyTimes.TryGetValue(id, out var readyAt) && Time.time < readyAt)
                return false;

            if (ability.effect == SupportEffectKind.AutoTurret && ActiveTurretCount >= 2 ||
                ability.effect == SupportEffectKind.PersonalShield && health.CurrentShield > 0f ||
                wallet.Credits < ability.cost) return false;
            if (!wallet.TrySpend(ability.cost)) return false;
            nextReadyTimes[id] = Time.time + Mathf.Max(0f, ability.cooldown);
            switch (ability.effect)
            {
                case SupportEffectKind.AutoTurret:
                    SpawnTurret(ability);
                    break;
                case SupportEffectKind.PersonalShield:
                    health.SetShield(Mathf.Max(.1f, ability.power));
                    shieldExpiresAt = Time.time + Mathf.Max(.1f, ability.duration);
                    EnsureShieldVisual();
                    shieldVisual.SetActive(true);
                    break;
            }
            RefreshHud();
            return true;
        }

        public void SetAbilities(SupportAbilityDefinition[] definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions)
                if (definition == null || string.IsNullOrWhiteSpace(definition.id) ||
                    definition.cost <= 0 || !Enum.IsDefined(typeof(SupportEffectKind), definition.effect) ||
                    !ids.Add(definition.id))
                    throw new ArgumentException("Ability IDs must be unique and costs positive.", nameof(definitions));
            abilities = (SupportAbilityDefinition[])definitions.Clone();
            RefreshHud();
        }

        private SupportAbilityDefinition FindAbility(string id)
        {
            if (abilities == null) return null;
            foreach (var ability in abilities)
                if (ability != null && string.Equals(ability.id, id, StringComparison.Ordinal)) return ability;
            return null;
        }

        private void SpawnTurret(SupportAbilityDefinition ability)
        {
            var position = transform.position + transform.forward * 2.4f;
            if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out var hit, 10f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y;
            var turretObject = new GameObject("Auto Turret");
            turretObject.transform.position = position;
            var turret = turretObject.AddComponent<AutoTurret>();
            turret.Configure(profile.AccountId, waves, Mathf.Max(.1f, ability.duration),
                Mathf.Max(.1f, ability.power));
            turrets.Add(turret);
        }

        private void EnsureShieldVisual()
        {
            if (shieldVisual != null) return;
            shieldVisual = new GameObject("Personal Shield");
            shieldVisual.transform.SetParent(transform, false);
            for (var ring = 0; ring < 3; ring++)
            {
                var ringObject = new GameObject("Shield Ring " + ring, typeof(LineRenderer));
                ringObject.transform.SetParent(shieldVisual.transform, false);
                var line = ringObject.GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.loop = true;
                line.positionCount = 32;
                line.widthMultiplier = .045f;
                var material = new Material(Shader.Find("Sprites/Default"));
                line.material = material;
                shieldMaterials.Add(material);
                line.startColor = line.endColor = new Color(.15f, .8f, 1f, .85f);
                var height = .45f + ring * .5f;
                var radius = ring == 1 ? 1.05f : .85f;
                for (var index = 0; index < line.positionCount; index++)
                {
                    var angle = index * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, height,
                        Mathf.Sin(angle) * radius));
                }
            }
        }

        private void OnMissionPhaseChanged(PayloadMissionPhase previous, PayloadMissionPhase current)
        {
            if (current == PayloadMissionPhase.Completed) wallet?.Grant(mainMissionReward);
            else if (current == PayloadMissionPhase.Escaped) wallet?.Grant(evacuationReward);
        }

        private void OnSideMissionCompleted(int index, SideMissionKind kind)
        {
            wallet?.Grant(sideMissionReward);
        }

        private void BuildHud()
        {
            if (hudText != null) return;
            var canvas = FindObjectOfType<MissionHud>()?.GetComponent<Canvas>();
            if (canvas == null) return;
            var panel = new GameObject("Support Equipment HUD", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            var rect = panel.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 18f);
            rect.sizeDelta = new Vector2(300f, 94f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(.025f, .07f, .09f, .87f);
            background.raycastTarget = false;
            var label = new GameObject("Support Equipment Label", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(9f, 7f);
            labelRect.offsetMax = new Vector2(-8f, -6f);
            hudText = label.GetComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hudText.fontSize = 15;
            hudText.color = new Color(.85f, .98f, 1f);
            hudText.alignment = TextAnchor.UpperLeft;
            hudText.raycastTarget = false;
            RefreshHud();
        }

        private void RefreshHud()
        {
            if (hudText == null || wallet == null) return;
            var content = "지원 장비  ·  " + wallet.Credits + " 크레딧";
            if (abilities != null)
                foreach (var ability in abilities)
                {
                    if (ability == null) continue;
                    var ready = nextReadyTimes.TryGetValue(ability.id, out var time)
                        ? Mathf.Max(0f, time - Time.time) : 0f;
                    var status = ready > 0f ? Mathf.CeilToInt(ready) + "초" :
                        wallet.Credits < ability.cost ? "크레딧 부족" : "준비";
                    content += "\n" + ability.key.ToString().Replace("Alpha", "") + " " +
                        ability.displayName + "  " + ability.cost + "C  ·  " + status;
                }
            hudText.text = content;
        }
    }
}
