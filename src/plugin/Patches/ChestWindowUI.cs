using Assets.Scripts.Utility;
using HarmonyLib;
using MegabonkTogether.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using UnityEngine;

namespace MegabonkTogether.Patches
{
    //TODO: Refacto with LevelUpScreenPatch
    [HarmonyPatch(typeof(ChestWindowUi))]
    internal static class ChestWindowUiPatches
    {
        private static readonly Services.ISynchronizationService synchronizationService = Plugin.Services.GetService<Services.ISynchronizationService>();
        public static Coroutine CurrentRoutine;
        private static TMPro.TextMeshProUGUI infoText;
        private static bool isOpeningFinished = false;

        private const float INVULNERABILITY_AFTER_REVEAL = 10f; //Time to pick the item while the world keeps running (no pause in netplay)
        private const float MAX_OPENING_WAIT = 10f; //Before the reveal (clicking Open + animation): never stay invulnerable forever if the item is never revealed

        /// <summary>
        /// Diagnostic for issue #93 (soft lock when opening chest)
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ChestWindowUi.Open))]
        public static void Open_Prefix(ChestWindowUi __instance, Assets.Scripts.UI.InGame.Rewards.EEncounter encounterType)
        {
            if (__instance.chestOpening == null || __instance.b_open == null ||
                __instance.b_leave == null || __instance.b_take == null ||
                __instance.b_banish == null || __instance.t_itemName == null)
            {
                Plugin.Log.LogError($"[chest #93] ChestWindowUi.Open about to dereference a destroyed ref: " +
                    $"encounter={encounterType} shared={synchronizationService.IsSharedExperienceEnabled()} " +
                    $"chestOpening={__instance.chestOpening == null} b_open={__instance.b_open == null} " +
                    $"b_leave={__instance.b_leave == null} b_take={__instance.b_take == null} " +
                    $"b_banish={__instance.b_banish == null} t_itemName={__instance.t_itemName == null}");
            }
        }

        /// <summary>
        /// The window and its opening animation stay as in the original game (the player clicks Open),
        /// so the player is made invulnerable as soon as the window shows: the world is never paused in netplay.
        /// Let the original flow run if shared experience is enabled
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ChestWindowUi.Open))]
        public static void Open_Postfix(ChestWindowUi __instance)
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            if (synchronizationService.IsSharedExperienceEnabled())
            {
                return;
            }

            if (!GameManager.Instance.player.playerInput.CanInput())
            {
                Plugin.Log.LogInfo("Player cannot input, skipping invulnerability while opening chest.");
                return;
            }

            isOpeningFinished = false;
            CoroutineRunner.Instance.Stop(CurrentRoutine);
            CurrentRoutine = CoroutineRunner.Instance.Run(InvulnerableWhileChestOpen(__instance));
        }

        /// <summary>
        /// The item is revealed: start the invulnerability countdown
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ChestWindowUi.OpeningFinished))]
        public static void OpeningFinished_Postfix(ChestWindowUi __instance)
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            if (synchronizationService.IsSharedExperienceEnabled())
            {
                return;
            }

            MyTime.Unpause();

            isOpeningFinished = true;
        }

        /// <summary>
        /// Remove the invulnerability as soon as the chest window is closed on netplay
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ChestWindowUi.OnClose))]
        public static void OnClose_Postfix(ChestWindowUi __instance)
        {
            if (!synchronizationService.HasNetplaySessionStarted())
            {
                return;
            }

            if (synchronizationService.IsSharedExperienceEnabled())
            {
                return;
            }

            CoroutineRunner.Instance.Stop(CurrentRoutine);
            CurrentRoutine = null;
            isOpeningFinished = false;

            if (Plugin.Instance.IS_MANUAL_INVINCIBLE)
            {
                GameManager.Instance.player.isTeleporting = false;
                Plugin.Instance.IS_MANUAL_INVINCIBLE = false;
            }

            if (infoText != null)
            {
                infoText.enabled = false;
            }

            Plugin.Instance.NetPlayersDisplayer.Show();
        }

        private static IEnumerator InvulnerableWhileChestOpen(ChestWindowUi __instance)
        {
            if (infoText == null)
            {
                var go = new GameObject("InfoText");
                UnityEngine.GameObject.DontDestroyOnLoad(go);
                infoText = go.AddComponent<TMPro.TextMeshProUGUI>();
            }

            if (infoText == null)
            {
                yield break;
            }

            GameManager.Instance.player.isTeleporting = true;
            Plugin.Instance.IS_MANUAL_INVINCIBLE = true;

            Plugin.Instance.NetPlayersDisplayer.Hide();

            infoText.enabled = true;
            infoText.transform.SetParent(__instance.transform);
            infoText.rectTransform.anchorMin = new Vector2(0, 0);
            infoText.rectTransform.anchorMax = new Vector2(0, 0);
            infoText.rectTransform.pivot = new Vector2(0, 0);
            infoText.rectTransform.anchoredPosition = new Vector2(100, 100);
            infoText.alignment = TMPro.TextAlignmentOptions.Center;
            infoText.fontSize = 36;
            infoText.color = Color.white;
            infoText.text = "Opening chest... you are invulnerable";

            //Waiting for the player to click Open and for the original opening animation
            float waited = 0f;
            while (!isOpeningFinished && waited < MAX_OPENING_WAIT)
            {
                yield return null;
                waited += Time.deltaTime;

                if (!Plugin.Instance.IS_MANUAL_INVINCIBLE)
                {
                    infoText.enabled = false;
                    yield break;
                }
            }

            float timer = INVULNERABILITY_AFTER_REVEAL;
            while (timer > 0f)
            {
                int seconds = Mathf.FloorToInt(timer);
                int milliseconds = Mathf.FloorToInt((timer - seconds) * 1000);
                infoText.text = $"You will be vulnerable in {seconds.ToString("D2")}:{milliseconds.ToString("D3")} secondes";
                yield return null;
                timer -= Time.deltaTime;

                if (!Plugin.Instance.IS_MANUAL_INVINCIBLE)
                {
                    infoText.enabled = false;
                    yield break;
                }
            }

            GameManager.Instance.player.isTeleporting = false;
            Plugin.Instance.IS_MANUAL_INVINCIBLE = false;

            infoText.text = "You are now vulnerable!";
            infoText.color = Color.red;
        }
    }
}
