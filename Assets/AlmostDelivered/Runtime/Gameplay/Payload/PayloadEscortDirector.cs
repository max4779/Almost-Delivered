using System;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class PayloadEscortDirector
    {
        private const float InitialMinimumDelay = 5f;
        private const float InitialMaximumDelay = 8f;
        private const float PressureMinimumDelay = 12f;
        private const float PressureMaximumDelay = 20f;

        private readonly Random random;
        private readonly int pressureCap;
        private bool hasEnteredEscort;
        private float remainingDelay;
        private int nextSequenceId;

        public PayloadEscortDirector(int seed, int pressureCap = 12)
        {
            random = new Random(seed);
            this.pressureCap = Math.Max(1, pressureCap);
        }

        public bool Tick(
            float deltaTime,
            PayloadMissionPhase phase,
            int activeEnemyCount,
            out PayloadEscortEncounter encounter)
        {
            encounter = default;
            if (!IsFinite(deltaTime) || deltaTime < 0f)
            {
                return false;
            }

            if (phase != PayloadMissionPhase.Escorting)
            {
                return false;
            }

            if (!hasEnteredEscort)
            {
                hasEnteredEscort = true;
                remainingDelay = NextDelay(InitialMinimumDelay, InitialMaximumDelay);
                return false;
            }

            remainingDelay -= deltaTime;
            if (remainingDelay > 0f)
            {
                return false;
            }

            var remainingCapacity = pressureCap - Math.Max(0, activeEnemyCount);
            if (remainingCapacity <= 0)
            {
                return false;
            }

            var requestedSize = random.Next(3, 7);
            encounter = new PayloadEscortEncounter(
                nextSequenceId,
                Math.Min(requestedSize, remainingCapacity),
                (PayloadAssaultPattern)(nextSequenceId % 4));
            nextSequenceId++;
            remainingDelay = NextDelay(PressureMinimumDelay, PressureMaximumDelay);
            return true;
        }

        private float NextDelay(float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
