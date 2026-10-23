using Assets.Scripts.Actors.Enemies;
using Assets.Scripts.Saves___Serialization.Progression.Stats;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MegabonkTogether.Helpers
{
    /// <summary>
    /// Shared kills option: kills made by the other players are added to the local run stats
    /// (HUD counter and end of run screen). The persistent stats (MyStats, saved to disk / Steam) are never touched
    /// </summary>
    internal static class SharedKillsHelper
    {
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetRequiredService<ISynchronizationService>();

        /// <summary>
        /// Count a kill made by another player. With a null enemy (death received before the enemy was spawned locally) only the kill counter is updated
        /// </summary>
        public static void CountRemoteKill(Enemy enemy)
        {
            if (!synchronizationService.IsSharedKillsEnabled())
            {
                return;
            }

            RunStats.AddValue(EMyStat.kills, 1);

            if (enemy == null)
            {
                return;
            }

            if (enemy.IsElite())
            {
                RunStats.AddValue(EMyStat.eliteKills, 1);
            }

            if (enemy.IsBoss())
            {
                RunStats.AddValue(EMyStat.bossKills, 1);
            }
        }
    }
}
