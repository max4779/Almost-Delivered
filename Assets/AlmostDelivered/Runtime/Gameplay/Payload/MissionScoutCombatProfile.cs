using AlmostDelivered.Frontend;

namespace AlmostDelivered.Gameplay.Payload
{
    public sealed class MissionScoutCombatProfile
    {
        private float lastShotTime = float.NegativeInfinity;
        private PlayerWeaponType lastShotWeapon;
        private bool wasSprinting;
        private bool wasRolling;

        public MissionScoutCombatProfile(int actorId) => ActorId = actorId;

        public int ActorId { get; }
        public bool HasObservedWeapon { get; private set; }
        public PlayerWeaponType LastObservedWeapon { get; private set; }
        public int WeaponSwitches { get; private set; }
        public int ShotsObserved { get; private set; }
        public int ReloadsObserved { get; private set; }
        public int MeleeAttacksObserved { get; private set; }
        public int SprintEpisodesObserved { get; private set; }
        public int ObservedRolls { get; private set; }
        public int RapidShotPairs { get; private set; }
        public float LastObservedAt { get; private set; }
        public bool IsRapidFire => RapidShotPairs >= 2;
        public bool PrefersMelee => MeleeAttacksObserved >= 2 &&
                                    MeleeAttacksObserved >= ShotsObserved;
        public string WeaponLabel => !HasObservedWeapon ? "UNKNOWN" :
            LastObservedWeapon == PlayerWeaponType.Melee ? "MELEE" :
            LastObservedWeapon == PlayerWeaponType.Revolver ? "REVOLVER" : "RIFLE";

        public void ObserveStatus(PlayerWeaponType weapon, bool sprinting, bool rolling, float now)
        {
            if (now - LastObservedAt > .5f)
            {
                wasSprinting = false;
                wasRolling = false;
            }
            if (HasObservedWeapon && LastObservedWeapon != weapon) WeaponSwitches++;
            HasObservedWeapon = true;
            LastObservedWeapon = weapon;
            if (sprinting && !wasSprinting) SprintEpisodesObserved++;
            if (rolling && !wasRolling) ObservedRolls++;
            wasSprinting = sprinting;
            wasRolling = rolling;
            LastObservedAt = now;
        }

        public void ObserveShot(float now)
        {
            if (ShotsObserved > 0 && lastShotWeapon == LastObservedWeapon &&
                now >= lastShotTime && now - lastShotTime <= .45f)
                RapidShotPairs++;
            ShotsObserved++;
            lastShotTime = now;
            lastShotWeapon = LastObservedWeapon;
            LastObservedAt = now;
        }

        public void ObserveReload(float now)
        {
            ReloadsObserved++;
            LastObservedAt = now;
        }

        public void ObserveMelee(float now)
        {
            MeleeAttacksObserved++;
            LastObservedAt = now;
        }
    }
}
