using AlmostDelivered.Combat;
using AlmostDelivered.Frontend;
using UnityEngine;

namespace AlmostDelivered.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerMobility))]
    public sealed class MissionPlayerController : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float turnSpeed = 540f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float groundedVerticalSpeed = -2f;
        [SerializeField] private RangedAttackController rangedAttack;
        [SerializeField] private MeleeAttackController meleeAttack;
        [SerializeField] private PlayerWeaponType weaponType;

        private CharacterController characterController;
        private ActorHealth health;
        private PlayerMobility mobility;
        private float verticalVelocity;

        public PlayerWeaponType WeaponType => weaponType;

        public void Configure(CharacterController controller, RangedAttackController attack, float speed)
        {
            characterController = controller;
            rangedAttack = attack;
            moveSpeed = Mathf.Max(.1f, speed);
            health = GetComponent<ActorHealth>();
            mobility = GetComponent<PlayerMobility>();
            verticalVelocity = 0f;
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            health = GetComponent<ActorHealth>();
            mobility = GetComponent<PlayerMobility>();
            if (rangedAttack == null)
                rangedAttack = GetComponent<RangedAttackController>();
            if (meleeAttack == null)
                meleeAttack = GetComponent<MeleeAttackController>();
        }

        private void Start() => SetWeaponType(PlayerWeaponLoadout.Selected);

        public void ConfigureMelee(MeleeAttackController attack)
        {
            meleeAttack = attack;
        }

        public void SetWeaponType(PlayerWeaponType selected)
        {
            weaponType = selected;
            if (rangedAttack != null)
            {
                rangedAttack.enabled = PlayerWeaponLoadout.IsFirearm(selected);
                PlayerWeaponLoadout.ConfigureFirearm(rangedAttack, selected);
            }
            if (meleeAttack != null)
            {
                PlayerWeaponLoadout.ConfigureMelee(meleeAttack);
                meleeAttack.enabled = selected == PlayerWeaponType.Melee;
            }
            PlayerWeaponLoadout.ApplyVisual(gameObject, selected);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R)) Reload(Time.time);
            MoveWithActions(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Time.deltaTime, PlayerInputBindings.IsHeld(PlayerAction.Sprint),
                PlayerInputBindings.WasPressed(PlayerAction.Roll));

            if (TryGetMouseAim(out var aimPoint))
            {
                if (Input.GetMouseButton(0))
                    AimAndFire(aimPoint, Time.time, Time.deltaTime);
                else
                    AimAt(aimPoint, Time.deltaTime);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
                GameSceneFlow.LoadDepartureHub();
        }

        public bool Reload(float now) => CanAct() && rangedAttack != null && rangedAttack.TryReload(now);

        public bool Move(Vector2 input, float deltaTime) => MoveWithActions(input, deltaTime, false, false);

        public bool MoveWithActions(Vector2 input, float deltaTime, bool sprintHeld, bool rollPressed)
        {
            if (!CanAct() || characterController == null || !characterController.enabled)
                return false;
            if (!IsFinite(input) || !IsFinite(deltaTime) || deltaTime <= 0f)
                return false;

            var hasMovementInput = input.sqrMagnitude >= .0001f;
            var normalized = hasMovementInput && input.sqrMagnitude > 1f ? input.normalized : input;
            var direction = new Vector3(normalized.x, 0f, normalized.y);
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = groundedVerticalSpeed;
            else
                verticalVelocity += gravity * deltaTime;

            var planarVelocity = mobility != null
                ? mobility.Step(normalized, sprintHeld, rollPressed, deltaTime, moveSpeed)
                : direction * moveSpeed;
            var velocity = planarVelocity + Vector3.up * verticalVelocity;
            var collisionFlags = characterController.Move(velocity * deltaTime);
            if ((collisionFlags & CollisionFlags.Below) != 0)
                verticalVelocity = groundedVerticalSpeed;

            return planarVelocity.sqrMagnitude > .0001f;
        }

        public bool AimAt(Vector3 aimPoint, float deltaTime)
        {
            if (!CanAct() || !IsFinite(aimPoint) || !IsFinite(deltaTime) || deltaTime <= 0f)
                return false;

            RotateTowards(aimPoint - transform.position, deltaTime);
            return true;
        }

        public bool AimAndFire(Vector3 aimPoint, float now)
        {
            return AimAndFire(aimPoint, now, Time.deltaTime);
        }

        public bool AimAndFire(Vector3 aimPoint, float now, float deltaTime)
        {
            if (!CanAct() || !IsFinite(aimPoint) || (mobility != null && mobility.IsRolling))
                return false;

            AimAt(aimPoint, deltaTime);
            if (weaponType == PlayerWeaponType.Melee)
                return meleeAttack != null && meleeAttack.TryAttack(now, aimPoint - transform.position);
            return rangedAttack != null && rangedAttack.TryFire(aimPoint, now);
        }

        private bool TryGetMouseAim(out Vector3 aimPoint)
        {
            aimPoint = default;
            var camera = Camera.main;
            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var targetHealth = hit.collider.GetComponentInParent<ActorHealth>();
                var targetTeam = hit.collider.GetComponentInParent<CombatTeam>();
                var ownTeam = GetComponent<CombatTeam>();
                if (targetHealth != null && !targetHealth.IsDead && targetTeam != null &&
                    (ownTeam == null || targetTeam.Team != ownTeam.Team))
                {
                    aimPoint = hit.collider.bounds.center;
                    return IsFinite(aimPoint);
                }
            }

            var plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out var distance))
                return false;

            aimPoint = ray.GetPoint(distance);
            return IsFinite(aimPoint);
        }

        private void RotateTowards(Vector3 direction, float deltaTime)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude <= .0001f || !IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * deltaTime);
        }

        private bool CanAct() => enabled && (health == null || !health.IsDead);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
