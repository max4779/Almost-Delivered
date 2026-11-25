using AlmostDelivered.Combat;
using AlmostDelivered.Gameplay.Payload;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Support
{
    /// <summary>One owner's temporary turret. Targets only mission enemies and expires on its own.</summary>
    public sealed class AutoTurret : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 16f;
        [SerializeField, Min(.1f)] private float fireInterval = .65f;
        private PayloadWaveSpawner waves;
        private float expiresAt;
        private float nextShotAt;
        private float damage;
        private Transform head;
        private LineRenderer tracer;
        private float tracerEndsAt;
        private Material bodyMaterial;
        private Material tracerMaterial;

        public string OwnerAccountId { get; private set; }
        public float Damage => damage;

        public void Configure(string ownerAccountId, PayloadWaveSpawner spawner,
            float lifetime, float shotDamage)
        {
            OwnerAccountId = ownerAccountId;
            waves = spawner;
            expiresAt = Time.time + Mathf.Max(.1f, lifetime);
            damage = Mathf.Max(.1f, shotDamage);
            nextShotAt = Time.time + .3f;
            BuildVisual();
        }

        private void Update()
        {
            if (Time.time >= expiresAt)
            {
                Destroy(gameObject);
                return;
            }
            if (tracer != null && Time.time >= tracerEndsAt) tracer.enabled = false;
            if (waves == null || Time.time < nextShotAt) return;
            var target = FindTarget();
            if (target == null) return;
            var targetPoint = target.transform.position + Vector3.up * 1f;
            var origin = transform.position + Vector3.up * 1.45f;
            var direction = targetPoint - origin;
            var planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (head != null && planar.sqrMagnitude > .001f)
                head.rotation = Quaternion.LookRotation(planar, Vector3.up);
            target.GetComponent<ActorHealth>()?.ApplyDamage(damage);
            if (tracer != null)
            {
                tracer.SetPosition(0, origin);
                tracer.SetPosition(1, targetPoint);
                tracer.enabled = true;
                tracerEndsAt = Time.time + .09f;
            }
            nextShotAt = Time.time + fireInterval;
        }

        private MissionMobController FindTarget()
        {
            MissionMobController closest = null;
            var bestDistance = range * range;
            var origin = transform.position + Vector3.up * 1.45f;
            foreach (var enemy in waves.LivingEnemies)
            {
                if (enemy == null || enemy.IsDefeated) continue;
                var point = enemy.transform.position + Vector3.up;
                var distance = (point - origin).sqrMagnitude;
                if (distance >= bestDistance) continue;
                if (Physics.Linecast(origin, point, out var hit, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore) &&
                    hit.transform != enemy.transform && !hit.transform.IsChildOf(enemy.transform))
                    continue;
                closest = enemy;
                bestDistance = distance;
            }
            return closest;
        }

        private void BuildVisual()
        {
            if (head != null) return;
            var shader = Shader.Find("Standard");
            bodyMaterial = shader != null ? new Material(shader) : null;
            if (bodyMaterial != null)
            {
                bodyMaterial.color = new Color(.12f, .44f, .52f);
                bodyMaterial.EnableKeyword("_EMISSION");
                bodyMaterial.SetColor("_EmissionColor", new Color(.03f, .5f, .75f));
            }
            CreatePart("Turret Base", PrimitiveType.Cylinder, new Vector3(0f, .18f, 0f),
                new Vector3(.75f, .18f, .75f), transform);
            CreatePart("Turret Mast", PrimitiveType.Cube, new Vector3(0f, .85f, 0f),
                new Vector3(.2f, 1.1f, .2f), transform);
            head = new GameObject("Turret Head").transform;
            head.SetParent(transform, false);
            head.localPosition = Vector3.up * 1.45f;
            CreatePart("Turret Sensor", PrimitiveType.Cube, Vector3.zero,
                new Vector3(.6f, .35f, .7f), head);
            CreatePart("Turret Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, .55f),
                new Vector3(.12f, .55f, .12f), head).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);

            var beam = new GameObject("Turret Shot", typeof(LineRenderer));
            beam.transform.SetParent(transform, false);
            tracer = beam.GetComponent<LineRenderer>();
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.widthMultiplier = .055f;
            tracerMaterial = new Material(Shader.Find("Sprites/Default"));
            tracer.material = tracerMaterial;
            tracer.startColor = new Color(.2f, 1f, 1f);
            tracer.endColor = new Color(.8f, 1f, 1f);
            tracer.enabled = false;
        }

        private GameObject CreatePart(string label, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Transform parent)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (bodyMaterial != null) part.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            var collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return part;
        }

        private void OnDestroy()
        {
            if (bodyMaterial != null) Destroy(bodyMaterial);
            if (tracerMaterial != null) Destroy(tracerMaterial);
        }
    }
}
