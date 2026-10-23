using MegabonkTogether.Common.Models;
using MegabonkTogether.Configuration;
using System;
using System.Globalization;
using System.Text;

namespace MegabonkTogether.Helpers
{
    /// <summary>
    /// Difficulty lobby option: one multiplier per enemy stat, the host decides for everyone. Static and service free on purpose:
    /// GameBalanceService needs it and SynchronizationService depends on GameBalanceService, so it can't go through ISynchronizationService
    /// </summary>
    internal static class DifficultyHelper
    {
        /// <summary>
        /// Slider ranges in whole steps of 0.1: 5 -> 0.5x, 20 -> 2.0x, 50 -> 5.0x. Whole numbers on the slider avoid float drift
        /// </summary>
        public const int MinSteps = 5;

        private static readonly DifficultySettings defaults = new();

        public static int MaxSteps(DifficultyStat stat) => (int)Math.Round(DifficultyRange.Max(stat) * 10f);

        public static float FromSteps(DifficultyStat stat, int steps) => DifficultyRange.Normalize(stat, steps / 10f);

        public static int ToSteps(DifficultyStat stat, float value) => (int)Math.Round(DifficultyRange.Normalize(stat, value) * 10f);

        /// <summary>
        /// "1.3x", invariant culture whatever the system locale
        /// </summary>
        public static string Format(float value) => DifficultySettings.FormatMultiplier(value);

        public static string Label(DifficultyStat stat)
        {
            switch (stat)
            {
                case DifficultyStat.EnemyHp: return "Enemy HP";
                case DifficultyStat.EnemyDamage: return "Enemy damage";
                case DifficultyStat.EnemySpeed: return "Enemy speed";
                case DifficultyStat.EliteChance: return "Elite chance";
                case DifficultyStat.EnemyCap: return "Max enemies";
                default: return stat.ToString();
            }
        }

        /// <summary>
        /// The local config values, normalized. What a host sends to the server
        /// </summary>
        public static DifficultySettings FromConfig()
        {
            var settings = new DifficultySettings();
            if (ModConfig.Difficulty == null)
            {
                return settings;
            }

            foreach (var stat in DifficultyRange.Stats)
            {
                if (ModConfig.Difficulty.TryGetValue(stat, out var entry))
                {
                    settings.Set(stat, DifficultyRange.Normalize(stat, entry.Value));
                }
            }

            return settings;
        }

        /// <summary>
        /// "&difficultyHp=1.0&difficultyDamage=1.5..." for the WebSocket connect. Never ToString() the floats directly: a French locale would send "1,5"
        /// </summary>
        public static string QueryString()
        {
            var settings = FromConfig();
            var builder = new StringBuilder();
            foreach (var stat in DifficultyRange.Stats)
            {
                builder.Append('&')
                    .Append(DifficultyRange.QueryName(stat))
                    .Append('=')
                    .Append(settings.Get(stat).ToString("0.0", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Effective lobby values. Mirrors SynchronizationService.IsSharedKillsEnabled: the MatchInfo values (host's config, copied by the server)
        /// once known, otherwise a friendlies host alone in its lobby uses its own config, anyone else gets the defaults. Read only
        /// </summary>
        public static DifficultySettings GetSettings()
        {
            var mode = Plugin.Instance?.Mode;
            if (mode == null)
            {
                return defaults;
            }

            if (mode.Difficulty != null)
            {
                return mode.Difficulty;
            }

            if (mode.Mode == NetworkModeType.Friendlies && mode.Role == Role.Host)
            {
                return FromConfig();
            }

            return defaults;
        }

        /// <summary>
        /// Two short lines for the friendlies info overlay (no word wrapping there)
        /// </summary>
        public static string FormatForOverlay(DifficultySettings settings)
        {
            if (settings.IsDefault)
            {
                return "Difficulty: default";
            }

            return $"Difficulty: HP {Format(settings.EnemyHp)}, Dmg {Format(settings.EnemyDamage)}, Spd {Format(settings.EnemySpeed)}\n  Elites {Format(settings.EliteChance)}, Max enemies {Format(settings.EnemyCap)}";
        }
    }
}
