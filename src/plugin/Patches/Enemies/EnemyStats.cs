using Assets.Scripts.Actors.Enemies;
using Assets.Scripts.Inventory.Stats;
using HarmonyLib;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MegabonkTogether.Patches.Enemies
{
    [HarmonyPatch(typeof(EnemyStats))]
    internal static class EnemyStatsPatches
    {
        private static readonly IGameBalanceService gameBalanceService = Plugin.Services.GetService<IGameBalanceService>();
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetService<ISynchronizationService>();

        /// <summary>
        /// Adjust enemy HP based on netplay configuration
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyStats.GetHp))]
        private static void GetHpPostfix(Enemy enemy, ref float __result)
        {
            if (!synchronizationService.HasNetplaySessionInitialized())
            {
                return;
            }

            var isServer = synchronizationService.IsServerMode() ?? false;
            if (!isServer)
            {
                return;
            }

            var multiplier = gameBalanceService.GetEnemyHpMultiplier(enemy.enemyFlag);
            __result *= multiplier;
        }

        /// <summary>
        /// Enemies hit harder with more players. Runs on every peer: each client computes the damage its own player takes
        /// (DamageUtility.GetPlayerDamage, enemy special attacks), and the multiplier only depends on the player count at stage start
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyStats.GetDamage))]
        private static void GetDamagePostfix(ref float __result)
        {
            if (!synchronizationService.HasNetplaySessionInitialized())
            {
                return;
            }

            __result *= gameBalanceService.GetEnemyDamageMultiplier();
        }

        /// <summary>
        /// Enemies move slightly faster with more players. Same deterministic input on every peer, so puppets and host agree
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyStats.GetSpeed))]
        private static void GetSpeedPostfix(ref float __result)
        {
            if (!synchronizationService.HasNetplaySessionInitialized())
            {
                return;
            }

            __result *= gameBalanceService.GetEnemySpeedMultiplier();
        }

        /// <summary>
        /// More elites with more players (the host decides the elite status at spawn and sends it)
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyStats.GetEliteChance))]
        private static void GetEliteChancePostfix(ref float __result)
        {
            if (!synchronizationService.HasNetplaySessionInitialized())
            {
                return;
            }

            __result = System.Math.Min(1f, __result * gameBalanceService.GetEliteChanceMultiplier());
        }
    }
}
