using UnityEngine;

namespace AlmostDelivered.CommanderAI.Commander
{
    public sealed class CommandTelegraph : MonoBehaviour
    {
        public bool IsVisible { get; private set; }
        public string TacticId { get; private set; }
        public float CompletesAt { get; private set; }

        public void Begin(string tacticId, float now, float duration)
        { TacticId = tacticId; CompletesAt = now + duration; IsVisible = true; }
        public bool IsComplete(float now) => IsVisible && now >= CompletesAt;
        public void Cancel() { IsVisible = false; TacticId = null; }
        private void OnDisable() => Cancel();

        private void OnGUI()
        {
            if (!IsVisible) return;
            var camera = Camera.main;
            var position = camera == null ? new Vector3(20, Screen.height - 60, 1) : camera.WorldToScreenPoint(transform.position + Vector3.up * 2);
            if (position.z > 0) GUI.Box(new Rect(position.x - 100, Screen.height - position.y, 260, 30), "Commander preparing: " + TacticId);
        }
    }
}
