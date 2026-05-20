using System;
using HarmonyLib;
using Il2Cpp;
using Il2Cpp_Scripts.Player;
using Il2Cpp_Scripts.Systems.Chat;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class SettingsCommand
    {
        private static readonly Vector2 PanelSize = new Vector2(900f, 640f);
        private const float LabelWidth = 300f;
        private const float InputHeight = 36f;
        private const float SliderHeight = 30f;
        private const float ToggleHeight = 24.5f;
        private const float RowSpacing = 8f;
        private const float TitleFontSize = 24f;
        private const float LabelFontSize = 16f;
        private const float InputFontSize = 16f;
        private const float ServerNameFontSize = 16f;

        private static GameObject _root;
        private static GameObject _firstSelectable;

        public static void HandleCommand(PlayerControl playerControl, string args)
        {
            Show();
        }

        public static bool IsOpen => _root != null;

        private static void Show()
        {
            CloseSettings();
            UILib.CaptureSceneDefaults(overwriteExisting: false);
            ForceCloseChat();
            UiReferenceController.Instance?.CloseAllOpenMenus(false);

            Transform parent = FindNativeUiParent();
            if (parent == null)
            {
                Debug.LogWarning("[MultiplayerTools] Could not open settings UI: no native UI parent is available.");
                return;
            }

            _root = UILib.Create("MultiplayerTools Settings", parent).GameObject;
            UILib.Stretch(_root);
            _firstSelectable = null;
            CanvasGroup canvasGroup = _root.GetComponent<CanvasGroup>() ?? _root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Image fadeTemplate = UILib.Find("UI_BackgroundFade").Get<Image>();
            Image backdrop = UILib.CreateScreenBackdrop(_root.transform, template: fadeTemplate);

            UILib.Element panel = UILib.CreatePanel(backdrop != null ? backdrop.transform : _root.transform, "Panel");
            panel.CenterAnchors().Resize(PanelSize).Move(Vector2.zero);
            UILib.SetVerticalLayout(
                panel.GameObject,
                new RectOffset(43, 43, 26, 26),
                spacing: RowSpacing);

            AddHeader(panel.Transform, "MultiplayerTools Settings", addCloseButton: true);
            AddInput(panel.Transform, "Server Name", MultiplayerToolsCore.ServerName, MultiplayerToolsCore.SetServerName);
            AddCapacitySlider(panel.Transform);
            AddToggle(panel.Transform, "Public Lobby", MultiplayerToolsCore.IsPublicLobby, MultiplayerToolsCore.SetIsPublicLobby);
            AddToggle(panel.Transform, "Password Protected", MultiplayerToolsCore.IsPasswordProtected, MultiplayerToolsCore.SetIsPasswordProtected);
            AddInput(panel.Transform, "Lobby Password", MultiplayerToolsCore.LobbyPassword, MultiplayerToolsCore.SetLobbyPassword);
            AddToggle(panel.Transform, "Peaceful Mode", MultiplayerToolsCore.IsPeacefulMode, MultiplayerToolsCore.SetIsPeacefulMode);
            AddToggle(panel.Transform, "Text Chat Only", MultiplayerToolsCore.IsTextChatOnly, MultiplayerToolsCore.SetIsTextChatOnly);
            AddDivider(panel.Transform);
            AddHeader(panel.Transform, "Mod Settings", textScale: 0.7f);
            AddToggle(panel.Transform, "Guest Bang Commands", MultiplayerToolsCore.EnableGuestBangCommands, MultiplayerToolsCore.SetEnableGuestBangCommands);
            AddInput(panel.Transform, "Message of the Day", MultiplayerToolsCore.MessageOfTheDay, MultiplayerToolsCore.SetMessageOfTheDay);
            AddToggle(panel.Transform, "Join Messages", MultiplayerToolsCore.ShowJoinMessages, MultiplayerToolsCore.SetShowJoinMessages);
            AddToggle(panel.Transform, "Leave Messages", MultiplayerToolsCore.ShowLeaveMessages, MultiplayerToolsCore.SetShowLeaveMessages);

            OpenAsGameMenu();
        }

        private static Transform FindNativeUiParent()
        {
            UiReferenceController uiController = UiReferenceController.Instance;
            Transform parent =
                uiController?.settingsMenu.panel?.transform.parent ??
                uiController?.pauseMenu.panel?.transform.parent ??
                uiController?.playerActiveCanvas?.transform ??
                uiController?.mainMenu.panel?.transform.parent;

            if (parent != null)
                return parent;

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }

        private static void CloseSettings()
        {
            if (_root == null)
                return;

            UiReferenceController.Instance?.CloseAllOpenMenus(false);
            Object.Destroy(_root);
            _root = null;
            _firstSelectable = null;

            ForceCloseChat();
            RestoreClosedCursorState();
        }

        private static void CloseButtonOnClick()
        {
            CloseSettings();
        }

        private static void RestoreClosedCursorState()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ForceCloseChat()
        {
            ChatManager chat = ChatManager.Instance;
            if (chat == null)
                return;

            try
            {
                chat.chatBox?.Disable(false);
                Traverse.Create(chat).Property("IsChatOpen").SetValue(false);
                Traverse.Create(chat).Field("<IsChatOpen>k__BackingField").SetValue(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] Failed to force close chat: {ex.Message}");
            }
        }

        private static void OpenAsGameMenu()
        {
            UiReferenceController uiController = UiReferenceController.Instance;
            if (uiController == null || _root == null)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            UiReferenceController.UiToggleableMenu menu = new UiReferenceController.UiToggleableMenu
            {
                panel = _root,
                firstSelectable = _firstSelectable
            };

            uiController.OpenMenu(menu);
        }

        [HarmonyPatch(typeof(UiReferenceController), "Update")]
        [HarmonyPostfix]
        private static void UiReferenceController_Update_Postfix()
        {
            if (_root == null || !UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return;

            CloseSettings();
        }

        private static void AddHeader(Transform parent, string label=null, bool addCloseButton=false, float textScale=1f)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, height: 50f).GameObject;
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: 50f);

            TMP_Text title = UILib.CreatePlainLabel(row.transform, label, "Header", UILib.Defaults.HeaderLabel ?? UILib.Defaults.Label);
            UILib.SetTextMetrics(title, TitleFontSize * Mathf.Max(0.01f, textScale), TextAlignmentOptions.Center);
            title.color = Color.white;
            title.fontStyle = FontStyles.Bold;
            title.ForceMeshUpdate();
            UILib.SetFixedLayoutSize(title.gameObject, flexibleWidth: 1f, preferredHeight: 50f);

            if (!addCloseButton)
                return;
            Button closeButton = UILib.CreateButton(row.transform, "X", (UnityAction)CloseButtonOnClick, "Close", cloneTemplate: false);
            _firstSelectable = closeButton.gameObject;
            UILib.SetFixedLayoutSize(closeButton.gameObject, preferredWidth: 25f, preferredHeight: 25f);
        }

        private static void AddCapacitySlider(Transform parent)
        {
            MySliderUI slider = null;
            slider = UILib.CreateSlider(
                parent,
                name: "Server Capacity Slider",
                minValue: 1f,
                maxValue: 64f,
                value: Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64),
                onValueChanged: (UnityAction<float>)((value) =>
                {
                    MultiplayerToolsCore.SetServerCapacity(Mathf.RoundToInt(value));
                    slider?.UpdateSliderValueDisplay();
                }));

            if (slider == null || slider.slider == null)
            {
                AddIntInput(parent, "Server Capacity", MultiplayerToolsCore.ServerCapacity, MultiplayerToolsCore.SetServerCapacity);
                return;
            }

            TMP_Text label = slider.transform.Find("(Text) Slider Label")?.GetComponent<TMP_Text>();
            if (label != null)
            {
                UILib.SetText(label, "Server Capacity");
                UILib.SetTextMetrics(label, LabelFontSize, TextAlignmentOptions.Left);
                label.color = Color.white;
                label.ForceMeshUpdate();
            }

            if (slider.sliderValueText != null)
            {
                UILib.SetTextMetrics(slider.sliderValueText, LabelFontSize, TextAlignmentOptions.Right);
                slider.sliderValueText.color = Color.white;
                slider.sliderValueText.ForceMeshUpdate();
            }

            UILib.SetFixedLayoutSize(slider.gameObject, flexibleWidth: 1f, preferredHeight: SliderHeight);
        }

        private static void AddDivider(Transform parent, string label=null)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, "Divider", height: 2f, spacing: 12f).GameObject;
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: 2f);

            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text text = UILib.CreatePlainLabel(row.transform, label, "Divider Label", UILib.Defaults.SliderLabel ?? UILib.Defaults.Label);
                UILib.SetTextMetrics(text, LabelFontSize, TextAlignmentOptions.Left);
                UILib.SetFixedLayoutSize(text.gameObject, preferredWidth: LabelWidth, preferredHeight: 24f);
            }

            Image line = UILib.Create("Divider Line", row.transform).GameObject.AddComponent<Image>();
            line.color = new Color(1f, 1f, 1f, 0.28f);
            line.raycastTarget = false;
            UILib.SetLayout(line.gameObject, flexibleWidth: 1f, preferredHeight: 2f, minWidth: 0f, minHeight: 0f);
        }

        private static void AddInput(Transform parent, string label, string value, Action<string> setter)
        {
            GameObject row = AddPreferenceRow(parent, label);
            TMP_InputField input = UILib.CreateInputField(
                row.transform,
                value ?? string.Empty,
                placeholder: string.Empty,
                name: "Input",
                onValueChanged: (UnityAction<string>)((text) => setter(text)));
            input.lineType = TMP_InputField.LineType.SingleLine;
            bool isServerName = string.Equals(label, "Server Name", StringComparison.OrdinalIgnoreCase);
            UILib.SetInputTextStyle(input, isServerName ? ServerNameFontSize : InputFontSize, autoSize: isServerName, minFontSize: 13f);
            UILib.SetFixedLayoutSize(input.gameObject, flexibleWidth: 1f, preferredHeight: InputHeight);
        }

        private static void AddIntInput(Transform parent, string label, int value, Action<int> setter)
        {
            GameObject row = AddPreferenceRow(parent, label);
            TMP_InputField input = UILib.CreateInputField(
                row.transform,
                value.ToString(),
                placeholder: string.Empty,
                name: "Input",
                onValueChanged: (UnityAction<string>)((text) =>
                {
                    if (int.TryParse(text, out int parsed))
                        setter(Mathf.Clamp(parsed, 1, 64));
                }));

            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            UILib.SetInputTextStyle(input, InputFontSize);
            UILib.SetFixedLayoutSize(input.gameObject, flexibleWidth: 1f, preferredHeight: InputHeight);
        }

        private static void AddToggle(Transform parent, string label, bool value, Action<bool> setter)
        {
            Toggle toggle = UILib.CreateToggle(
                parent,
                label,
                value,
                (UnityAction<bool>)((isOn) => setter(isOn)));

            TMP_Text labelText = toggle.GetComponentInChildren<TMP_Text>(true);
            UILib.SetTextMetrics(labelText, LabelFontSize, TextAlignmentOptions.Left, autoSize: true, minFontSize: 12f);
            UILib.StabilizeClonedControl(toggle.gameObject);
            UILib.SetFixedLayoutSize(toggle.gameObject, flexibleWidth: 1f, preferredHeight: ToggleHeight);
        }

        private static GameObject AddPreferenceRow(Transform parent, string label)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, height: InputHeight, spacing: RowSpacing).GameObject;
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: InputHeight);

            TMP_Text labelText = UILib.CreatePlainLabel(row.transform, label, "Label", UILib.Defaults.SliderLabel ?? UILib.Defaults.Label);
            UILib.SetTextMetrics(labelText, LabelFontSize, TextAlignmentOptions.Left, autoSize: true, minFontSize: 12f);
            labelText.color = Color.white;
            labelText.ForceMeshUpdate();
            UILib.SetFixedLayoutSize(labelText.gameObject, preferredWidth: LabelWidth, preferredHeight: InputHeight);
            return row;
        }
    }
}
