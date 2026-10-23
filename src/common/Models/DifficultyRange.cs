namespace MegabonkTogether.Common.Models
{
    /// <summary>
    /// The enemy stats the host can scale independently (one slider each)
    /// </summary>
    public enum DifficultyStat
    {
        EnemyHp,
        EnemyDamage,
        EnemySpeed,
        EliteChance,
        EnemyCap
    }

    /// <summary>
    /// Bounds of the netplay difficulty multipliers (host decided lobby option). Shared by the plugin and the server
    /// so the values coming from the query string are clamped the same way on both ends
    /// </summary>
    public static class DifficultyRange
    {
        public const float Min = 0.5f;
        public const float Step = 0.1f;
        public const float Default = 1.0f;

        public static readonly DifficultyStat[] Stats =
        {
            DifficultyStat.EnemyHp,
            DifficultyStat.EnemyDamage,
            DifficultyStat.EnemySpeed,
            DifficultyStat.EliteChance,
            DifficultyStat.EnemyCap
        };

        /// <summary>
        /// Upper bound per stat. HP, damage and elite chance can go high (the game caps the final elite chance at 100% anyway);
        /// speed above 2x outruns every character and the enemy cap is a frame rate matter
        /// </summary>
        public static float Max(DifficultyStat stat)
        {
            switch (stat)
            {
                case DifficultyStat.EnemySpeed:
                case DifficultyStat.EnemyCap:
                    return 2.0f;
                default:
                    return 5.0f;
            }
        }

        /// <summary>
        /// Name of the WebSocket query parameter carrying that stat, used by the plugin to send and by the server to parse
        /// </summary>
        public static string QueryName(DifficultyStat stat)
        {
            switch (stat)
            {
                case DifficultyStat.EnemyHp: return "difficultyHp";
                case DifficultyStat.EnemyDamage: return "difficultyDamage";
                case DifficultyStat.EnemySpeed: return "difficultySpeed";
                case DifficultyStat.EliteChance: return "difficultyElite";
                case DifficultyStat.EnemyCap: return "difficultyCap";
                default: return "difficulty" + stat;
            }
        }

        /// <summary>
        /// Snap to the 0.1 grid and clamp to [Min, Max(stat)]. Non finite values (NaN, infinity) fall back to Default
        /// </summary>
        public static float Normalize(DifficultyStat stat, float value)
        {
            if (!float.IsFinite(value))
            {
                return Default;
            }

            var snapped = MathF.Round(value * 10f) / 10f;
            return Math.Clamp(snapped, Min, Max(stat));
        }
    }
}
