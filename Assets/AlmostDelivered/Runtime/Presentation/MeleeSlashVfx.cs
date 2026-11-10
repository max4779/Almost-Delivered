using UnityEngine;
using UnityEngine.Rendering;

namespace AlmostDelivered.Presentation
{
    /// <summary>Draws the exact horizontal hit arc and a short moving wind trail.</summary>
    public sealed class MeleeSlashVfx : MonoBehaviour
    {
        private const int Segments = 28;
        private const float Duration = .26f;
        [SerializeField] private Material windMaterial;
        private Mesh effectMesh;
        private MeshRenderer effectRenderer;
        private Material fallbackMaterial;
        private Color[] colors;
        private float elapsed;
        private float impactRemaining;
        private Vector3 attackPosition;
        private Quaternion attackRotation;

        public float Reach { get; private set; }
        public float ArcDegrees { get; private set; }
        public bool IsVisible => effectRenderer != null && effectRenderer.enabled;
        public Mesh EffectMesh => effectMesh;
        public Material WindMaterial => windMaterial;
        public Vector3 AttackDirection => attackRotation * Vector3.forward;

        public void Configure(Material material)
        {
            windMaterial = material;
            if (effectRenderer != null)
                effectRenderer.sharedMaterial = material != null ? material : FallbackMaterial();
        }

        public void Play(float reach, float arcDegrees)
        {
            Play(reach, arcDegrees, transform.forward);
        }

        public void Play(float reach, float arcDegrees, Vector3 direction)
        {
            Reach = Mathf.Max(.1f, reach);
            ArcDegrees = Mathf.Clamp(arcDegrees, 1f, 360f);
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude <= .0001f) direction = transform.forward;
            attackPosition = transform.position + Vector3.down * .45f;
            attackRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            EnsureVisual();
            BuildArc();
            elapsed = 0f;
            impactRemaining = 0f;
            effectRenderer.enabled = true;
            PinToAttack();
            UpdateColors();
        }

        public void ConfirmHit()
        {
            if (!IsVisible) return;
            impactRemaining = .13f;
            UpdateColors();
        }

        private void Update()
        {
            if (!IsVisible) return;
            elapsed += Time.deltaTime;
            impactRemaining = Mathf.Max(0f, impactRemaining - Time.deltaTime);
            if (elapsed >= Duration)
            {
                effectRenderer.enabled = false;
                return;
            }
            UpdateColors();
        }

        private void LateUpdate()
        {
            if (IsVisible) PinToAttack();
        }

        private void PinToAttack()
        {
            effectRenderer.transform.SetPositionAndRotation(attackPosition, attackRotation);
        }

        private void OnDisable()
        {
            if (effectRenderer != null) effectRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (effectMesh != null)
            {
                if (Application.isPlaying) Destroy(effectMesh);
                else DestroyImmediate(effectMesh);
            }
            if (fallbackMaterial != null)
            {
                if (Application.isPlaying) Destroy(fallbackMaterial);
                else DestroyImmediate(fallbackMaterial);
            }
        }

        private void EnsureVisual()
        {
            if (effectRenderer != null) return;
            var visual = new GameObject("Melee Wind Arc", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, -.45f, 0f);
            effectMesh = new Mesh { name = "Melee Wind Arc Mesh" };
            effectMesh.MarkDynamic();
            visual.GetComponent<MeshFilter>().sharedMesh = effectMesh;
            effectRenderer = visual.GetComponent<MeshRenderer>();
            effectRenderer.sharedMaterial = windMaterial != null ? windMaterial : FallbackMaterial();
            effectRenderer.shadowCastingMode = ShadowCastingMode.Off;
            effectRenderer.receiveShadows = false;
            effectRenderer.enabled = false;
        }

        private Material FallbackMaterial()
        {
            if (fallbackMaterial == null)
                fallbackMaterial = new Material(Shader.Find("Sprites/Default"));
            return fallbackMaterial;
        }

        private void BuildArc()
        {
            var vertices = new Vector3[Segments * 8];
            var triangles = new int[Segments * 12];
            colors = new Color[vertices.Length];
            var innerRadius = Mathf.Min(.75f, Reach * .3f);
            var edgeInnerRadius = Mathf.Max(innerRadius, Reach - .12f);
            for (var i = 0; i < Segments; i++)
            {
                var first = (-ArcDegrees * .5f + ArcDegrees * i / Segments) * Mathf.Deg2Rad;
                var second = (-ArcDegrees * .5f + ArcDegrees * (i + 1) / Segments) * Mathf.Deg2Rad;
                var firstDirection = new Vector3(Mathf.Sin(first), 0f, Mathf.Cos(first));
                var secondDirection = new Vector3(Mathf.Sin(second), 0f, Mathf.Cos(second));
                var start = i * 8;
                vertices[start] = firstDirection * innerRadius;
                vertices[start + 1] = secondDirection * innerRadius;
                vertices[start + 2] = firstDirection * Reach;
                vertices[start + 3] = secondDirection * Reach;
                vertices[start + 4] = firstDirection * edgeInnerRadius;
                vertices[start + 5] = secondDirection * edgeInnerRadius;
                vertices[start + 6] = firstDirection * Reach;
                vertices[start + 7] = secondDirection * Reach;
                var triangle = i * 12;
                for (var band = 0; band < 2; band++)
                {
                    var offset = start + band * 4;
                    var index = triangle + band * 6;
                    triangles[index] = offset;
                    triangles[index + 1] = offset + 1;
                    triangles[index + 2] = offset + 2;
                    triangles[index + 3] = offset + 1;
                    triangles[index + 4] = offset + 3;
                    triangles[index + 5] = offset + 2;
                }
            }
            effectMesh.Clear();
            effectMesh.vertices = vertices;
            effectMesh.triangles = triangles;
            effectMesh.RecalculateBounds();
        }

        private void UpdateColors()
        {
            var progress = Mathf.Clamp01(elapsed / .09f);
            var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, Duration, elapsed));
            var impact = Mathf.Clamp01(impactRemaining / .13f);
            var color = Color.Lerp(new Color(.42f, .95f, 1f, 1f),
                new Color(1f, .9f, .72f, 1f), impact);
            for (var i = 0; i < Segments; i++)
            {
                var position = (i + .5f) / Segments;
                var trail = position <= progress ? Mathf.Exp(-(progress - position) * 12f) : 0f;
                var start = i * 8;
                color.a = fade * (.1f + trail * .2f + impact * .1f);
                colors[start] = colors[start + 1] = color;
                color.a = fade * (.28f + trail * .45f + impact * .2f);
                colors[start + 2] = colors[start + 3] = color;
                color.a = fade * (.52f + trail * .3f + impact * .15f);
                colors[start + 4] = colors[start + 5] = color;
                color.a = fade * Mathf.Min(1f, .8f + trail * .25f + impact * .2f);
                colors[start + 6] = colors[start + 7] = color;
            }
            effectMesh.colors = colors;
        }
    }
}
