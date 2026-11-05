using HarmonyLib;
using MegabonkTogether.Configuration;
using MegabonkTogether.Helpers;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace MegabonkTogether.Patches.Unity
{
    [HarmonyPatch(typeof(UnityEngine.UI.Button))]
    internal static class UnityButtonPatches
    {
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetService<ISynchronizationService>();
        private static float confirmLockedUntil = 0f; //Unscaled: the game clock can be paused while a window is open

        /// <summary>
        /// A window opening mid-run (level up, chest, shrine...) selects one of its buttons right away, and the confirm key is also the jump key
        /// (Space, A on a gamepad): a player chaining jumps picks a reward before even seeing the window.
        /// The confirm key is ignored for a short time after a window opens. The pause menu is left alone: the player opened it on purpose
        /// </summary>
        public static void OnWindowOpened()
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            var delay = ModConfig.WindowConfirmDelay.Value;
            if (delay <= 0f)
            {
                return;
            }

            if (PauseMenuHelper.IsPauseMenuOpen())
            {
                return;
            }

            confirmLockedUntil = Time.unscaledTime + delay;
        }

        /// <summary>
        /// OnSubmit is only the keyboard / gamepad confirm of the selected button.
        /// Mouse clicks go through OnPointerClick and number keys through UpgradePicker.KeyboardInput: neither is held back
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(UnityEngine.UI.Button.OnSubmit))]
        public static bool OnSubmit_Prefix()
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return true;
            }

            return Time.unscaledTime >= confirmLockedUntil;
        }
    }
}
