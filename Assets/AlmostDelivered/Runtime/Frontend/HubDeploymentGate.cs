using UnityEngine;

namespace AlmostDelivered.Frontend
{
    [RequireComponent(typeof(Collider))]
    public sealed class HubDeploymentGate : MonoBehaviour
    {
        [SerializeField] private GameObject prompt;

        private Collider gateTrigger;
        private HubPlayerController occupant;
        private Collider occupantCollider;

        public bool CanLaunch => occupant != null;

        public void Configure(GameObject promptObject)
        {
            prompt = promptObject;
            SetPrompt(false);
        }

        private void Awake()
        {
            gateTrigger = GetComponent<Collider>();
            gateTrigger.isTrigger = true;
            SetPrompt(false);
        }

        private void FixedUpdate()
        {
            if (occupant == null)
            {
                return;
            }

            if (occupantCollider == null || !gateTrigger.bounds.Intersects(occupantCollider.bounds))
            {
                ClearOccupant();
            }
        }

        private void Update()
        {
            if (CanLaunch && Input.GetKeyDown(KeyCode.E))
            {
                RequestLaunch();
            }
        }

        public void RequestLaunch()
        {
            if (CanLaunch)
            {
                GameSceneFlow.LoadMissionPrototype();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<HubPlayerController>();
            if (player == null)
            {
                return;
            }

            occupant = player;
            occupantCollider = other;
            SetPrompt(true);
        }

        private void OnTriggerExit(Collider other)
        {
            var player = other.GetComponentInParent<HubPlayerController>();
            if (player == null || player != occupant)
            {
                return;
            }

            ClearOccupant();
        }

        private void ClearOccupant()
        {
            occupant = null;
            occupantCollider = null;
            SetPrompt(false);
        }

        private void SetPrompt(bool visible)
        {
            if (prompt != null)
            {
                prompt.SetActive(visible);
            }
        }
    }
}
