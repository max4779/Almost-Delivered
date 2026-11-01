using AlmostDelivered.Combat;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Debugging
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArenaPlayerController : MonoBehaviour
    {
        private CharacterController controller;
        private void Awake() => controller = GetComponent<CharacterController>();
        private void Update()
        {
            if (GetComponent<ActorHealth>().IsDead) return;
            var direction = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            controller.Move((direction.normalized * 8 + Vector3.down * 2) * Time.deltaTime);
            if (Input.GetMouseButtonDown(0) && Camera.main != null &&
                Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out var hit, 200))
                hit.collider.GetComponentInParent<ActorHealth>()?.ApplyDamage(25);
            if (Input.GetKeyDown(KeyCode.Space))
                foreach (var nearby in Physics.OverlapSphere(transform.position, 5))
                    nearby.GetComponentInParent<StunState>()?.Apply(2, Time.unscaledTime);
        }
    }
}
