using MegabonkTogether.Common.Models;
using MegabonkTogether.Configuration;
using System;
using System.Globalization;

namespace MegabonkTogether.Helpers
{
    /// <summary>
    /// Difficulty lobby option (0.5x - 2.0x, the host decides for everyone). Static and service free on purpose:
    /// GameBalanceService needs it and SynchronizationService depends on GameBalanceService, so it can't go through ISynchronizationService
    /// </summary>
    internal static class DifficultyHelper
    {
        /// <summary>
        /// Slider range in whole steps of 0.1: 5 -> 0.5x, 20 -> 2.0x. Whole numbers on the slider avoid float drift
        /// </summary>
        public const int MinSteps = 5;
        public const int MaxSteps = 20;

        public static float FromSteps(int steps) => DifficultyRange.Normalize(steps / 10f);

        public static int ToSteps(float difficulty) => (int)Math.Round(DifficultyRange.Normalize(difficulty) * 10f);

        /// <summary>
        /// "1.3x", invariant culture whatever the system locale
        /// </summary>
        public static string Format(float difficulty) => ToQueryString(difficulty) + "x";

        /// <summary>
        /// "1.3", what the matchmaking server parses. Never ToString() the float directly: a French locale would send "1,3"
        /// </summary>
        public static string ToQueryString(float difficulty) => DifficultyRange.Normalize(difficulty).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// Effective lobby value. Mirrors SynchronizationService.IsSharedKillsEnabled: the MatchInfo value (host's config, copied by the server)
        /// once known, otherwise a friendlies host alone in its lobby uses its own config, anyone else gets the default
        /// </summary>
        public static float GetMultiplier()
        {
            var mode = Plugin.Instance?.Mode;
            if (mode == null)
            {
                return DifficultyRange.Default;
            }

            if (mode.Difficulty.HasValue)
            {
                return DifficultyRange.Normalize(mode.Difficulty.Value);
            }

            if (mode.Mode == NetworkModeType.Friendlies && mode.Role == Role.Host && ModConfig.Difficulty != null)
            {
                return DifficultyRange.Normalize(ModConfig.Difficulty.Value);
            }

            return DifficultyRange.Default;
        }
    }
}
