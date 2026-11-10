using System;
using UnityEngine;

namespace AlmostDelivered.Gameplay.Payload
{
    [Serializable]
    public sealed class RelayRouteOptionDefinition
    {
        public string id;
        public string title;
        [TextArea] public string benefit;
        [TextArea] public string drawback;
        public Color color = new Color(.1f, .32f, .2f);
        [Min(.01f)] public float escortSpeedMultiplier = 1f;
        [Min(0f)] public float cargoRepairPerSecond;
        [Min(0f)] public float pulseDamage;
        [Min(.1f)] public float pulseInterval = 3f;
        [Min(0f)] public float pulseRadius = 6f;
        [Min(0)] public int extraTravelEnemies;
        public bool forceCargoSaboteur;
        [Min(.01f)] public float enemyHealthMultiplier = 1f;

        public static RelayRouteOptionDefinition[] CreateDefaults()
        {
            return new[]
            {
                new RelayRouteOptionDefinition
                {
                    id = "auto-repair", title = "자동 수리",
                    benefit = "호위 중 화물 체력 초당 +5",
                    drawback = "매 습격에 화물 파괴 적 합류",
                    color = new Color(.1f, .32f, .2f),
                    cargoRepairPerSecond = 5f,
                    forceCargoSaboteur = true
                },
                new RelayRouteOptionDefinition
                {
                    id = "overdrive", title = "급속 운송",
                    benefit = "화물 속도 +50%",
                    drawback = "이동 중 적 무리 2명 추가",
                    color = new Color(.08f, .27f, .39f),
                    escortSpeedMultiplier = 1.5f,
                    extraTravelEnemies = 2
                },
                new RelayRouteOptionDefinition
                {
                    id = "shock-grid", title = "방어 전류",
                    benefit = "3초마다 주변 적 피해 12",
                    drawback = "이후 생성되는 적 체력 +25%",
                    color = new Color(.29f, .16f, .37f),
                    pulseDamage = 12f,
                    pulseInterval = 3f,
                    pulseRadius = 6f,
                    enemyHealthMultiplier = 1.25f
                }
            };
        }
    }
}
