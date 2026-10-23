using MemoryPack;
using System.Globalization;

namespace MegabonkTogether.Common.Models
{
    /// <summary>
    /// Per stat enemy multipliers, decided by the host for the whole lobby. 1.0 everywhere is the regular netplay balance.
    /// Travels inside MatchInfo (server copies the host's values to every player)
    /// </summary>
    [MemoryPackable]
    public partial class DifficultySettings
    {
        public float EnemyHp { get; set; } = DifficultyRange.Default;
        public float EnemyDamage { get; set; } = DifficultyRange.Default;
        public float EnemySpeed { get; set; } = DifficultyRange.Default;
        public float EliteChance { get; set; } = DifficultyRange.Default;
        public float EnemyCap { get; set; } = DifficultyRange.Default;

        public float Get(DifficultyStat stat)
        {
            switch (stat)
            {
                case DifficultyStat.EnemyHp: return EnemyHp;
                case DifficultyStat.EnemyDamage: return EnemyDamage;
                case DifficultyStat.EnemySpeed: return EnemySpeed;
                case DifficultyStat.EliteChance: return EliteChance;
                case DifficultyStat.EnemyCap: return EnemyCap;
                default: return DifficultyRange.Default;
            }
        }

        public void Set(DifficultyStat stat, float value)
        {
            switch (stat)
            {
                case DifficultyStat.EnemyHp: EnemyHp = value; break;
                case DifficultyStat.EnemyDamage: EnemyDamage = value; break;
                case DifficultyStat.EnemySpeed: EnemySpeed = value; break;
                case DifficultyStat.EliteChance: EliteChance = value; break;
                case DifficultyStat.EnemyCap: EnemyCap = value; break;
            }
        }

        [MemoryPackIgnore]
        public bool IsDefault
        {
            get
            {
                foreach (var stat in DifficultyRange.Stats)
                {
                    if (Get(stat) != DifficultyRange.Default)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Copy with every value snapped and clamped to its stat's range
        /// </summary>
        public DifficultySettings Normalized()
        {
            var result = new DifficultySettings();
            foreach (var stat in DifficultyRange.Stats)
            {
                result.Set(stat, DifficultyRange.Normalize(stat, Get(stat)));
            }

            return result;
        }

        /// <summary>
        /// "1.3x", invariant culture whatever the system locale
        /// </summary>
        public static string FormatMultiplier(float value)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture) + "x";
        }

        /// <summary>
        /// One line summary for logs and notifications
        /// </summary>
        public string Format()
        {
            if (IsDefault)
            {
                return "default (all 1.0x)";
            }

            return $"HP {FormatMultiplier(EnemyHp)}, Damage {FormatMultiplier(EnemyDamage)}, Speed {FormatMultiplier(EnemySpeed)}, Elites {FormatMultiplier(EliteChance)}, Max enemies {FormatMultiplier(EnemyCap)}";
        }
    }
}
