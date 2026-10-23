namespace MegabonkTogether.Common.Models
{
    /// <summary>
    /// Bounds of the netplay difficulty multiplier (host decided lobby option). Shared by the plugin and the server
    /// so the value coming from the query string is clamped the same way on both ends
    /// </summary>
    public static class DifficultyRange
    {
        public const float Min = 0.5f;
        public const float Max = 2.0f;
        public const float Step = 0.1f;
        public const float Default = 1.0f;

        /// <summary>
        /// Snap to the 0.1 grid and clamp to [Min, Max]. Non finite values (NaN, infinity) fall back to Default
        /// </summary>
        public static float Normalize(float value)
        {
            if (!float.IsFinite(value))
            {
                return Default;
            }

            var snapped = MathF.Round(value * 10f) / 10f;
            return Math.Clamp(snapped, Min, Max);
        }
    }
}
