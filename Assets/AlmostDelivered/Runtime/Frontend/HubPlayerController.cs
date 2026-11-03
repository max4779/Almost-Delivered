using UnityEngine;

namespace AlmostDelivered.Frontend
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class HubPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 6f;

        private CharacterController characterController;

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GameSceneFlow.LoadFrontend();
                return;
            }

            Move(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Time.deltaTime);
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

            if (direction.sqrMagnitude > .0001f)
            {
                transform.forward = direction;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
