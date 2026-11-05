using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using UnityEngine;

namespace AlmostDelivered.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MissionPlayerController : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float moveSpeed = 6f;
        [SerializeField] private RangedAttackController rangedAttack;

        private CharacterController characterController;
        private ActorHealth health;

        public void Configure(CharacterController controller, RangedAttackController attack, float speed)
        {
            characterController = controller;
            rangedAttack = attack;
            moveSpeed = Mathf.Max(.1f, speed);
            health = GetComponent<ActorHealth>();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<ActorHealth>();
            if (rangedAttack == null)
                rangedAttack = GetComponent<RangedAttackController>();
        }

        private void Update()
        {
            Move(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), Time.deltaTime);

            if (Input.GetMouseButton(0) && TryGetMouseAim(out var aimPoint))
                AimAndFire(aimPoint, Time.time);

            if (Input.GetKeyDown(KeyCode.Escape))
                GameSceneFlow.LoadDepartureHub();
        }

        public bool Move(Vector2 input, float deltaTime)
        {
            if (!CanAct() || characterController == null || !characterController.enabled)
                return false;
            if (!IsFinite(input) || !IsFinite(deltaTime) || deltaTime <= 0f || input.sqrMagnitude < .0001f)
                return false;

            var normalized = input.sqrMagnitude > 1f ? input.normalized : input;
            var direction = new Vector3(normalized.x, 0f, normalized.y);
            characterController.Move(direction * (moveSpeed * deltaTime));
            transform.forward = direction.normalized;
            return true;
        }

        public bool AimAndFire(Vector3 aimPoint, float now)
        {
            return CanAct() && IsFinite(aimPoint) && rangedAttack != null && rangedAttack.TryFire(aimPoint, now);
        }

        private bool TryGetMouseAim(out Vector3 aimPoint)
        {
            aimPoint = default;
            var camera = Camera.main;
            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out var distance))
                return false;

            aimPoint = ray.GetPoint(distance);
            return IsFinite(aimPoint);
        }

        private bool CanAct() => enabled && (health == null || !health.IsDead);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
