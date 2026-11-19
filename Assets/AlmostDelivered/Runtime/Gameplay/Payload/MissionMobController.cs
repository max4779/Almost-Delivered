using System;
using System.Collections.Generic;
using AlmostDelivered.Combat;
using AlmostDelivered.Presentation;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    public enum MissionMobAwarenessState { Unaware, Pursuing, Vigilant }

    public enum MissionMobArchetype
    {
        Melee,
        Ranged,
        Shield,
        CommanderBoss,
        ChargerBoss,
        CargoSaboteur
    }

    public readonly struct MissionMobDefinition
    {
        public MissionMobDefinition(
            MissionMobArchetype archetype,
            float maxHealth,
            float moveSpeed,
            float contactRange,
            float contactDamage,
            float contactCooldown,
            float standOffRange)
        {
            Archetype = archetype;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            ContactRange = contactRange;
            ContactDamage = contactDamage;
            ContactCooldown = contactCooldown;
            StandOffRange = standOffRange;
        }

        public MissionMobArchetype Archetype { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float ContactRange { get; }
        public float ContactDamage { get; }
        public float ContactCooldown { get; }
        public float StandOffRange { get; }

        public static MissionMobDefinition For(MissionMobArchetype archetype)
        {
            switch (archetype)
            {
                case MissionMobArchetype.Ranged:
                    return new MissionMobDefinition(archetype, 45f, 3f, 1.2f, 8f, 1.2f, 9f);
                case MissionMobArchetype.Shield:
                    return new MissionMobDefinition(archetype, 90f, 2.2f, 1.6f, 5f, 1.2f, 0f);
                case MissionMobArchetype.CommanderBoss:
                    return new MissionMobDefinition(archetype, 220f, 2.5f, 1.5f, 12f, 1f, 7f);
                case MissionMobArchetype.ChargerBoss:
                    return new MissionMobDefinition(archetype, 300f, 4.5f, 1.7f, 16f, 1.1f, 0f);
                case MissionMobArchetype.CargoSaboteur:
                    return new MissionMobDefinition(archetype, 75f, 4.5f, 2.3f, 22f, 1.4f, 0f);
                default:
                    return new MissionMobDefinition(MissionMobArchetype.Melee, 60f, 4.8f, 1.4f, 10f, 1f, 0f);
            }
        }
    }

    [RequireComponent(typeof(ActorHealth), typeof(CombatTeam))]
    public sealed class MissionMobController : MonoBehaviour
    {
        private const float ShieldEscortDistance = 2.2f;
        private const float ShieldEscortSearchRadius = 14f;
        private const float ShieldPushDistance = 1f;
        private const float RangedRetreatStart = 4f;
        private const float RangedRetreatStop = 7f;
        private const float RangedCoverDistance = 2.8f;
        private const float RangedEngagementRange = 20f;
        private static readonly HashSet<MissionMobController> ActiveMobs = new HashSet<MissionMobController>();

        [SerializeField] private Transform target;
        [SerializeField] private Transform cargoTarget;
        [SerializeField] private RangedAttackController rangedAttack;
        [SerializeField, Min(1f)] private float turnSpeed = 360f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVerticalSpeed = -2f;
        [SerializeField, Min(.1f)] private float deathDuration = 1f;
        [SerializeField] private float deathFallAngle = -82f;
        [SerializeField, Min(0f)] private float deathSinkDepth = 1.35f;
        [SerializeField, Min(0f)] private float alertRadius = 7f;
        [SerializeField, Min(.1f)] private float alertIndicatorDuration = 1.25f;
        [SerializeField, Range(1f, 180f)] private float sightFieldOfView = 120f;
        [SerializeField, Min(0f)] private float closeAwarenessRadius = 3.25f;
        [SerializeField, Min(0f)] private float freeAwarenessRadius = 18f;
        [SerializeField, Min(0f)] private float pursuitSightRadius = 28f;
        [SerializeField, Min(0f)] private float vigilanceDuration = 22f;
        [SerializeField, Min(0f)] private float soundMemoryDuration = 3f;

        private ActorHealth health;
        private CharacterController characterController;
        private float nextContactTime;
        private float verticalVelocity;
        private bool removalReported;
        private bool deathAnimating;
        private float deathElapsed;
        private Transform deathVisual;
        private Vector3 deathStartPosition;
        private Quaternion deathStartRotation;
        private bool guardPostEnabled;
        private bool guardPostEngaged;
        private Vector3 guardPost;
        private float guardAwarenessRadius;
        private float lastKnownHealth;
        private float alertTimeRemaining;
        private float vigilanceRemaining;
        private Vector3 lastKnownPlayerPosition;
        private bool hasLastKnownPlayerPosition;
        private bool hasSeenPlayer;
        private bool hasShownAlert;
        private bool pursuingFromNoise;
        private MissionMobAwarenessState awarenessState;
        private bool suppressDamageAlert;
        private GameObject alertIndicator;
        private bool assaultingCargo;
        private readonly RaycastHit[] navigationHits = new RaycastHit[24];
        private Collider detourObstacle;
        private Vector3 detourNear;
        private Vector3 detourFar;
        private bool headingToFar;
        private MissionMobController escortedRanged;
        private float nextEscortSearchAt;
        private MissionMobController protectingShield;
        private float nextShieldSearchAt;
        private bool rangedRetreating;

        public MissionMobDefinition Definition { get; private set; }
        public int WaveId { get; private set; } = -1;
        public int EnemyId { get; private set; } = -1;
        public bool IsDefeated => health != null && health.IsDead;
        public bool HasGuardPost => guardPostEnabled;
        public bool IsGuardPostEngaged => guardPostEngaged;
        public bool IsAlerted => alertTimeRemaining > 0f && alertIndicator != null && alertIndicator.activeSelf;
        public bool IsCargoSaboteur => Definition.Archetype == MissionMobArchetype.CargoSaboteur;
        public bool IsPursuingPlayer => !IsCargoSaboteur && awarenessState != MissionMobAwarenessState.Unaware;
        public MissionMobAwarenessState AwarenessState => awarenessState;
        public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
        public float VigilanceRemaining => vigilanceRemaining;
        public GameObject AlertIndicator => alertIndicator;

        public event Action<MissionMobController> Removed;
        public event Action<MissionMobController, Vector3> VisualContactObserved;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            characterController = GetComponent<CharacterController>();
            lastKnownHealth = health.CurrentHealth;
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDeath;
        }

        private void OnEnable()
        {
            ActiveMobs.Add(this);
            CombatNoiseSystem.NoiseEmitted += HandleNoise;
        }

        private void OnDisable()
        {
            ActiveMobs.Remove(this);
            CombatNoiseSystem.NoiseEmitted -= HandleNoise;
            if (health != null && !health.IsDead)
            {
                NotifyRemoved();
            }
        }

        private void OnDestroy()
        {
            ActiveMobs.Remove(this);
            CombatNoiseSystem.NoiseEmitted -= HandleNoise;
            if (health != null)
            {
                health.HealthChanged -= HandleHealthChanged;
                health.Died -= HandleDeath;
            }

            NotifyRemoved();
        }

        private void Update()
        {
            Tick(Time.time, Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (alertIndicator == null || !alertIndicator.activeSelf || Camera.main == null)
            {
                return;
            }

            var direction = alertIndicator.transform.position - Camera.main.transform.position;
            if (direction.sqrMagnitude > .0001f)
            {
                alertIndicator.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        public void Configure(MissionMobDefinition definition, Transform combatTarget, RangedAttackController attack)
        {
            Definition = definition;
            target = combatTarget;
            rangedAttack = attack;
            nextContactTime = 0f;
            verticalVelocity = 0f;
            removalReported = false;
            deathAnimating = false;
            deathElapsed = 0f;
            deathVisual = null;
            guardPostEnabled = false;
            guardPostEngaged = false;
            assaultingCargo = false;
            cargoTarget = null;
            alertTimeRemaining = 0f;
            vigilanceRemaining = soundMemoryDuration;
            awarenessState = combatTarget != null ? MissionMobAwarenessState.Pursuing : MissionMobAwarenessState.Unaware;
            lastKnownPlayerPosition = combatTarget != null ? combatTarget.position : transform.position;
            hasLastKnownPlayerPosition = combatTarget != null;
            hasSeenPlayer = combatTarget != null;
            hasShownAlert = false;
            pursuingFromNoise = false;
            if (alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }

            suppressDamageAlert = true;
            health.ConfigureMaximum(definition.MaxHealth);
            lastKnownHealth = health.CurrentHealth;
            suppressDamageAlert = false;
            ClearDetour();
            escortedRanged = null;
            nextEscortSearchAt = float.NegativeInfinity;
            protectingShield = null;
            nextShieldSearchAt = float.NegativeInfinity;
            rangedRetreating = false;
        }

        public void ConfigureGuardPost(Vector3 position, float awarenessRadius)
        {
            guardPost = position;
            guardAwarenessRadius = Mathf.Max(0f, awarenessRadius);
            guardPostEnabled = true;
            guardPostEngaged = false;
            awarenessState = MissionMobAwarenessState.Unaware;
            hasLastKnownPlayerPosition = false;
            hasSeenPlayer = false;
            hasShownAlert = false;
            pursuingFromNoise = false;
        }

        public void ConfigureCargoAssault(Transform cargo)
        {
            cargoTarget = cargo;
            assaultingCargo = cargo != null;
            guardAwarenessRadius = 13f;
            awarenessState = MissionMobAwarenessState.Unaware;
            hasLastKnownPlayerPosition = false;
            hasSeenPlayer = false;
            hasShownAlert = false;
            pursuingFromNoise = false;
        }

        public void ReleaseGuardPostToCargo(Transform cargo)
        {
            guardPostEnabled = false;
            guardPostEngaged = false;
            ConfigureCargoAssault(cargo);
        }

        public void InvestigateCommanderReport(Vector3 observedPosition)
        {
            if (IsDefeated || IsCargoSaboteur) return;
            ReceiveAlert(false, observedPosition);
            awarenessState = MissionMobAwarenessState.Vigilant;
            vigilanceRemaining = vigilanceDuration;
            hasShownAlert = true;
        }

        public void AssignIdentity(int waveId, int enemyId)
        {
            WaveId = waveId;
            EnemyId = enemyId;
        }

        public void Tick(float now, float deltaTime)
        {
            if (!enabled || !IsFinite(now) || !IsFinite(deltaTime) || deltaTime < 0f)
            {
                return;
            }

            TickAlertIndicator(deltaTime);

            if (deathAnimating)
            {
                TickDeath(deltaTime);
                return;
            }

            if (IsDefeated || (target == null && !IsCargoSaboteur))
            {
                return;
            }

            var targetHealth = target != null ? target.GetComponentInParent<ActorHealth>() : null;
            if (!IsCargoSaboteur && (targetHealth == null || targetHealth.IsDead))
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var toPlayer = target != null
                ? Vector3.ProjectOnPlane(target.position - transform.position, Vector3.up)
                : Vector3.zero;
            if (!IsCargoSaboteur)
            {
                if (pursuingFromNoise)
                {
                    awarenessState = MissionMobAwarenessState.Pursuing;
                    lastKnownPlayerPosition = target.position;
                    hasLastKnownPlayerPosition = true;
                }
                else if (awarenessState == MissionMobAwarenessState.Unaware && CanDetectTarget(toPlayer))
                {
                    var firstVisual = !hasSeenPlayer;
                    ReceiveAlert(!assaultingCargo, target.position, true);
                    if (firstVisual) VisualContactObserved?.Invoke(this, target.position);
                }
                else if (awarenessState != MissionMobAwarenessState.Unaware &&
                         (hasSeenPlayer ? CanTrackTarget(toPlayer) : CanDetectTarget(toPlayer)))
                {
                    var firstVisual = !hasSeenPlayer;
                    awarenessState = MissionMobAwarenessState.Pursuing;
                    hasSeenPlayer = true;
                    vigilanceRemaining = hasShownAlert ? vigilanceDuration : soundMemoryDuration;
                    lastKnownPlayerPosition = target.position;
                    hasLastKnownPlayerPosition = true;
                    if (firstVisual) VisualContactObserved?.Invoke(this, target.position);
                }
                else if (awarenessState == MissionMobAwarenessState.Pursuing)
                {
                    awarenessState = MissionMobAwarenessState.Vigilant;
                    vigilanceRemaining = hasShownAlert ? vigilanceDuration : soundMemoryDuration;
                    alertTimeRemaining = 0f;
                    if (alertIndicator != null) alertIndicator.SetActive(false);
                    ClearDetour();
                }
                else if (awarenessState == MissionMobAwarenessState.Vigilant)
                {
                    vigilanceRemaining = Mathf.Max(0f, vigilanceRemaining - deltaTime);
                    if (vigilanceRemaining <= 0f)
                    {
                        awarenessState = MissionMobAwarenessState.Unaware;
                        guardPostEngaged = false;
                        hasLastKnownPlayerPosition = false;
                        hasSeenPlayer = false;
                        hasShownAlert = false;
                        ClearDetour();
                    }
                }
            }

            if (Definition.Archetype == MissionMobArchetype.Ranged && assaultingCargo &&
                cargoTarget != null && toPlayer.sqrMagnitude >
                RangedEngagementRange * RangedEngagementRange)
            {
                awarenessState = MissionMobAwarenessState.Unaware;
                pursuingFromNoise = false;
                hasLastKnownPlayerPosition = false;
                hasSeenPlayer = false;
                ClearDetour();
            }

            if (IsCargoSaboteur && cargoTarget == null)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            if (awarenessState == MissionMobAwarenessState.Vigilant && hasLastKnownPlayerPosition)
            {
                MoveToKnownPosition(lastKnownPlayerPosition, deltaTime);
                return;
            }

            var pursuingPlayer = IsPursuingPlayer;
            var destination = pursuingPlayer ? target : cargoTarget;
            if (guardPostEnabled && !guardPostEngaged)
            {
                MoveToKnownPosition(guardPost, deltaTime);
                return;
            }

            if (TryEscortRanged(now, deltaTime, pursuingPlayer))
                return;

            if (destination == null)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var toTarget = Vector3.ProjectOnPlane(destination.position - transform.position, Vector3.up);
            if (toTarget.sqrMagnitude <= .0001f)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var distance = toTarget.magnitude;
            var direction = toTarget / distance;
            if (Definition.Archetype == MissionMobArchetype.Ranged && pursuingPlayer)
            {
                if (distance < RangedRetreatStart) rangedRetreating = true;
                if (distance >= RangedRetreatStop) rangedRetreating = false;
                if (rangedRetreating && TryRetreat(direction, out var retreatDirection))
                {
                    ClearDetour();
                    RotateTowards(direction, deltaTime);
                    TryFireAtPlayer(direction, distance, now);
                    Move(retreatDirection, deltaTime);
                    return;
                }
                var shield = FindProtectingShield(now);
                if (shield != null)
                {
                    var shieldToPlayer = Vector3.ProjectOnPlane(
                        target.position - shield.transform.position, Vector3.up);
                    if (shieldToPlayer.sqrMagnitude > .01f)
                    {
                        var coverPosition = shield.transform.position -
                                            shieldToPlayer.normalized * RangedCoverDistance;
                        if (Vector3.Distance(transform.position, coverPosition) > .8f)
                            MoveToKnownPosition(coverPosition, deltaTime);
                        else
                            Move(Vector3.zero, deltaTime);
                        RotateTowards(direction, deltaTime);
                        TryFireAtPlayer(direction, distance, now);
                        return;
                    }
                }
                if (distance <= RangedEngagementRange)
                {
                    ClearDetour();
                    RotateTowards(direction, deltaTime);
                    TryFireAtPlayer(direction, distance, now);
                    Move(Vector3.zero, deltaTime);
                    return;
                }
                if (cargoTarget != null)
                    MoveToKnownPosition(cargoTarget.position, deltaTime);
                else
                    Move(Vector3.zero, deltaTime);
                return;
            }
            Collider obstacle = null;
            var blocked = characterController != null && FindBlockingObstacle(direction, distance, out obstacle);
            if (blocked && detourObstacle != obstacle && detourObstacle == null)
                PlanDetour(obstacle, direction);
            var navigationDirection = NavigationDirection(direction);
            RotateTowards(navigationDirection, deltaTime);

            if (IsRanged(Definition.Archetype) && pursuingPlayer)
            {
                if (distance > Definition.StandOffRange || blocked || detourObstacle != null)
                {
                    Move(navigationDirection, deltaTime);
                }
                else if (pursuingPlayer && rangedAttack != null && !blocked && detourObstacle == null)
                {
                    if (rangedAttack.TryFire(target.position, now))
                        GetComponentInChildren<ProceduralPoseAnimator>()?.PlayAttack();
                }

                if (distance <= Definition.StandOffRange && !blocked && detourObstacle == null)
                {
                    Move(Vector3.zero, deltaTime);
                }

                return;
            }

            if (Definition.Archetype == MissionMobArchetype.Ranged && !pursuingPlayer &&
                cargoTarget != null)
            {
                if (distance > Definition.StandOffRange || blocked || detourObstacle != null)
                    Move(navigationDirection, deltaTime);
                else
                {
                    Move(Vector3.zero, deltaTime);
                    var cargo = cargoTarget.GetComponent<CargoIntegrity>();
                    if (cargo != null && rangedAttack != null &&
                        rangedAttack.TryFire(cargoTarget.position, now))
                    {
                        cargo.ApplyDamage(Definition.ContactDamage);
                        GetComponentInChildren<ProceduralPoseAnimator>()?.PlayGunshot(false);
                    }
                }
                return;
            }

            var contactRange = pursuingPlayer ? Definition.ContactRange : 2.3f;
            if (distance > contactRange || (pursuingPlayer && blocked) || detourObstacle != null)
            {
                Move(navigationDirection, deltaTime);
                return;
            }

            if (pursuingPlayer && now >= nextContactTime)
            {
                var canPush = Definition.Archetype == MissionMobArchetype.Shield &&
                              !targetHealth.IsInvulnerable;
                targetHealth.ApplyDamage(Definition.ContactDamage);
                if (canPush)
                    target.GetComponent<MissionPlayerController>()?.TryApplyKnockback(
                        transform.position, ShieldPushDistance);
                GetComponentInChildren<ProceduralPoseAnimator>()?.PlayMeleeAttack();
                nextContactTime = now + Definition.ContactCooldown;
            }
            else if (!pursuingPlayer && now >= nextContactTime)
            {
                var cargo = cargoTarget.GetComponent<CargoIntegrity>();
                if (cargo != null)
                    cargo.ApplyDamage(Definition.ContactDamage);
                GetComponentInChildren<ProceduralPoseAnimator>()?.PlayMeleeAttack();
                nextContactTime = now + Definition.ContactCooldown;
            }

            Move(Vector3.zero, deltaTime);
        }

        private bool TryEscortRanged(float now, float deltaTime, bool pursuingPlayer)
        {
            if (Definition.Archetype != MissionMobArchetype.Shield || EnemyId == -1 ||
                (pursuingPlayer && Vector3.Distance(transform.position, target.position) <=
                    Definition.ContactRange + .2f) ||
                (!pursuingPlayer && cargoTarget != null &&
                    Vector3.Distance(transform.position, cargoTarget.position) <= 2.3f))
                return false;

            if (escortedRanged == null || escortedRanged.IsDefeated ||
                escortedRanged.WaveId != WaveId ||
                Vector3.Distance(transform.position, escortedRanged.transform.position) >
                    ShieldEscortSearchRadius)
                escortedRanged = null;
            if (escortedRanged == null && now >= nextEscortSearchAt)
            {
                nextEscortSearchAt = now + .5f;
                var closestDistance = ShieldEscortSearchRadius;
                foreach (var candidate in ActiveMobs)
                {
                    if (candidate == null || candidate == this || candidate.IsDefeated ||
                        candidate.WaveId != WaveId ||
                        candidate.Definition.Archetype != MissionMobArchetype.Ranged)
                        continue;
                    var distance = Vector3.Distance(transform.position, candidate.transform.position);
                    if (distance >= closestDistance) continue;
                    closestDistance = distance;
                    escortedRanged = candidate;
                }
            }
            if (escortedRanged == null) return false;

            var threat = pursuingPlayer && target != null
                ? target.position
                : cargoTarget != null ? cargoTarget.position : target.position;
            var offset = Vector3.ProjectOnPlane(threat - escortedRanged.transform.position,
                Vector3.up);
            if (offset.sqrMagnitude < .01f) return false;
            var destination = escortedRanged.transform.position +
                              offset.normalized * ShieldEscortDistance;
            if (Vector3.Distance(transform.position, destination) <= .6f)
                RotateTowards(offset.normalized, deltaTime);
            MoveToKnownPosition(destination, deltaTime);
            return true;
        }

        private MissionMobController FindProtectingShield(float now)
        {
            if (EnemyId == -1) return null;
            if (protectingShield == null || protectingShield.IsDefeated ||
                protectingShield.WaveId != WaveId ||
                Vector3.Distance(transform.position, protectingShield.transform.position) >
                    ShieldEscortSearchRadius)
                protectingShield = null;
            if (protectingShield != null || now < nextShieldSearchAt)
                return protectingShield;
            nextShieldSearchAt = now + .5f;
            var closestDistance = ShieldEscortSearchRadius;
            foreach (var candidate in ActiveMobs)
            {
                if (candidate == null || candidate == this || candidate.IsDefeated ||
                    candidate.WaveId != WaveId ||
                    candidate.Definition.Archetype != MissionMobArchetype.Shield)
                    continue;
                var distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                protectingShield = candidate;
            }
            return protectingShield;
        }

        private void TryFireAtPlayer(Vector3 direction, float distance, float now)
        {
            if (rangedAttack == null || distance > RangedEngagementRange ||
                FindBlockingObstacle(direction, distance, out _)) return;
            if (rangedAttack.TryFire(target.position, now))
                GetComponentInChildren<ProceduralPoseAnimator>()?.PlayGunshot(false);
        }

        private bool TryRetreat(Vector3 towardPlayer, out Vector3 retreatDirection)
        {
            var away = -towardPlayer;
            var sideways = new Vector3(away.z, 0f, -away.x);
            var preference = EnemyId % 2 == 0 ? 1f : -1f;
            for (var index = 0; index < 5; index++)
            {
                var candidate = index == 0 ? away :
                    index == 1 ? (away + sideways * preference).normalized :
                    index == 2 ? (away - sideways * preference).normalized :
                    index == 3 ? sideways * preference : -sideways * preference;
                if (characterController != null &&
                    FindBlockingObstacle(candidate, 2f, out _)) continue;
                retreatDirection = candidate;
                return true;
            }
            retreatDirection = Vector3.zero;
            return false;
        }

        private bool FindBlockingObstacle(Vector3 direction, float distance, out Collider obstacle)
        {
            obstacle = null;
            var origin = transform.position + Vector3.up;
            var count = Physics.SphereCastNonAlloc(origin, .38f, direction, navigationHits,
                Mathf.Max(0f, distance - .5f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearest = float.PositiveInfinity;
            for (var index = 0; index < count; index++)
            {
                var hit = navigationHits[index];
                var candidate = hit.collider;
                if (candidate == null || candidate.transform.IsChildOf(transform) ||
                    candidate.transform == target || candidate.transform.IsChildOf(target) ||
                    (!IsPursuingPlayer && cargoTarget != null &&
                     (candidate.transform == cargoTarget || candidate.transform.IsChildOf(cargoTarget))) ||
                    candidate.GetComponentInParent<ActorHealth>() != null || hit.distance >= nearest)
                    continue;

                nearest = hit.distance;
                obstacle = candidate;
            }

            return obstacle != null;
        }

        private void MoveToKnownPosition(Vector3 position, float deltaTime)
        {
            var offset = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (offset.sqrMagnitude <= .36f)
            {
                Move(Vector3.zero, deltaTime);
                return;
            }

            var direction = offset.normalized;
            if (characterController != null && detourObstacle == null &&
                FindBlockingObstacle(direction, offset.magnitude, out var obstacle))
                PlanDetour(obstacle, direction);
            var navigationDirection = NavigationDirection(direction);
            RotateTowards(navigationDirection, deltaTime);
            Move(navigationDirection, deltaTime);
        }

        private void PlanDetour(Collider obstacle, Vector3 targetDirection)
        {
            var bounds = obstacle.bounds;
            var center = Vector3.ProjectOnPlane(bounds.center, Vector3.up);
            var lateral = new Vector3(targetDirection.z, 0f, -targetDirection.x);
            var side = Vector3.Dot(transform.position - center, lateral) >= 0f ? 1f : -1f;
            var extent = bounds.extents;
            var forwardRadius = Mathf.Abs(targetDirection.x) * extent.x + Mathf.Abs(targetDirection.z) * extent.z + .9f;
            var sideRadius = Mathf.Abs(lateral.x) * extent.x + Mathf.Abs(lateral.z) * extent.z + 1f;
            detourNear = center - targetDirection * forwardRadius + lateral * side * sideRadius;
            detourFar = center + targetDirection * forwardRadius + lateral * side * sideRadius;
            detourObstacle = obstacle;
            headingToFar = false;
        }

        private Vector3 NavigationDirection(Vector3 direct)
        {
            if (detourObstacle == null)
                return direct;

            var here = Vector3.ProjectOnPlane(transform.position, Vector3.up);
            if (!headingToFar && Vector3.Distance(here, detourNear) < .65f)
                headingToFar = true;
            if (headingToFar && Vector3.Distance(here, detourFar) < .7f)
            {
                ClearDetour();
                return direct;
            }

            var waypoint = headingToFar ? detourFar : detourNear;
            return (waypoint - here).normalized;
        }

        private void ClearDetour()
        {
            detourObstacle = null;
            headingToFar = false;
        }

        private void Move(Vector3 direction, float deltaTime)
        {
            var horizontalVelocity = direction * Definition.MoveSpeed;
            if (characterController != null && characterController.enabled)
            {
                if (characterController.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = groundedVerticalSpeed;
                else
                    verticalVelocity += gravity * deltaTime;

                var movement = (horizontalVelocity + Vector3.up * verticalVelocity) * deltaTime;
                var collisionFlags = characterController.Move(movement);
                if ((collisionFlags & CollisionFlags.Below) != 0)
                    verticalVelocity = groundedVerticalSpeed;
            }
            else
            {
                transform.position += horizontalVelocity * deltaTime;
            }
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude <= .0001f || deltaTime <= 0f)
                return;

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * deltaTime);
        }

        private void HandleDeath()
        {
            alertTimeRemaining = 0f;
            if (alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }

            if (rangedAttack != null)
            {
                rangedAttack.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            foreach (var actorCollider in GetComponentsInChildren<Collider>())
            {
                actorCollider.enabled = false;
            }

            deathVisual = transform.Find("VisualRoot");
            if (deathVisual != null)
            {
                deathStartPosition = deathVisual.localPosition;
                deathStartRotation = deathVisual.localRotation;
            }

            deathElapsed = 0f;
            deathAnimating = true;
            NotifyRemoved();
        }

        private void HandleHealthChanged(float current, float maximum)
        {
            var tookDamage = !suppressDamageAlert && current + .0001f < lastKnownHealth;
            lastKnownHealth = current;
            if (tookDamage)
            {
                if (assaultingCargo && !IsCargoSaboteur)
                    ReceiveAlert(false, transform.position);
                AlertNearbyMobs();
            }
        }

        private void HandleNoise(CombatNoise noise)
        {
            if (!isActiveAndEnabled || IsDefeated || target == null || IsCargoSaboteur ||
                noise.SourceTeam != CombatTeamId.Player)
            {
                return;
            }

            var offset = Vector3.ProjectOnPlane(transform.position - noise.Position, Vector3.up);
            if (offset.sqrMagnitude <= noise.Radius * noise.Radius)
            {
                if (!hasShownAlert)
                {
                    ReceiveAlert(false, noise.Position);
                    pursuingFromNoise = true;
                    awarenessState = MissionMobAwarenessState.Pursuing;
                }
                else
                {
                    ReceiveAlert(false, noise.Position);
                }
            }
        }

        private bool CanDetectTarget(Vector3 toTarget)
        {
            var distanceSquared = toTarget.sqrMagnitude;
            if (distanceSquared <= .0001f)
                return true;

            var awarenessRadius = guardPostEnabled || assaultingCargo
                ? guardAwarenessRadius : freeAwarenessRadius;
            if (distanceSquared > awarenessRadius * awarenessRadius)
            {
                return false;
            }

            var direction = toTarget.normalized;
            var close = distanceSquared <= closeAwarenessRadius * closeAwarenessRadius;
            var minimumDot = Mathf.Cos(sightFieldOfView * .5f * Mathf.Deg2Rad);
            if (!close && Vector3.Dot(transform.forward, direction) < minimumDot)
            {
                return false;
            }

            return HasLineOfSight(direction, Mathf.Sqrt(distanceSquared));
        }

        private bool CanTrackTarget(Vector3 toTarget)
        {
            var distanceSquared = toTarget.sqrMagnitude;
            return distanceSquared <= pursuitSightRadius * pursuitSightRadius &&
                (distanceSquared <= .0001f || HasLineOfSight(toTarget.normalized, Mathf.Sqrt(distanceSquared)));
        }

        private bool HasLineOfSight(Vector3 planarDirection, float distance)
        {
            var eye = transform.position + Vector3.up;
            if (!Physics.Raycast(eye, planarDirection, out var hit, distance, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.transform == target || hit.transform.IsChildOf(target);
        }

        private void AlertNearbyMobs()
        {
            var center = transform.position;
            var radiusSquared = alertRadius * alertRadius;
            var snapshot = new List<MissionMobController>(ActiveMobs);
            foreach (var candidate in snapshot)
            {
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.IsDefeated)
                {
                    continue;
                }

                var offset = Vector3.ProjectOnPlane(candidate.transform.position - center, Vector3.up);
                if (offset.sqrMagnitude <= radiusSquared)
                {
                    candidate.ReceiveAlert(!candidate.assaultingCargo, center);
                }
            }
        }

        private void ReceiveAlert(bool showIndicator = true, Vector3? knownPosition = null,
            bool visualContact = false)
        {
            if (IsCargoSaboteur)
                return;
            var firstDetection = awarenessState == MissionMobAwarenessState.Unaware;
            guardPostEngaged = true;
            ClearDetour();
            awarenessState = visualContact ? MissionMobAwarenessState.Pursuing : MissionMobAwarenessState.Vigilant;
            if (visualContact) hasSeenPlayer = true;
            vigilanceRemaining = hasShownAlert ? vigilanceDuration : soundMemoryDuration;
            if (knownPosition.HasValue)
            {
                lastKnownPlayerPosition = knownPosition.Value;
                hasLastKnownPlayerPosition = true;
            }
            if (assaultingCargo)
                showIndicator = false;
            if (!showIndicator || !firstDetection)
            {
                return;
            }

            hasShownAlert = true;
            vigilanceRemaining = vigilanceDuration;
            alertTimeRemaining = Mathf.Max(alertTimeRemaining, alertIndicatorDuration);
            EnsureAlertIndicator();
            alertIndicator.SetActive(true);
        }

        private void TickAlertIndicator(float deltaTime)
        {
            if (alertTimeRemaining <= 0f)
            {
                return;
            }

            alertTimeRemaining = Mathf.Max(0f, alertTimeRemaining - deltaTime);
            if (alertTimeRemaining <= 0f && alertIndicator != null)
            {
                alertIndicator.SetActive(false);
            }
        }

        private void EnsureAlertIndicator()
        {
            if (alertIndicator != null)
            {
                return;
            }

            alertIndicator = new GameObject("Alert Indicator", typeof(TextMesh));
            alertIndicator.transform.SetParent(transform, false);
            alertIndicator.transform.localPosition = new Vector3(0f, 2.85f, 0f);
            var text = alertIndicator.GetComponent<TextMesh>();
            text.text = "!";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = .16f;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, .86f, .08f, 1f);
        }

        private void TickDeath(float deltaTime)
        {
            deathElapsed += deltaTime;
            var progress = Mathf.Clamp01(deathElapsed / Mathf.Max(.1f, deathDuration));
            if (deathVisual != null)
            {
                var fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .55f));
                var sink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, progress));
                deathVisual.localRotation = deathStartRotation * Quaternion.Euler(deathFallAngle * fall, 0f, 0f);
                deathVisual.localPosition = deathStartPosition + Vector3.down * (deathSinkDepth * sink);
            }

            if (progress < 1f)
            {
                return;
            }

            deathAnimating = false;
            enabled = false;
            Destroy(gameObject);
        }

        private void NotifyRemoved()
        {
            if (removalReported)
            {
                return;
            }

            removalReported = true;
            Removed?.Invoke(this);
        }

        private static bool IsRanged(MissionMobArchetype archetype)
        {
            return archetype == MissionMobArchetype.Ranged || archetype == MissionMobArchetype.CommanderBoss;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
