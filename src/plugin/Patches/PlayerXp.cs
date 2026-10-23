using HarmonyLib;
using Inventory__Items__Pickups.Xp_and_Levels;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MegabonkTogether.Patches
{
    [HarmonyPatch(typeof(PlayerXp))]
    internal static class PlayerXpPatches
    {
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetService<ISynchronizationService>();

        /// <summary>
        /// Remember the xp before the game adds it, to know how much was actually gained (after xp multipliers)
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerXp.AddXp))]
        public static void AddXp_Prefix(PlayerXp __instance, out int __state)
        {
            __state = __instance.xp;
        }

        /// <summary>
        /// Synchronize XP gains in shared experience mode.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerXp.AddXp))]
        public static void AddXp_Postfix(PlayerXp __instance, int __state)
        {
            if (!synchronizationService.IsNetplaySessionActive()) //Also while teleporting to the next stage: xp gained then must still be shared
            {
                return;
            }

            if (!synchronizationService.IsSharedExperienceEnabled())
            {
                return;
            }

            if (!Plugin.CAN_SEND_MESSAGES)
            {
                return;
            }

            var localPlayerXp = GameManager.Instance?.player?.inventory?.playerXp;
            if (localPlayerXp == null || __instance.Pointer != localPlayerXp.Pointer)
            {
                return; //Only the local player's xp is shared
            }

            var gainedXp = __instance.xp - __state;
            if (gainedXp <= 0)
            {
                return;
            }

            synchronizationService.PlayerXpAddXp(__instance.xp, gainedXp, __instance.leftOverXp);
        }

    }
}
