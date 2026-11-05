using System.Linq;
using AlmostDelivered.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay
{
    public sealed class MissionHud : MonoBehaviour
    {
        [SerializeField] private Text controlsText;
        [SerializeField] private Text playerHealthText;
        [SerializeField] private Text bossCountText;
        [SerializeField] private ActorHealth playerHealth;
        [SerializeField] private PrototypeBossController[] bosses;

        public int RemainingBosses => bosses == null ? 0 : bosses.Count(boss => boss != null && !boss.IsDefeated);

        public void BindViews(Text controls, Text health, Text bossCount)
        {
            controlsText = controls;
            playerHealthText = health;
            bossCountText = bossCount;
        }

        public void Configure(ActorHealth player, PrototypeBossController[] missionBosses)
        {
            playerHealth = player;
            bosses = missionBosses;
            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (controlsText != null)
                controlsText.text = "WASD 이동  |  마우스 조준  |  좌클릭 발사  |  ESC 귀환";
            if (playerHealthText != null && playerHealth != null)
                playerHealthText.text = $"PLAYER  {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
            if (bossCountText != null)
                bossCountText.text = $"TARGETS  {RemainingBosses}";
        }
    }
}
