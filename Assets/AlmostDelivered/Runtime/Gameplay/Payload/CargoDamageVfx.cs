using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>Shows the cargo's persistent section damage on every client.</summary>
    public sealed class CargoDamageVfx : MonoBehaviour
    {
        private PayloadMissionController mission;
        private ParticleSystem leftSmoke;
        private ParticleSystem rightSmoke;
        private ParticleSystem leftFlame;
        private ParticleSystem rightFlame;
        private ParticleSystem burstSmoke;
        private ParticleSystem burstSparks;
        private Material particleMaterial;
        private Texture2D particleTexture;
        private AudioSource destructionAudio;
        private AudioClip destructionClip;
        private Light destructionFlash;
        private Transform visualRoot;
        private Vector3 visualPosition;
        private Quaternion visualRotation;
        private float cueElapsed = -1f;

        public int ActiveDamageStage { get; private set; } = -1;
        public int DestructionCueCount { get; private set; }

        public void Configure(PayloadMissionController controller)
        {
            if (mission != null) mission.SnapshotChanged -= OnSnapshotChanged;
            mission = controller;
            EnsureEmitters();
            if (isActiveAndEnabled && mission != null)
                mission.SnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(mission != null ? mission.Snapshot : default);
        }

        private void OnEnable()
        {
            if (mission == null) return;
            mission.SnapshotChanged -= OnSnapshotChanged;
            mission.SnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(mission.Snapshot);
        }

        private void OnDisable()
        {
            if (mission != null) mission.SnapshotChanged -= OnSnapshotChanged;
            if (destructionFlash != null) destructionFlash.enabled = false;
            if (visualRoot != null)
            {
                visualRoot.localPosition = visualPosition;
                visualRoot.localRotation = visualRotation;
            }
        }

        private void OnDestroy()
        {
            if (mission != null) mission.SnapshotChanged -= OnSnapshotChanged;
            if (particleMaterial != null) Destroy(particleMaterial);
            if (particleTexture != null) Destroy(particleTexture);
            if (destructionClip != null) Destroy(destructionClip);
        }

        private void LateUpdate()
        {
            if (visualRoot == null || cueElapsed < 0f) return;
            var final = ActiveDamageStage >= 3;
            var duration = final ? .85f : .46f;
            cueElapsed = Mathf.Min(duration, cueElapsed + Time.deltaTime);
            var progress = cueElapsed / duration;
            var shake = Mathf.Sin(progress * Mathf.PI * 11f) * (1f - progress);
            var tilt = final ? Mathf.SmoothStep(0f, 11f, progress) : 0f;
            if (destructionFlash != null)
            {
                destructionFlash.intensity = (final ? 3.6f : 2.4f) *
                    Mathf.Pow(1f - progress, 8f);
                destructionFlash.enabled = destructionFlash.intensity > .02f;
            }
            visualRoot.localPosition = visualPosition + new Vector3(shake * .15f, -shake * .09f -
                (final ? Mathf.SmoothStep(0f, .3f, progress) : 0f), 0f);
            visualRoot.localRotation = visualRotation * Quaternion.Euler(shake * 6f, 0f, tilt + shake * 4f);
            if (!final && cueElapsed >= duration)
            {
                visualRoot.localPosition = visualPosition;
                visualRoot.localRotation = visualRotation;
                cueElapsed = -1f;
            }
        }

        private void OnSnapshotChanged(PayloadMissionSnapshot snapshot)
        {
            var stage = Mathf.Clamp(snapshot.CargoDestructions, 0, 3);
            if (stage == ActiveDamageStage) return;
            var previous = ActiveDamageStage;
            ActiveDamageStage = stage;
            SetEmission(leftSmoke, stage >= 3 ? 35f : stage >= 2 ? 23f : stage >= 1 ? 11f : 0f);
            SetEmission(rightSmoke, stage >= 3 ? 31f : stage >= 2 ? 20f : 0f);
            SetEmission(leftFlame, stage >= 3 ? 16f : stage >= 2 ? 9f : 0f);
            SetEmission(rightFlame, stage >= 3 ? 14f : stage >= 2 ? 8f : 0f);
            if (previous >= 0 && stage > previous)
                PlayDestructionCue(stage);
        }

        private void PlayDestructionCue(int stage)
        {
            DestructionCueCount++;
            cueElapsed = 0f;
            if (destructionFlash != null)
            {
                destructionFlash.intensity = stage >= 3 ? 3.6f : 2.4f;
                destructionFlash.enabled = true;
            }
            if (burstSmoke != null)
            {
                burstSmoke.Play();
                burstSmoke.Emit(stage >= 3 ? 44 : 22);
            }
            if (burstSparks != null)
            {
                burstSparks.Play();
                burstSparks.Emit(stage >= 3 ? 22 : 12);
            }
            if (destructionAudio != null && destructionClip != null)
                destructionAudio.PlayOneShot(destructionClip, stage >= 3 ? 1f : .68f);
        }

        private static void SetEmission(ParticleSystem particles, float rate)
        {
            if (particles == null) return;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            if (rate > 0f)
            {
                if (!particles.isPlaying) particles.Play();
            }
            else if (particles.isPlaying)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureEmitters()
        {
            if (leftSmoke != null) return;
            particleTexture = BuildSoftParticle();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null)
            {
                Debug.LogWarning("Cargo damage particles need a transparent shader.", this);
                return;
            }
            particleMaterial = new Material(shader) { name = "Cargo Damage Particle" };
            particleMaterial.mainTexture = particleTexture;
            leftSmoke = CreateEmitter("Cargo Damage Smoke Left", new Vector3(-.8f, 1.6f, -.15f),
                new Color(.32f, .38f, .4f, .53f), 1.7f, 2.7f, .55f, 1.08f, .55f, 1.25f);
            rightSmoke = CreateEmitter("Cargo Damage Smoke Right", new Vector3(.8f, 1.6f, -.15f),
                new Color(.27f, .32f, .34f, .6f), 1.8f, 2.9f, .6f, 1.12f, .55f, 1.3f);
            leftFlame = CreateEmitter("Cargo Damage Flame Left", new Vector3(-.8f, 1.45f, -.15f),
                new Color(1f, .39f, .08f, .82f), .3f, .58f, .23f, .43f, .22f, .52f);
            rightFlame = CreateEmitter("Cargo Damage Flame Right", new Vector3(.8f, 1.45f, -.15f),
                new Color(1f, .55f, .12f, .78f), .28f, .55f, .21f, .4f, .2f, .48f);
            burstSmoke = CreateEmitter("Cargo Destruction Smoke Burst", new Vector3(0f, 1.5f, 0f),
                new Color(.34f, .38f, .4f, .75f), .65f, 1.2f, .55f, 1f, 1.3f, 2.6f);
            burstSparks = CreateEmitter("Cargo Destruction Sparks", new Vector3(0f, 1.5f, 0f),
                new Color(1f, .62f, .2f, .9f), .24f, .55f, .08f, .18f, 2f, 4f);
            var smokeShape = burstSmoke.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Sphere;
            smokeShape.radius = .25f;
            var sparkShape = burstSparks.shape;
            sparkShape.shapeType = ParticleSystemShapeType.Sphere;
            sparkShape.radius = .18f;
            var sparkMain = burstSparks.main;
            sparkMain.gravityModifier = .8f;
            destructionFlash = new GameObject("Cargo Destruction Flash").AddComponent<Light>();
            destructionFlash.transform.SetParent(transform, false);
            destructionFlash.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            destructionFlash.type = LightType.Point;
            destructionFlash.color = new Color(1f, .48f, .16f);
            destructionFlash.range = 5f;
            destructionFlash.enabled = false;
            visualRoot = transform.Find("VisualRoot");
            if (visualRoot != null)
            {
                visualPosition = visualRoot.localPosition;
                visualRotation = visualRoot.localRotation;
            }
            destructionAudio = gameObject.AddComponent<AudioSource>();
            destructionAudio.playOnAwake = false;
            destructionAudio.spatialBlend = .6f;
            destructionAudio.volume = .75f;
            destructionAudio.minDistance = 8f;
            destructionAudio.maxDistance = 65f;
            destructionAudio.rolloffMode = AudioRolloffMode.Linear;
            destructionClip = CreateDestructionClip();
        }

        private static AudioClip CreateDestructionClip()
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.RoundToInt(sampleRate * .72f)];
            var random = new System.Random(8191);
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)sampleRate;
                var impact = Mathf.Exp(-time * 13f);
                var rumble = Mathf.Exp(-time * 3.5f);
                var metal = Mathf.Sin(time * Mathf.PI * 2f * (116f - time * 75f)) * impact;
                var low = Mathf.Sin(time * Mathf.PI * 2f * 54f) * rumble;
                var grit = ((float)random.NextDouble() * 2f - 1f) * (impact + rumble * .35f);
                samples[i] = Mathf.Clamp((metal * .27f + low * .32f + grit * .19f) *
                    Mathf.Min(1f, time * 400f), -.8f, .8f);
            }
            var clip = AudioClip.Create("Cargo Section Break", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private ParticleSystem CreateEmitter(string name, Vector3 position, Color color,
            float minLifetime, float maxLifetime, float minSize, float maxSize,
            float minSpeed, float maxSpeed)
        {
            var emitter = new GameObject(name);
            emitter.SetActive(false);
            emitter.transform.SetParent(transform, false);
            emitter.transform.localPosition = position;
            var particles = emitter.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startColor = color;
            main.maxParticles = 170;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = .14f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .2f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = particleMaterial;
            emitter.SetActive(true);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static Texture2D BuildSoftParticle()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Cargo Damage Soft Particle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var offset = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - offset.magnitude), 1.7f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
