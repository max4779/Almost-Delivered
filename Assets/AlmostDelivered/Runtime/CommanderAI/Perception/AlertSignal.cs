using System;
using AlmostDelivered.Combat;
using AlmostDelivered.CommanderAI.Commander;
using AlmostDelivered.CommanderAI.Domain;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Perception
{
    [DefaultExecutionOrder(32000)]
    [RequireComponent(typeof(ActorHealth), typeof(StunState), typeof(MonsterPerception))]
    public sealed class AlertSignal : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float duration = 1f;
        private SquadController squad;
        private int reporterId;
        private ActorHealth health;
        private StunState stun;
        private MonsterPerception perception;
        private ObservableActor target;
        private float completesAt;
        private int dueFrame = -1;
        private bool signaling;
        [SerializeField] private TextMesh cue;
        private float cueExpiresAt;

        public bool IsSignaling => signaling;

        private void Awake()
        {
            health = GetComponent<ActorHealth>();
            stun = GetComponent<StunState>();
            perception = GetComponent<MonsterPerception>();
            EnsureCue();
        }

        private void OnEnable()
        {
            health.Died += Cancel;
            stun.StunChanged += OnStunChanged;
        }

        private void OnDisable()
        {
            Cancel();
            if (cue != null) cue.gameObject.SetActive(false);
            health.Died -= Cancel;
            stun.StunChanged -= OnStunChanged;
        }

        public void Configure(SquadController controller, int memberId, float alertDuration)
        {
            if (float.IsNaN(alertDuration) || float.IsInfinity(alertDuration) || alertDuration < 0f)
                throw new ArgumentOutOfRangeException(nameof(alertDuration));
            Cancel();
            squad = controller;
            reporterId = memberId;
            duration = alertDuration;
        }

        public void Begin(ObservableActor observedTarget)
        {
            if (IsSignaling || !CanSignal() || !perception.CanSee(observedTarget))
                return;
            target = observedTarget;
            signaling = true;
            completesAt = Time.unscaledTime + duration;
            dueFrame = -1;
            ShowCue("Alerting 0%", Color.yellow);
        }

        public void Cancel()
        {
            if (signaling) ShowCue("Alert cancelled", new Color(1f, .35f, .25f));
            signaling = false;
            target = null;
            dueFrame = -1;
        }

        // Also called by the arena builder so the cue is part of the saved scene wiring.
        public void EnsureCue()
        {
            if (cue != null) return;
            var label = new GameObject("Alert cue"); label.transform.SetParent(transform, false);
            label.transform.localPosition = Vector3.up * 2.4f;
            cue = label.AddComponent<TextMesh>(); cue.anchor = TextAnchor.MiddleCenter;
            cue.alignment = TextAlignment.Center; cue.fontSize = 64; cue.characterSize = .08f;
            cue.fontStyle = FontStyle.Bold;
            label.SetActive(false);
        }

        private void ShowCue(string text, Color color)
        {
            EnsureCue(); cue.text = text; cue.color = color;
            cue.gameObject.SetActive(isActiveAndEnabled);
            cueExpiresAt = Time.unscaledTime + .45f;
        }

        private void OnStunChanged(bool isStunned)
        {
            if (isStunned)
                Cancel();
        }

        private bool CanSignal() => isActiveAndEnabled && squad != null &&
            squad.State == SquadAlertState.Unaware && squad.IsLivingMember(reporterId) &&
            !health.IsDead && !stun.IsStunned && perception.isActiveAndEnabled;

        private void LateUpdate()
        {
            if (cue != null && cue.gameObject.activeSelf)
            {
                var camera = Camera.main;
                if (camera != null) cue.transform.rotation = camera.transform.rotation;
                if (!signaling && Time.unscaledTime >= cueExpiresAt) cue.gameObject.SetActive(false);
            }
            if (!IsSignaling)
                return;
            if (!CanSignal() || !perception.CanSee(target))
            {
                Cancel();
                return;
            }
            ShowCue("Alerting " + Mathf.Clamp01(duration <= 0 ? 1 : 1 - (completesAt - Time.unscaledTime) / duration).ToString("P0"), Color.yellow);
            if (Time.unscaledTime < completesAt)
                return;

            // Leave the whole due frame interruptible, including work after LateUpdate.
            // Revalidate above on the following frame before publishing anything irreversible.
            if (dueFrame < 0)
            {
                dueFrame = Time.frameCount;
                return;
            }
            if (Time.frameCount <= dueFrame)
                return;

            var observedTarget = target;
            signaling = false; target = null; dueFrame = -1;
            if (squad.TryCompleteAlert(reporterId))
            {
                ShowCue("Squad alerted", Color.green);
                perception.Observe(observedTarget);
            }
            else ShowCue("Alert cancelled", new Color(1f, .35f, .25f));
        }
    }
}
