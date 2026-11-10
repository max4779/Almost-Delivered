using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.Presentation
{
    /// <summary>Player-only muzzle flash, recoil and sound for successful shots.</summary>
    public sealed class GunfireFeedback : MonoBehaviour
    {
        [SerializeField] private RangedAttackController attack;
        [SerializeField] private Transform muzzle;
        [SerializeField] private AudioClip rifleShot;
        [SerializeField] private AudioClip revolverShot;
        [SerializeField] private Transform flashRoot;
        [SerializeField] private Light flashLight;
        [SerializeField] private AudioSource audioSource;

        private float flashRemaining;
        private float flashDuration;
        private float flashSize;

        public bool IsFlashing => flashRoot != null && flashRoot.gameObject.activeSelf;

        public void Configure(RangedAttackController attackController, Transform muzzleTransform,
            AudioClip rifleClip, AudioClip revolverClip, Material flashMaterial)
        {
            if (isActiveAndEnabled && attack != null) attack.ShotFired -= OnShotFired;
            attack = attackController;
            muzzle = muzzleTransform;
            rifleShot = rifleClip;
            revolverShot = revolverClip;

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = .7f;

            if (flashRoot == null && muzzle != null)
            {
                flashRoot = new GameObject("Muzzle Flash").transform;
                flashRoot.SetParent(muzzle, false);
                MakeFlashPart("Horizontal", PrimitiveType.Cube, new Vector3(.5f, .09f, .34f),
                    Quaternion.identity, flashMaterial);
                MakeFlashPart("Vertical", PrimitiveType.Cube, new Vector3(.09f, .5f, .34f),
                    Quaternion.identity, flashMaterial);
                MakeFlashPart("Core", PrimitiveType.Sphere, Vector3.one * .2f,
                    Quaternion.identity, flashMaterial);
                flashLight = flashRoot.gameObject.AddComponent<Light>();
                flashLight.type = LightType.Point;
                flashLight.color = new Color(1f, .67f, .25f);
                flashLight.range = 2.2f;
                flashLight.intensity = 2.6f;
                flashRoot.gameObject.SetActive(false);
            }

            if (isActiveAndEnabled && attack != null) attack.ShotFired += OnShotFired;
        }

        private void OnEnable()
        {
            if (attack != null) attack.ShotFired += OnShotFired;
        }

        private void OnDisable()
        {
            if (attack != null) attack.ShotFired -= OnShotFired;
            flashRemaining = 0f;
            if (flashRoot != null) flashRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (flashRemaining <= 0f) return;
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.deltaTime);
            if (flashRoot == null) return;
            if (flashRemaining <= 0f)
            {
                flashRoot.gameObject.SetActive(false);
                return;
            }
            flashRoot.localScale = Vector3.one * (flashSize * (.65f + .35f * flashRemaining / flashDuration));
            if (flashLight != null) flashLight.intensity = 2.6f * flashRemaining / flashDuration;
        }

        private void OnShotFired()
        {
            var revolver = attack != null && attack.MagazineSize == 6;
            var activeMuzzle = attack != null && attack.Muzzle != null ? attack.Muzzle : muzzle;
            if (flashRoot != null && activeMuzzle != null && flashRoot.parent != activeMuzzle)
                flashRoot.SetParent(activeMuzzle, false);
            GetComponentInChildren<ProceduralPoseAnimator>(true)?.PlayGunshot(revolver);
            var clip = revolver ? revolverShot : rifleShot;
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);

            flashDuration = revolver ? .11f : .065f;
            flashRemaining = flashDuration;
            flashSize = revolver ? 1.3f : .85f;
            if (flashRoot != null)
            {
                flashRoot.localPosition = Vector3.forward * (revolver ? .36f : .32f);
                flashRoot.localScale = Vector3.one * flashSize;
                flashRoot.gameObject.SetActive(true);
            }
        }

        private void MakeFlashPart(string partName, PrimitiveType primitive, Vector3 scale,
            Quaternion rotation, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(flashRoot, false);
            part.transform.localPosition = new Vector3(0f, 0f, .12f);
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
