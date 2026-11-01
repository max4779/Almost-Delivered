using System;
using System.Collections.Generic;
using AlmostDelivered.CommanderAI.Tactics;
using UnityEngine;

namespace AlmostDelivered.CommanderAI.Commander
{
    public enum CommanderDifficulty { Easy, Normal, Hard }

    [CreateAssetMenu(menuName = "Almost Delivered/Commander AI/Difficulty Profile")]
    public sealed class CommanderDifficultyProfile : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float commandCycleSeconds = 3f;
        [SerializeField, Min(0f)] private float telegraphDuration = 1f;
        [SerializeField] private string[] availableTacticIds = { InitialTacticIds.ObjectiveBlockade, InitialTacticIds.Pincer, InitialTacticIds.FocusSupport };
        [SerializeField, Min(0f)] private float minimumHoldTime = 6f;
        [SerializeField, Range(0f, 1f)] private float switchMargin = .1f;
        public float CommandCycleSeconds => commandCycleSeconds;
        public float TelegraphDuration => telegraphDuration;
        public IReadOnlyList<string> AvailableTacticIds => Array.AsReadOnly(availableTacticIds);
        public float MinimumHoldTime => minimumHoldTime;
        public float SwitchMargin => switchMargin;

        public static CommanderDifficultyProfile Create(CommanderDifficulty difficulty)
        {
            if (!Enum.IsDefined(typeof(CommanderDifficulty), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            var profile = CreateInstance<CommanderDifficultyProfile>();
            profile.name = difficulty + " Commander";
            switch (difficulty)
            {
                case CommanderDifficulty.Easy:
                    profile.commandCycleSeconds = 5f;
                    profile.telegraphDuration = 2f;
                    profile.availableTacticIds = new[] { InitialTacticIds.ObjectiveBlockade };
                    profile.minimumHoldTime = 10f;
                    profile.switchMargin = .2f;
                    break;
                case CommanderDifficulty.Hard:
                    profile.commandCycleSeconds = 1.5f;
                    profile.telegraphDuration = .5f;
                    profile.minimumHoldTime = 3f;
                    profile.switchMargin = .05f;
                    break;
            }
            return profile;
        }
    }
}
