using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>The extraction call is a short, loud visual cue; the defense timer lives on the mission.</summary>
    public sealed class EvacuationSignalView : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float fadeSeconds = 7f;
        private PayloadMissionController mission;
        private Transform beam;
        private LineRenderer pulse;
        private Light flash;
        private AudioSource audioSource;
        private Material beamMaterial;
        private Material pulseMaterial;
        private float elapsed;
        private bool active;

        public bool IsSignalVisible => active;
        public float SignalIntensity => flash != null ? flash.intensity : 0f;

        public void Configure(PayloadMissionController source)
        {
            if (mission != null) mission.PhaseChanged -= OnPhaseChanged;
            mission = source;
            if (mission != null) mission.PhaseChanged += OnPhaseChanged;
            EnsureVisuals();
            if (mission != null && mission.Snapshot.Phase == PayloadMissionPhase.EvacuationDefense)
                Activate();
        }

        private void OnDestroy()
        {
            if (mission != null) mission.PhaseChanged -= OnPhaseChanged;
            if (audioSource != null && audioSource.clip != null)
                Destroy(audioSource.clip);
            if (beamMaterial != null) Destroy(beamMaterial);
            if (pulseMaterial != null) Destroy(pulseMaterial);
        }

        private void OnPhaseChanged(PayloadMissionPhase previous, PayloadMissionPhase current)
        {
            if (current == PayloadMissionPhase.EvacuationDefense) Activate();
        }

        public void Activate()
        {
            EnsureVisuals();
            if (beam == null || pulse == null || flash == null) return;
            elapsed = 0f;
            active = true;
            beam.gameObject.SetActive(true);
            pulse.gameObject.SetActive(true);
            flash.enabled = true;
            if (audioSource != null && audioSource.clip != null) audioSource.Play();
            UpdateVisual(0f);
        }

        private void Update()
        {
            if (!active) return;
            elapsed += Time.deltaTime;
            UpdateVisual(Mathf.Clamp01(elapsed / fadeSeconds));
            if (elapsed < fadeSeconds) return;
            active = false;
            beam.gameObject.SetActive(false);
            pulse.gameObject.SetActive(false);
            flash.enabled = false;
        }

        private void UpdateVisual(float progress)
        {
            var strength = Mathf.Pow(1f - progress, 1.5f);
            var shimmer = 1f + Mathf.Sin(elapsed * 21f) * .1f;
            beam.localScale = new Vector3(Mathf.Max(.025f, .5f * strength * shimmer),
                3.4f + strength * 1.6f, Mathf.Max(.025f, .5f * strength * shimmer));
            flash.intensity = 7f * strength;
            flash.range = 9f + 24f * strength;
            pulse.widthMultiplier = .06f + .3f * strength;
            var color = new Color(1f, .28f, .84f, strength);
            pulse.startColor = pulse.endColor = color;
            var radius = 1.8f + progress * 13f;
            for (var index = 0; index < pulse.positionCount; index++)
            {
                var angle = index * Mathf.PI * 2f / pulse.positionCount;
                pulse.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, .14f,
                    Mathf.Sin(angle) * radius));
            }
            if (beamMaterial != null)
                beamMaterial.SetColor("_EmissionColor", new Color(1f, .18f, .8f) * (3f * strength));
        }

        private void EnsureVisuals()
        {
            if (beam != null) return;
            var beamObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beamObject.name = "Extraction Signal Beam";
            beam = beamObject.transform;
            beam.SetParent(transform, false);
            beam.localPosition = new Vector3(0f, 4f, 0f);
            var collider = beamObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            var shader = Shader.Find("Standard");
            if (shader != null)
            {
                beamMaterial = new Material(shader) { color = new Color(1f, .44f, .88f) };
                beamMaterial.EnableKeyword("_EMISSION");
                beamObject.GetComponent<Renderer>().material = beamMaterial;
            }

            var pulseObject = new GameObject("Extraction Signal Pulse", typeof(LineRenderer));
            pulseObject.transform.SetParent(transform, false);
            pulse = pulseObject.GetComponent<LineRenderer>();
            pulse.useWorldSpace = false;
            pulse.loop = true;
            pulse.positionCount = 48;
            pulse.numCapVertices = 3;
            pulseMaterial = new Material(Shader.Find("Sprites/Default"));
            pulse.material = pulseMaterial;

            var lightObject = new GameObject("Extraction Signal Flash", typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.up * 4f;
            flash = lightObject.GetComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, .28f, .83f);
            flash.shadows = LightShadows.None;

            audioSource = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = .7f;
            audioSource.minDistance = 5f;
            audioSource.maxDistance = 90f;
            audioSource.volume = .8f;
            audioSource.clip = BuildOriginalSignalSound();
            beam.gameObject.SetActive(false);
            pulse.gameObject.SetActive(false);
            flash.enabled = false;
        }

        private static AudioClip BuildOriginalSignalSound()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate * 3;
            var data = new float[sampleCount];
            var phase = 0f;
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var envelope = Mathf.Min(1f, time * 13f) * Mathf.Pow(1f - time / 3f, 1.3f);
                var frequency = 720f - 420f * time / 3f + 45f * Mathf.Sin(time * 8f);
                phase += 2f * Mathf.PI * frequency / sampleRate;
                var harmonic = Mathf.Sin(phase) * .72f + Mathf.Sin(phase * 2.01f) * .2f;
                var laserSweep = Mathf.Sin(2f * Mathf.PI * (100f * time + 520f * time * time)) *
                    Mathf.Exp(-time * 3f) * .16f;
                data[index] = (harmonic + laserSweep) * envelope * .55f;
            }
            var clip = AudioClip.Create("Original Extraction Signal", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
