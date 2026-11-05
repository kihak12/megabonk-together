using Assets.Scripts.UI.InGame.Levelup;
using Assets.Scripts.UI.InGame.Rewards;
using Assets.Scripts.Utility;
using Coffee.UIExtensions;
using HarmonyLib;
using MegabonkTogether.Helpers;
using MegabonkTogether.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using UnityEngine;

namespace MegabonkTogether.Patches
{
    [HarmonyPatch(typeof(EncounterWindows))]
    internal static class EncounterWindowPatches
    {
        private static readonly ISynchronizationService synchronizationService = Plugin.Services.GetService<ISynchronizationService>();
        private static readonly IEncounterService encounterService = Plugin.Services.GetService<IEncounterService>();
        private static readonly List<UIParticleRenderer> hiddenParticles = []; //Hidden while waiting, shown again once the encounter is closed
        private static bool isDeferredByPauseMenu = false; //A reward is held back until the pause menu is closed (only used to log it once)

        /// <summary>
        /// When changing level, the game will try to pop reward from previous stage missed
        /// This can freeze the game as the queue will get modified while iterating
        /// To prevent that, we just make sure the player can move before popping the reward
        /// Also if shared experience, notify end of reward to not block the game for other players
        /// Same idea while the pause menu is open: the game never pauses in netplay, and a reward window popping behind the menu
        /// takes the focus away from it, which locks the menu. The reward stays queued until the menu is closed
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EncounterWindows.PopReward))]
        public static bool PopReward_Prefix()
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return true;
            }

            if (!GameManager.Instance.player.playerInput.CanInput())
            {
                //Plugin.Log.LogWarning($"Player can't move yet, skipping reward pop for now");
                return false;
            }

            if (GameManager.Instance.player.IsDead())
            {
                if (synchronizationService.IsSharedExperienceEnabled() && !synchronizationService.ConsumeLocalOnlyEncounter())
                {
                    MyTime.Pause();
                    if (!synchronizationService.RewardFinished())
                    {
                        ScreenTextHelper.Show("Waiting for other player(s) choices...", new Vector2(0, -350));
                    }
                }
                return false;
            }

            if (PauseMenuHelper.IsPauseMenuOpen())
            {
                NoteDeferredByPauseMenu();
                return false; //Still queued: popped from LateUpdate once the menu is closed
            }

            return true;
        }

        /// <summary>
        /// Prevent adding an encounter if one is already in progress
        /// This should not only prevent missing some reward but hopefully also random crashes happening with encounter
        /// The encounter will be queued and popped later at LateUpdate
        /// Also if shared experience, notify end of reward to not block the game for other players
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EncounterWindows.AddEncounter))]
        public static bool AddEncounter_Prefix(EncounterWindows __instance, EEncounter rewardWindowType)
        {

            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return true;
            }

            if (__instance.encounterInProgress)
            {
                //Plugin.Log.LogWarning($"Encounter in progress, queueing encounter {rewardWindowType}");
                __instance.rewardQueue.Enqueue(rewardWindowType);
                return false;
            }

            if (GameManager.Instance.player.IsDead())
            {
                if (synchronizationService.IsSharedExperienceEnabled() && !synchronizationService.ConsumeLocalOnlyEncounter())
                {
                    MyTime.Pause();
                    if (!synchronizationService.RewardFinished())
                    {
                        ScreenTextHelper.Show("Waiting for other player(s) choices...", new Vector2(0, -350));
                    }
                }
                return false;
            }

            if (PauseMenuHelper.IsPauseMenuOpen())
            {
                //Queued instead of shown behind the pause menu (see PopReward_Prefix), popped at LateUpdate once the menu is closed
                __instance.rewardQueue.Enqueue(rewardWindowType);
                NoteDeferredByPauseMenu();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prevent pause on netplay reward pop (Shady guy and other) on non shared experience
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(EncounterWindows.PopReward))]
        public static void PopReward_Postfix()
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            if (PauseMenuHelper.IsPauseMenuOpen())
            {
                return; //Nothing was popped (the prefix held it back): don't bring the previous window back behind the menu
            }

            if (synchronizationService.IsSharedExperienceEnabled())
            {
                UiManager.Instance.encounterWindows?.activeEncounterWindow?.gameObject.SetActive(true);
                return;
            }

            MyTime.Unpause();
        }

        /// <summary>
        /// PopReward_Prefix prevent reward pop if player can't move yet.
        /// If we can move now, we should pop previously prevented reward as soon as possible.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EncounterWindows.LateUpdate))]
        public static void LateUpdate_Prefix(EncounterWindows __instance)
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            if (!GameManager.Instance.player.playerInput.CanInput())
            {
                return;
            }

            if (GameManager.Instance.player.IsDead())
            {
                return;
            }

            var currentQueue = __instance.rewardQueue;
            if (currentQueue.Count > 0 && !__instance.encounterInProgress)
            {
                if (PauseMenuHelper.IsPauseMenuOpen())
                {
                    return; //Held back: popped on the first frame after the menu is closed
                }

                if (isDeferredByPauseMenu)
                {
                    isDeferredByPauseMenu = false;
                    Plugin.Log.LogInfo($"[PauseMenu] Menu closed, showing the {currentQueue.Count} reward(s) that were held back");
                }

                //Plugin.Log.LogInfo($"Pop previously missed reward");
                __instance.PopReward();
            }
            else if (isDeferredByPauseMenu && currentQueue.Count == 0 && !PauseMenuHelper.IsPauseMenuOpen())
            {
                isDeferredByPauseMenu = false; //The queue was emptied in the meantime (next stage, game over)
            }
        }

        private static void NoteDeferredByPauseMenu()
        {
            if (isDeferredByPauseMenu)
            {
                return;
            }

            isDeferredByPauseMenu = true;
            Plugin.Log.LogInfo("[PauseMenu] Reward held back until the pause menu is closed");
        }


        /// <summary>
        /// Synchronize end of reward. This is needed on shared experience to unblock the game for all players
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EncounterWindows.RewardFinished))]
        public static bool RewardFinished_Prefix(EncounterWindows __instance)
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return true;
            }

            if (!synchronizationService.IsSharedExperienceEnabled())
            {
                return true;
            }

            //Checked before the queue: when the encounter is released while more rewards are queued,
            //the state must still be cleared so the next queued reward waits for everyone again
            if (encounterService.IsClosable())
            {
                ScreenTextHelper.Clear();
                encounterService.ClearClosedEncounters();
                RestoreHiddenParticles();
                return true;
            }

            if (synchronizationService.ConsumeLocalOnlyEncounter())
            {
                return true; //Level up given by an xp sync: only this player has it, nobody to wait for
            }

            var currentQueue = __instance.rewardQueue;
            if (currentQueue.Count > 0) //Keep popping reward until queue is empty
            {
                return true;
            }

            if (synchronizationService.RewardFinished())
            {
                return false; //Everyone was already done: the encounter was just closed
            }

            var ui = UiManager.Instance;
            ui.encounterWindows?.activeEncounterWindow?.gameObject.SetActive(false);

            foreach (var particles in Il2CppFindHelper.RuntimeGetComponentsInChildren<UIParticleRenderer>(ui))
            {
                if (particles.enabled)
                {
                    particles.enabled = false;
                    hiddenParticles.Add(particles);
                }
            }

            ScreenTextHelper.Show("Waiting for other player(s) choices...", new Vector2(0, -350));

            return false;
        }

        private static void RestoreHiddenParticles()
        {
            foreach (var particles in hiddenParticles)
            {
                if (particles != null)
                {
                    particles.enabled = true;
                }
            }

            hiddenParticles.Clear();
        }
    }
}
