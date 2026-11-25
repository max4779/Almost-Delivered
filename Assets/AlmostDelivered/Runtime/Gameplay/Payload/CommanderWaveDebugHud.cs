using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AlmostDelivered.Gameplay.Payload
{
    /// <summary>Development-only view of the commander plans that actually entered the wave spawner.</summary>
    public sealed class CommanderWaveDebugHud : MonoBehaviour
    {
        [SerializeField] private PayloadWaveSpawner waves;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text details;
        private CommanderWaveDebugSnapshot lastSnapshot;
        private bool available;

        public void Configure(PayloadWaveSpawner waveSpawner, GameObject debugPanel, Text detailText)
        {
            waves = waveSpawner;
            panel = debugPanel;
            details = detailText;
            Refresh();
        }

        private void Start()
        {
            available = Application.isEditor || Debug.isDebugBuild;
            if (panel != null) panel.SetActive(available);
            Refresh();
        }

        private void Update()
        {
            if (!available || panel == null) return;
            if (Input.GetKeyDown(KeyCode.F8)) panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf && waves != null && lastSnapshot != waves.LastCommanderWave)
                Refresh();
        }

        public void Refresh()
        {
            if (details == null) return;
            lastSnapshot = waves != null ? waves.LastCommanderWave : null;
            if (waves == null || waves.RecentCommanderWaves.Count == 0)
            {
                details.text = "최초 화물 경비병: 랜덤\n기동 후 지휘관 편성이 여기에 표시됩니다.";
                return;
            }

            var result = new StringBuilder();
            var history = waves.RecentCommanderWaves;
            for (var index = history.Count - 1; index >= 0; index--)
            {
                if (result.Length > 0) result.AppendLine();
                var wave = history[index];
                result.Append(KindName(wave.Kind)).Append(" #").Append(wave.SequenceId);
                result.Append(wave.Kind == CommanderWaveKind.Random
                    ? "  랜덤/예산 외" : $"  토큰 {wave.Spent}/{wave.Budget}");
                result.AppendLine();
                if (wave.Kind == CommanderWaveKind.Recon)
                    result.Append("정찰병 ×").Append(wave.ScoutCount);
                else
                    AppendComposition(result, wave.Units);
            }
            details.text = result.ToString();
        }

        private static string KindName(CommanderWaveKind kind)
        {
            switch (kind)
            {
                case CommanderWaveKind.Travel: return "이동 증원";
                case CommanderWaveKind.Checkpoint: return "거점 웨이브";
                case CommanderWaveKind.Recon: return "정찰 파견";
                default: return "지휘관 부재";
            }
        }

        private static void AppendComposition(StringBuilder result,
            IReadOnlyList<MissionMobArchetype> units)
        {
            if (units == null || units.Count == 0)
            {
                result.Append("병력 없음");
                return;
            }
            var counts = new Dictionary<MissionMobArchetype, int>();
            foreach (var type in units)
            {
                counts.TryGetValue(type, out var count);
                counts[type] = count + 1;
            }
            foreach (var type in new[] { MissionMobArchetype.Melee, MissionMobArchetype.Ranged,
                         MissionMobArchetype.Shield, MissionMobArchetype.CargoSaboteur,
                         MissionMobArchetype.ChargerBoss, MissionMobArchetype.CommanderBoss })
            {
                if (!counts.TryGetValue(type, out var count)) continue;
                if (result[result.Length - 1] != '\n') result.Append("  ");
                result.Append(TypeName(type)).Append('×').Append(count);
            }
        }

        private static string TypeName(MissionMobArchetype type)
        {
            switch (type)
            {
                case MissionMobArchetype.Ranged: return "원거리";
                case MissionMobArchetype.Shield: return "방패";
                case MissionMobArchetype.CargoSaboteur: return "화물";
                case MissionMobArchetype.ChargerBoss: return "돌진";
                case MissionMobArchetype.CommanderBoss: return "지휘관";
                default: return "근접";
            }
        }
    }
}
