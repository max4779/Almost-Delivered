using AlmostDelivered.Combat;
using UnityEngine.EventSystems;
using UnityEngine;

namespace AlmostDelivered.Frontend
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class HubPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 6f;
        [SerializeField, Min(1f)] private float turnSpeed = 720f;

        private CharacterController characterController;
        private RangedAttackController rangedAttack;
        private MeleeAttackController meleeAttack;
        private bool inputLocked;

        public bool IsInputLocked => inputLocked;

        public void SetInputLocked(bool locked) => inputLocked = locked;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            rangedAttack = GetComponent<RangedAttackController>();
            meleeAttack = GetComponent<MeleeAttackController>();
        }

        private void Start() => SetWeaponType(PlayerWeaponLoadout.Selected);

        public void SetWeaponType(PlayerWeaponType weapon)
        {
            if (rangedAttack != null) rangedAttack.enabled = weapon == PlayerWeaponType.Ranged;
            if (meleeAttack != null) meleeAttack.enabled = weapon == PlayerWeaponType.Melee;
            PlayerWeaponLoadout.ApplyVisual(gameObject, weapon);
        }

        private void Update()
        {
            if (inputLocked) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToFrontend();
                return;
            }

            Move(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Time.deltaTime);
            if (TryGetMouseAim(out var aimPoint))
            {
                AimAt(aimPoint, Time.deltaTime);
                if (Input.GetMouseButton(0) &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                    AimAndFire(aimPoint, Time.time, Time.deltaTime);
            }
        }

        public bool AimAndFire(Vector3 aimPoint, float now, float deltaTime)
        {
            if (inputLocked || !IsFinite(aimPoint.x) || !IsFinite(aimPoint.y) || !IsFinite(aimPoint.z))
                return false;
            AimAt(aimPoint, deltaTime);
            return PlayerWeaponLoadout.Selected == PlayerWeaponType.Melee
                ? meleeAttack != null && meleeAttack.TryAttack(now, aimPoint - transform.position)
                : rangedAttack != null && rangedAttack.TryFire(aimPoint, now);
        }

        public bool AimAt(Vector3 aimPoint, float deltaTime)
        {
            if (inputLocked || !IsFinite(deltaTime) || deltaTime <= 0f) return false;
            var direction = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
            if (direction.sqrMagnitude <= .0001f) return false;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction.normalized), turnSpeed * deltaTime);
            return true;
        }

        private bool TryGetMouseAim(out Vector3 aimPoint)
        {
            aimPoint = default;
            var camera = Camera.main;
            if (camera == null) return false;
            var plane = new Plane(Vector3.up, transform.position);
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out var distance)) return false;
            aimPoint = ray.GetPoint(distance);
            return IsFinite(aimPoint.x) && IsFinite(aimPoint.y) && IsFinite(aimPoint.z);
        }

        public void Move(Vector2 input, float deltaTime)
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null
                || !characterController.enabled
                || !isActiveAndEnabled
                || inputLocked
                || !IsFinite(input.x)
                || !IsFinite(input.y)
                || !IsFinite(deltaTime)
                || deltaTime <= 0f)
            {
                return;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            var direction = new Vector3(input.x, 0f, input.y);
            characterController.Move(direction * (speed * deltaTime));
        }

        public void ReturnToFrontend()
        {
            GameSceneFlow.LoadFrontend();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
