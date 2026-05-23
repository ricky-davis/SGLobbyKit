using System;
using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MultiplayerTools.Features.Settings
{
    internal static class SettingsMenuView
    {
        private static readonly Vector2 PanelSize = new Vector2(900f, 640f);
        private const float LabelWidth = 300f;
        private const float InputHeight = 36f;
        private const float SliderHeight = 30f;
        private const float ToggleHeight = 24.5f;
        private const float MessageRowHeight = 36f;
        private const float RowSpacing = 8f;
        private const float VerticalRowSpacing = RowSpacing + 5f;
        private const int ScrollBottomPadding = 48;
        private const float ScrollWheelSensitivity = 35f;
        private const float TitleFontSize = 24f;
        private const float LabelFontSize = 16f;
        private const float InputFontSize = 16f;
        private const float ServerNameFontSize = 16f;

        private static GameObject _firstSelectable;

        public static SettingsMenuHandle Build(Transform parent, SettingsDraft draft)
        {
            GameObject root = UILib.Create("MultiplayerTools Settings", parent).GameObject;
            UILib.Stretch(root);
            _firstSelectable = null;
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Image fadeTemplate = UILib.Find(SleddingUiPaths.BackgroundFade).Get<Image>();
            Image backdrop = UILib.CreateScreenBackdrop(root.transform, template: fadeTemplate);

            UILib.Element panel = UILib.CreatePanel(backdrop != null ? backdrop.transform : root.transform, "Panel");
            panel.CenterAnchors().Resize(PanelSize).Move(Vector2.zero);
            UILib.SetVerticalLayout(
                panel.GameObject,
                new RectOffset(43, 43, 26, 26),
                spacing: VerticalRowSpacing);

            AddHeader(panel.Transform, "MultiplayerTools Settings", addCloseButton: true);
            var scroll = UILib.CreateScrollViewport(panel.Transform, "Scroll Viewport");
            if (scroll.ScrollRect != null)
                scroll.ScrollRect.scrollSensitivity = ScrollWheelSensitivity;

            Transform content = scroll.ContentRect != null ? scroll.ContentRect.transform : panel.Transform;
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            if (contentLayout != null)
            {
                contentLayout.spacing = VerticalRowSpacing;
                contentLayout.padding.bottom = ScrollBottomPadding;
            }

            AddInput(content, "Server Name", draft.ServerName, value => draft.ServerName = value ?? string.Empty);
            AddCapacitySlider(content, draft);
            AddToggle(content, "Public Lobby", draft.IsPublicLobby, value => draft.IsPublicLobby = value);

            RectTransform passwordInputTrack = null;
            AddGridRow(content, MessageRowHeight, 0f, new[] { 3f, 4f }, cells =>
            {
                AddToggle(
                    cells[0],
                    "Password Protected",
                    draft.IsPasswordProtected,
                    value => draft.IsPasswordProtected = value,
                    labelOffsetX: 17f,
                    anchoredPosition: new Vector2(0f, -MessageRowHeight * 0.5f));
                passwordInputTrack = cells[1]?.GetComponent<RectTransform>();
                AddInput(cells[1], string.Empty, draft.LobbyPassword, value => draft.LobbyPassword = value ?? string.Empty, placeholderText: "enter password...");
            });
            if (passwordInputTrack != null)
                passwordInputTrack.offsetMin = new Vector2(308f, passwordInputTrack.offsetMin.y);

            AddToggle(content, "Peaceful Mode", draft.IsPeacefulMode, value => draft.IsPeacefulMode = value);
            AddToggle(content, "Text Chat Only", draft.IsTextChatOnly, value => draft.IsTextChatOnly = value);
            AddDivider(content);
            AddHeader(content, "Mod Settings", textScale: 0.7f, anchoredPosition: new Vector2(0f, 15f));
            AddToggle(content, "Enable Guest !Bang Commands", draft.EnableGuestBangCommands, value => draft.EnableGuestBangCommands = value);
            AddInput(content, "Message of the Day", draft.MessageOfTheDay, value => draft.MessageOfTheDay = value ?? string.Empty);
            AddMessageRow(
                content,
                "Join Messages",
                draft.ShowJoinMessages,
                value => draft.ShowJoinMessages = value,
                draft.JoinMessageSize,
                value => draft.JoinMessageSize = value);
            AddMessageRow(
                content,
                "Leave Messages",
                draft.ShowLeaveMessages,
                value => draft.ShowLeaveMessages = value,
                draft.LeaveMessageSize,
                value => draft.LeaveMessageSize = value);

            AddActionButtons(content);

            return new SettingsMenuHandle(root, _firstSelectable);
        }

        private static void CloseButtonOnClick()
        {
            SettingsMenuController.Instance.RequestClose();
        }

        private static void AddHeader(Transform parent, string label = null, bool addCloseButton = false, float textScale = 1f, Vector2? anchoredPosition = null)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, height: 50f).GameObject;

            GameObject titleContainer = UILib.Create("Header Title Container", row.transform).GameObject;
            UILib.SetFixedLayoutSize(titleContainer, flexibleWidth: 1f, preferredHeight: 50f);

            TMP_Text title = UILib.CreatePlainLabel(titleContainer.transform, label, "Header", UILib.Defaults.HeaderLabel ?? UILib.Defaults.Label);
            UILib.Stretch(title.gameObject);

            UILib.SetTextMetrics(title, TitleFontSize * Mathf.Max(0.01f, textScale), TextAlignmentOptions.Center);
            title.color = Color.white;
            title.fontStyle = FontStyles.Bold;
            title.ForceMeshUpdate();
            if (anchoredPosition != null)
            {
                title.rectTransform.anchoredPosition = anchoredPosition.Value;
                UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: anchoredPosition.Value.y);
            }
            else
            {
                UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: 50f);
            }

            if (!addCloseButton)
                return;

            Button closeButton = UILib.CreateButton(row.transform, "X", (UnityAction)CloseButtonOnClick, "Close", cloneTemplate: false);
            _firstSelectable = closeButton.gameObject;
            UILib.SetFixedLayoutSize(closeButton.gameObject, preferredWidth: 25f, preferredHeight: 25f);
        }

        private static void AddActionButtons(Transform parent)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, "Actions", height: 36f, spacing: RowSpacing).GameObject;
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: 36f);

            GameObject spacer = UILib.Create("Actions Spacer", row.transform).GameObject;
            UILib.SetFixedLayoutSize(spacer, flexibleWidth: 1f, preferredHeight: 36f);

            Button apply = UILib.CreateButton(row.transform, "Apply", (UnityAction)SettingsMenuController.Instance.Apply, "Apply");
            UILib.SetFixedLayoutSize(apply.gameObject, preferredWidth: 150f, preferredHeight: 36f);

            Button applyClose = UILib.CreateButton(row.transform, "Apply & Close", (UnityAction)SettingsMenuController.Instance.ApplyAndClose, "Apply & Close");
            UILib.SetFixedLayoutSize(applyClose.gameObject, preferredWidth: 190f, preferredHeight: 36f);
        }

        private static void AddCapacitySlider(Transform parent, SettingsDraft draft)
        {
            MySliderUI slider = null;
            slider = UILib.CreateSlider(
                parent,
                name: "Server Capacity Slider",
                minValue: 1f,
                maxValue: 64f,
                value: Mathf.Clamp(draft.ServerCapacity, 1, 64),
                onValueChanged: (UnityAction<float>)((value) =>
                {
                    draft.ServerCapacity = Mathf.RoundToInt(value);
                    slider?.UpdateSliderValueDisplay();
                }));

            if (slider == null || slider.slider == null)
            {
                AddIntInput(parent, "Server Capacity", draft.ServerCapacity, value => draft.ServerCapacity = value);
                return;
            }

            UILib.StabilizeClonedControl(slider.gameObject);

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

        private static void AddDivider(Transform parent, string label = null)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, "Divider", height: 2f, spacing: 0f).GameObject;
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

        private static void AddInput(Transform parent, string label, string value, Action<string> setter, string placeholderText = "")
        {
            GameObject row = AddPreferenceRow(parent, label);
            TMP_InputField input = UILib.CreateInputField(
                row.transform,
                value ?? string.Empty,
                placeholder: placeholderText ?? string.Empty,
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

        private static void AddToggle(
            Transform parent,
            string label,
            bool value,
            Action<bool> setter,
            bool clearLabel = false,
            bool stabilize = true,
            bool useFixedLayout = true,
            float? preferredHeight = null,
            Vector2? anchoredPosition = null,
            float labelOffsetX = 0f)
        {
            Toggle toggle = UILib.CreateToggle(
                parent,
                label,
                value,
                (UnityAction<bool>)((isOn) => setter(isOn)));

            TMP_Text labelText = toggle.GetComponentInChildren<TMP_Text>(true);
            if (clearLabel && labelText != null)
                UILib.SetText(labelText, string.Empty);
            UILib.SetTextMetrics(labelText, LabelFontSize, TextAlignmentOptions.Left, autoSize: true, minFontSize: 12f);
            if (labelText != null && Mathf.Abs(labelOffsetX) > 0.001f)
                labelText.rectTransform.localPosition += new Vector3(labelOffsetX, 0f, 0f);

            if (anchoredPosition.HasValue)
                UILib.SetRect(toggle, anchoredPosition: anchoredPosition.Value);

            float height = preferredHeight ?? ToggleHeight;
            if (useFixedLayout)
                UILib.SetFixedLayoutSize(toggle.gameObject, flexibleWidth: 1f, preferredHeight: height);
            else
                UILib.SetLayout(toggle.gameObject, flexibleWidth: 1f, preferredHeight: height);

            if (stabilize)
                UILib.StabilizeClonedControl(toggle.gameObject);
        }

        private static void AddMessageRow(
            Transform parent,
            string label,
            bool isOn,
            Action<bool> setToggle,
            int sizeValue,
            Action<int> setSize)
        {
            AddGridRow(parent, MessageRowHeight, RowSpacing, new[] { 2f, 1f, 4f }, cells =>
            {
                AddToggle(
                    cells[0],
                    label,
                    isOn,
                    setToggle,
                    clearLabel: false,
                    stabilize: false,
                    useFixedLayout: false,
                    preferredHeight: MessageRowHeight,
                    anchoredPosition: new Vector2(0f, -MessageRowHeight * 0.5f));
                CreateSizeSlider(cells[1], sizeValue, setSize);
            });
        }

        private static MySliderUI CreateSizeSlider(Transform parent, int sizeValue, Action<int> setSize)
        {
            MySliderUI sliderUi = null;
            sliderUi = UILib.CreateSlider(
                parent,
                name: "Size Slider",
                minValue: 50f,
                maxValue: 100f,
                value: Mathf.Clamp(sizeValue, 50, 100),
                onValueChanged: (UnityAction<float>)((value) =>
                {
                    setSize(Mathf.RoundToInt(value));
                    sliderUi?.UpdateSliderValueDisplay();
                }));

            if (sliderUi == null || sliderUi.slider == null)
                return null;

            foreach (LayoutGroup layout in sliderUi.GetComponentsInChildren<LayoutGroup>(true))
            {
                if (layout != null)
                    layout.enabled = false;
            }

            UILib.SetRect(sliderUi, anchoredPosition: new Vector2(350f, MessageRowHeight * -0.5f));
            UILib.SetRect(sliderUi.slider, anchoredPosition: Vector2.zero);
            sliderUi.slider.transform.localPosition = Vector3.zero;

            TMP_Text sliderLabel = sliderUi.transform.Find("(Text) Slider Label")?.GetComponent<TMP_Text>();
            if (sliderLabel != null)
            {
                UILib.SetText(sliderLabel, "Size");
                UILib.SetTextMetrics(sliderLabel, LabelFontSize, TextAlignmentOptions.Left);
                sliderLabel.color = Color.white;
                sliderLabel.ForceMeshUpdate();
            }

            if (sliderUi.sliderValueText != null)
            {
                UILib.SetRect(
                    sliderUi.sliderValueText,
                    anchorMin: new Vector2(0f, 0.5f),
                    anchorMax: new Vector2(0f, 0.5f),
                    pivot: new Vector2(1f, 0.5f));
                sliderUi.sliderValueText.transform.localPosition = new Vector3(160f, 17.5f, 0f);
                UILib.SetTextMetrics(sliderUi.sliderValueText, LabelFontSize, TextAlignmentOptions.Right);
                sliderUi.sliderValueText.color = Color.white;
                sliderUi.sliderValueText.ForceMeshUpdate();
            }

            UILib.SetFixedLayoutSize(sliderUi.gameObject, flexibleWidth: 1f, preferredHeight: SliderHeight);

            return sliderUi;
        }

        private static void AddGridRow(Transform parent, float height, float spacing, float[] trackWeights, Action<Transform[]> build)
        {
            UILib.GridTrackRow row = UILib.CreateGridTrackRow(parent, height: height, spacing: spacing, trackWidths: trackWeights);
            UILib.SetFixedLayoutSize(row.GameObject, flexibleWidth: 1f, preferredHeight: height);

            if (build == null || row.Tracks == null)
                return;

            build(Array.ConvertAll(row.Tracks, track => track != null ? track.transform : null));
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.GameObject.GetComponent<RectTransform>());
            UILib.LayoutGridTracks(row, spacing: spacing, trackWidths: trackWeights);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(row.GameObject.GetComponent<RectTransform>());
        }

        private static GameObject AddPreferenceRow(Transform parent, string label)
        {
            GameObject row = UILib.CreateHorizontalRow(parent, height: InputHeight, spacing: RowSpacing).GameObject;
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: InputHeight);

            if (string.IsNullOrEmpty(label))
            {
                UILib.SetRect(
                    row.GetComponent<RectTransform>(),
                    anchorMin: new Vector2(0f, 0.5f),
                    anchorMax: new Vector2(1f, 0.5f),
                    pivot: new Vector2(0.5f, 0.5f));
                RectTransform rowRect = row.GetComponent<RectTransform>();
                if (rowRect != null)
                {
                    rowRect.offsetMin = new Vector2(0f, -InputHeight * 0.5f);
                    rowRect.offsetMax = new Vector2(0f, InputHeight * 0.5f);
                }
                return row;
            }

            TMP_Text labelText = UILib.CreatePlainLabel(row.transform, label, "Label", UILib.Defaults.SliderLabel ?? UILib.Defaults.Label);
            UILib.SetTextMetrics(labelText, LabelFontSize, TextAlignmentOptions.Left, autoSize: true, minFontSize: 12f);
            labelText.color = Color.white;
            labelText.ForceMeshUpdate();
            UILib.SetFixedLayoutSize(labelText.gameObject, preferredWidth: LabelWidth, preferredHeight: InputHeight);
            return row;
        }
    }
}
