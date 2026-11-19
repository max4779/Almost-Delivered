using System;
using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [RequireComponent(typeof(ActorHealth), typeof(CharacterController))]
    public sealed class MissionScoutController : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float sightRadius = 17f;
        [SerializeField, Min(1f)] private float fleeSpeed = 4f;
        [SerializeField, Min(.1f)] private float fleeDuration = 8f;
        private ObservableActor player;
        private MissionCommanderDirector commander;
        private CharacterController character;
        private ActorHealth health;
        private Transform visual;
        private Material camouflage;
        private Texture2D pattern;
        private Rect bounds;
        private int reporterId;
        private float fleeUntil;
        private float nextSense;
        private Vector3 lastSeenPosition;

        public int ReporterId => reporterId;
        public bool IsDefeated => health != null && health.IsDead;
        public bool IsFleeing => Time.time < fleeUntil;
        public bool IsWatchingPlayer { get; private set; }
        public float FleeSpeed => fleeSpeed;
        public Vector3 LastSeenPosition => lastSeenPosition;
        public bool CanSeePlayerNow() => player != null && !IsDefeated && CanSeePlayer();

        public void Configure(ObservableActor playerActor, MissionCommanderDirector commanderDirector,
            int id, Rect missionBounds, Color ground, Color foliage, int seed)
        {
            player = playerActor;
            commander = commanderDirector;
            reporterId = id;
            bounds = missionBounds;
            character = GetComponent<CharacterController>();
            health = GetComponent<ActorHealth>();
            BuildVisual(ground, foliage, seed);
        }

        private void OnEnable()
        {
            health = GetComponent<ActorHealth>();
            health.HealthChanged += OnHealthChanged;
            health.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.HealthChanged -= OnHealthChanged;
            health.Died -= OnDeath;
        }

        private void OnDestroy()
        {
            if (camouflage != null) Destroy(camouflage);
            if (pattern != null) Destroy(pattern);
        }

        private void Update()
        {
            if (health == null || health.IsDead || player == null) return;
            var now = Time.time;
            if (now >= nextSense)
            {
                nextSense = now + .22f;
                IsWatchingPlayer = CanSeePlayer();
                if (IsWatchingPlayer)
                {
                    lastSeenPosition = player.transform.position;
                    commander?.ObserveScoutVision(this, player, lastSeenPosition, player.Velocity);
                    fleeUntil = now + fleeDuration;
                }
            }

            if (now < fleeUntil)
            {
                var away = Vector3.ProjectOnPlane(transform.position - player.transform.position, Vector3.up);
                if (away.sqrMagnitude < .001f) away = transform.forward;
                var direction = away.normalized;
                if (Physics.SphereCast(transform.position + Vector3.up * .5f, .35f, direction,
                        out var hit, 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    !hit.transform.IsChildOf(transform))
                    direction = Quaternion.Euler(0f, reporterId % 2 == 0 ? 65f : -65f, 0f) * direction;
                var next = transform.position + direction * (fleeSpeed * Time.deltaTime);
                if (bounds.Contains(new Vector2(next.x, next.z)))
                    character.Move(direction * (fleeSpeed * Time.deltaTime));
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), 540f * Time.deltaTime);
                if (visual != null) visual.localPosition = Vector3.up * .2f;
            }
            else if (visual != null)
                visual.localPosition = Vector3.zero;

        }

        private bool CanSeePlayer()
        {
            var healthTarget = player.GetComponent<ActorHealth>();
            if (healthTarget != null && healthTarget.IsDead) return false;
            var offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > sightRadius * sightRadius) return false;
            var start = transform.position + Vector3.up * .8f;
            var end = player.transform.position + Vector3.up;
            var ray = end - start;
            foreach (var hit in Physics.RaycastAll(start, ray.normalized, ray.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) &&
                    hit.transform != player.transform && !hit.transform.IsChildOf(player.transform))
                    return false;
            return true;
        }

        private void OnHealthChanged(float current, float maximum)
        {
            if (current >= maximum || current <= 0f) return;
            fleeUntil = Time.time + fleeDuration;
        }

        private void OnDeath()
        {
            IsWatchingPlayer = false;
            if (visual != null) visual.gameObject.SetActive(false);
            Destroy(gameObject, .4f);
        }

        private void BuildVisual(Color ground, Color foliage, int seed)
        {
            var shader = Shader.Find("Standard");
            if (shader == null) return;
            pattern = new Texture2D(64, 64, TextureFormat.RGB24, false);
            pattern.name = "Scout Terrain Camouflage";
            pattern.filterMode = FilterMode.Point;
            pattern.wrapMode = TextureWrapMode.Repeat;
            var random = new System.Random(seed);
            var colors = new[]
            {
                Color.Lerp(ground, foliage, .25f),
                Color.Lerp(ground, foliage, .65f),
                Color.Lerp(foliage, Color.black, .17f)
            };
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var patch = ((x / 8) * 13 + (y / 8) * 7 + random.Next(4)) % colors.Length;
                pattern.SetPixel(x, y, colors[patch]);
            }
            pattern.Apply();
            camouflage = new Material(shader) { name = "Scout Camouflage", mainTexture = pattern };
            var root = new GameObject("Camouflaged Scout Visual").transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * .86f;
            visual = root;
            Part(root, PrimitiveType.Capsule, "Crouched Body", new Vector3(0f, .65f, 0f),
                new Vector3(.62f, .58f, .62f));
            Part(root, PrimitiveType.Sphere, "Hood", new Vector3(0f, 1.16f, .08f),
                new Vector3(.52f, .37f, .52f));
            Part(root, PrimitiveType.Cube, "Cloak", new Vector3(0f, .72f, -.28f),
                new Vector3(.86f, .7f, .15f));
            Part(root, PrimitiveType.Cube, "Left Leg", new Vector3(-.25f, .25f, 0f),
                new Vector3(.22f, .42f, .26f));
            Part(root, PrimitiveType.Cube, "Right Leg", new Vector3(.25f, .25f, 0f),
                new Vector3(.22f, .42f, .26f));
        }

        private void Part(Transform root, PrimitiveType shape, string name, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = camouflage;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
    }
}
