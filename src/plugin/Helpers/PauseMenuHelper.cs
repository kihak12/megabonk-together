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

        /// <summary>
        /// Add a "Sync XP" button to the pause menu (cloned from an existing button so it keeps the game's look).
        /// The pause menu is recreated with every stage, so this must be called at every game start
        /// </summary>
        public static void AddResyncXpButton(Action onClick)
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
                if (button.name == RESYNC_XP_BUTTON_NAME)
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
                Plugin.Log.LogWarning("[XpSync] No pause menu button found to clone, skipping the Sync XP button");
                return;
            }

            var anchor = restart ?? template;
            var go = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = RESYNC_XP_BUTTON_NAME;
            go.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex() + 1);

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
                text.text = "SYNC XP";
            }

            //Without a layout group the clone would sit on top of its template
            if (template.transform.parent.GetComponent<LayoutGroup>() == null)
            {
                var templateRect = anchor.GetComponent<RectTransform>();
                var rect = go.GetComponent<RectTransform>();
                if (templateRect != null && rect != null)
                {
                    rect.anchoredPosition = templateRect.anchoredPosition - new Vector2(0, templateRect.sizeDelta.y + 10f);
                }
            }

            try
            {
                pause.mainWindow?.allButtons?.Add(customButton); //Gamepad / keyboard navigation
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[XpSync] Could not register the Sync XP button for navigation: {ex.Message}");
            }

            go.SetActive(true);
        }
    }
}
