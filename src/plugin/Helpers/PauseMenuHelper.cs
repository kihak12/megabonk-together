using Assets.Scripts.Utility;
using MegabonkTogether.Scripts.Button;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace MegabonkTogether.Helpers
{
    /// <summary>
    /// Mod specific buttons added to the in-game pause menu
    /// </summary>
    internal static class PauseMenuHelper
    {
        private const string RESYNC_XP_BUTTON_NAME = "B_ResyncXp";
        private const string SYNC_TIMER_BUTTON_NAME = "B_SyncTimer";
        private static readonly string[] modButtonNames = [RESYNC_XP_BUTTON_NAME, SYNC_TIMER_BUTTON_NAME];

        /// <summary>
        /// Add a "Sync XP" button to the pause menu (cloned from an existing button so it keeps the game's look).
        /// The pause menu is recreated with every stage, so this must be called at every game start
        /// </summary>
        public static void AddResyncXpButton(Action onClick)
        {
            AddButton(RESYNC_XP_BUTTON_NAME, "SYNC XP", onClick);
        }

        /// <summary>
        /// Add a "Sync timer" button to the pause menu, under the Sync XP button when there is one.
        /// Like every mod button it must be added at every game start
        /// </summary>
        public static void AddSyncTimerButton(Action onClick)
        {
            AddButton(SYNC_TIMER_BUTTON_NAME, "SYNC TIMER", onClick);
        }

        /// <summary>
        /// The pause menu (main page, options or map) is on screen.
        /// The game never pauses in netplay, so things keep happening behind it
        /// </summary>
        public static bool IsPauseMenuOpen()
        {
            var pause = UiManager.Instance?.pause;
            if (pause == null || !pause.isActiveAndEnabled)
            {
                return false;
            }

            //Both conditions on purpose: a false positive here would hold the rewards back forever
            return IsShown(pause.main) || IsShown(pause.options) || IsShown(pause.map) || IsShown(pause.current);
        }

        private static bool IsShown(GameObject panel)
        {
            return panel != null && panel.activeInHierarchy;
        }

        private static void AddButton(string buttonName, string label, Action onClick)
        {
            var pause = UiManager.Instance?.pause;
            if (pause == null)
            {
                return;
            }

            MyButton template = null;
            MyButton restart = null;
            foreach (var button in Il2CppFindHelper.RuntimeGetComponentsInChildren<MyButton>(pause))
            {
                if (button.name == buttonName)
                {
                    return; //Already added for this stage
                }

                if (button.name == "B_Settings")
                {
                    template = button;
                }
                else if (button.name == "B_Restart")
                {
                    restart = button;
                }
            }

            template ??= restart;
            if (template == null)
            {
                Plugin.Log.LogWarning($"[PauseMenu] No pause menu button found to clone, skipping the {label} button");
                return;
            }

            var parent = template.transform.parent;
            if (parent.Find(buttonName) != null)
            {
                return; //Already added for this stage
            }

            //Mod buttons stack under B_Restart in the order they are added: anchor on the lowest one already there
            var anchor = (restart ?? template).transform;
            foreach (var modButtonName in modButtonNames)
            {
                var existing = parent.Find(modButtonName);
                if (existing != null && existing.GetSiblingIndex() > anchor.GetSiblingIndex())
                {
                    anchor = existing;
                }
            }

            var go = UnityEngine.Object.Instantiate(template.gameObject, parent);
            go.name = buttonName;
            go.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);

            var originalButton = go.GetComponent<MyButton>();
            if (originalButton != null)
            {
                UnityEngine.Object.DestroyImmediate(originalButton);
            }

            var unityButton = go.GetComponentInChildren<Button>();
            if (unityButton != null)
            {
                unityButton.onClick = new();
            }

            var localizeStringEvent = go.GetComponentInChildren<LocalizeStringEvent>();
            if (localizeStringEvent != null)
            {
                UnityEngine.Object.DestroyImmediate(localizeStringEvent);
            }

            var customButton = go.AddComponent<CustomButton>();
            customButton.SetOnClickAction(onClick);
            customButton.state = MyButton.EButtonState.Active;
            customButton.RefreshState();

            var textWrapper = go.GetComponent<ButtonTextWrapper>();
            var text = textWrapper != null ? textWrapper.t_text : go.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = label;
            }

            //Without a layout group the clone would sit on top of its template
            if (parent.GetComponent<LayoutGroup>() == null)
            {
                var anchorRect = anchor.GetComponent<RectTransform>();
                var rect = go.GetComponent<RectTransform>();
                if (anchorRect != null && rect != null)
                {
                    rect.anchoredPosition = anchorRect.anchoredPosition - new Vector2(0, anchorRect.sizeDelta.y + 10f);
                }
            }

            try
            {
                pause.mainWindow?.allButtons?.Add(customButton); //Gamepad / keyboard navigation
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[PauseMenu] Could not register the {label} button for navigation: {ex.Message}");
            }

            go.SetActive(true);
        }
    }
}
