using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [RequireComponent(typeof(LineRenderer), typeof(AudioSource))]
    public sealed class PayloadActivationView : MonoBehaviour
    {
        private const int SegmentCount = 64;

        [SerializeField] private PayloadMissionController mission;
        [SerializeField] private LineRenderer ring;
        [SerializeField] private TextMesh label;
        [SerializeField] private AudioSource startupAudio;
        [SerializeField, Min(.1f)] private float baseRadius = 2.15f;
        private bool subscribed;
        private AudioClip runtimeStartupClip;

        public bool IsVisible => ring != null && ring.enabled;
        public TextMesh Label => label;

        public void Configure(PayloadMissionController missionController, Material material)
        {
            Unsubscribe();
            mission = missionController;
            EnsureVisuals();
            if (material != null)
            {
                ring.sharedMaterial = material;
            }

            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            EnsureVisuals();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (startupAudio != null)
            {
                startupAudio.Stop();
            }
        }

        private void OnDestroy()
        {
            if (runtimeStartupClip != null)
            {
                Destroy(runtimeStartupClip);
            }
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            DrawRing(baseRadius + Mathf.Sin(Time.time * 9f) * .16f);
            if (Camera.main != null && label != null)
            {
                var direction = label.transform.position - Camera.main.transform.position;
                if (direction.sqrMagnitude > .0001f)
                {
                    label.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }
        }

        public void Refresh()
        {
            EnsureVisuals();
            var active = mission != null && mission.Snapshot.Phase == PayloadMissionPhase.Activating;
            ring.enabled = active;
            label.gameObject.SetActive(active);
            RefreshAudio(active);
            if (!active)
            {
                return;
            }

            var progress = mission.Snapshot.ActivationProgress;
            var color = Color.Lerp(new Color(1f, .48f, .05f, .9f), new Color(.1f, .95f, 1f, .95f), progress);
            ring.startColor = color;
            ring.endColor = color;
            label.color = color;
            label.text = $"CARGO STARTUP  {Mathf.RoundToInt(progress * 100f)}%";
        }

        private void RefreshAudio(bool active)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureAudio();
            if (active && !startupAudio.isPlaying)
            {
                startupAudio.Play();
            }
            else if (!active && startupAudio.isPlaying)
            {
                startupAudio.Stop();
            }
        }

        private void EnsureAudio()
        {
            ConfigureAudioSource();
            if (runtimeStartupClip == null)
            {
                runtimeStartupClip = CreateStartupClip();
                startupAudio.clip = runtimeStartupClip;
            }
        }

        private static AudioClip CreateStartupClip()
        {
            const int sampleRate = 22050;
            const float duration = .8f;
            var sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            var random = new System.Random(4711);
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var pulse = .55f + .45f * Mathf.Sin(time * Mathf.PI * 5f);
                var motor = Mathf.Sin(time * Mathf.PI * 2f * 74f) * .55f +
                            Mathf.Sin(time * Mathf.PI * 2f * 111f) * .24f;
                var grit = ((float)random.NextDouble() * 2f - 1f) * .12f;
                samples[index] = (motor + grit) * pulse * .38f;
            }

            var clip = AudioClip.Create("Cargo Startup Loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void EnsureVisuals()
        {
            if (ring == null)
            {
                ring = GetComponent<LineRenderer>();
            }

            ring.useWorldSpace = false;
            ring.loop = true;
            ring.startWidth = .18f;
            ring.endWidth = .18f;
            ring.numCornerVertices = 2;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            DrawRing(baseRadius);

            if (label == null)
            {
                var labelObject = new GameObject("Activation Label", typeof(TextMesh));
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition = Vector3.up * 2.8f;
                label = labelObject.GetComponent<TextMesh>();
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontSize = 48;
                label.characterSize = .095f;
                label.fontStyle = FontStyle.Bold;
            }

            ConfigureAudioSource();
        }

        private void ConfigureAudioSource()
        {
            if (startupAudio == null)
            {
                startupAudio = GetComponent<AudioSource>();
            }

            if (startupAudio == null)
            {
                return;
            }

            startupAudio.playOnAwake = false;
            startupAudio.loop = true;
            startupAudio.spatialBlend = .85f;
            startupAudio.volume = .22f;
            startupAudio.minDistance = 2f;
            startupAudio.maxDistance = 24f;
            startupAudio.rolloffMode = AudioRolloffMode.Linear;
        }

        private void DrawRing(float radius)
        {
            ring.positionCount = SegmentCount;
            for (var index = 0; index < SegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / SegmentCount;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, .12f, Mathf.Sin(angle) * radius));
            }
        }

        private void Subscribe()
        {
            if (subscribed || mission == null || !isActiveAndEnabled)
            {
                return;
            }

            mission.SnapshotChanged += HandleSnapshotChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (subscribed && mission != null)
            {
                mission.SnapshotChanged -= HandleSnapshotChanged;
            }

            subscribed = false;
        }

        private void HandleSnapshotChanged(PayloadMissionSnapshot _)
        {
            Refresh();
        }
    }
}
