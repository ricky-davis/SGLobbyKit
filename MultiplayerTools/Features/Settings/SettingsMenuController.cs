using System;
using HarmonyLib;
using Il2Cpp;
using Il2Cpp_Scripts.Systems.Chat;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiplayerTools.Features.Settings
{
    internal delegate bool SettingsTemplateValidator(out string missingTemplates);

    internal sealed class SettingsMenuController
    {
        private GameObject _root;
        private GameObject _firstSelectable;
        private GameObject _confirmationRoot;
        private SettingsDraft _draft;
        private SettingsDraft _cleanDraft;

        public static SettingsMenuController Instance { get; } = new SettingsMenuController();

        private SettingsMenuController()
        {
        }

        public bool IsOpen => _root != null;

        public void Open(
            Func<Transform, SettingsDraft, SettingsMenuHandle> buildMenu,
            SettingsTemplateValidator validateTemplates = null)
        {
            CloseWithoutPrompt();
            SleddingUiAdapter.CaptureSceneTemplates(overwriteExisting: false);
            if (validateTemplates != null && !validateTemplates(out string missingTemplates))
            {
                Debug.LogWarning($"[MultiplayerTools] Could not open settings UI: missing native UI templates: {missingTemplates}.");
                return;
            }

            ForceCloseChat();
            UiReferenceController.Instance?.CloseAllOpenMenus(false);

            Transform parent = FindNativeUiParent();
            if (parent == null)
            {
                Debug.LogWarning("[MultiplayerTools] Could not open settings UI: no native UI parent is available.");
                return;
            }

            _draft = SettingsDraft.FromCurrent();
            _cleanDraft = _draft.Clone();

            SettingsMenuHandle handle = buildMenu?.Invoke(parent, _draft);
            _root = handle?.Root;
            _firstSelectable = handle?.FirstSelectable;

            if (_root == null)
            {
                Debug.LogWarning("[MultiplayerTools] Could not open settings UI: settings view was not created.");
                _draft = null;
                _cleanDraft = null;
                return;
            }

            OpenAsGameMenu();
        }

        public void RequestClose()
        {
            if (_root == null)
                return;

            if (HasUnappliedChanges)
            {
                ShowCloseConfirmation();
                return;
            }

            CloseWithoutPrompt();
        }

        public void Apply()
        {
            if (_draft == null)
                return;

            _draft.Apply();
            _cleanDraft = _draft.Clone();
        }

        public void ApplyAndClose()
        {
            Apply();
            CloseWithoutPrompt();
        }

        public void DiscardAndClose()
        {
            CloseWithoutPrompt();
        }

        private bool HasUnappliedChanges => _draft != null && !_draft.ValueEquals(_cleanDraft);

        private void CloseWithoutPrompt()
        {
            if (_root == null)
            {
                _confirmationRoot = null;
                _firstSelectable = null;
                _draft = null;
                _cleanDraft = null;
                return;
            }

            UiReferenceController.Instance?.CloseAllOpenMenus(false);
            _confirmationRoot = null;
            Object.Destroy(_root);
            _root = null;
            _firstSelectable = null;
            _draft = null;
            _cleanDraft = null;

            ForceCloseChat();
            RestoreClosedCursorState();
        }

        public void HandleEscape()
        {
            if (_root == null || !UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return;

            RequestClose();
        }

        private void ShowCloseConfirmation()
        {
            if (_root == null)
                return;

            if (_confirmationRoot != null)
            {
                _confirmationRoot.SetActive(true);
                return;
            }

            _confirmationRoot = UILib.Create("Unapplied Changes Confirmation", _root.transform).GameObject;
            UILib.Stretch(_confirmationRoot);

            Image backdrop = UILib.CreateScreenBackdrop(_confirmationRoot.transform, color: new Color(0f, 0f, 0f, 0.35f));

            UILib.Element panel = UILib.CreatePanel(backdrop != null ? backdrop.transform : _confirmationRoot.transform, "Confirmation Panel");
            panel.CenterAnchors().Resize(new Vector2(560f, 190f)).Move(Vector2.zero);
            UILib.SetVerticalLayout(panel.GameObject, new RectOffset(26, 26, 22, 22), spacing: 14f);

            TMP_Text title = NativeUiFactory.Label(panel.Transform, "Unapplied Changes", "Confirmation Title", UILib.Defaults.HeaderLabel ?? UILib.Defaults.Label);
            UILib.SetTextMetrics(title, 20f, TextAlignmentOptions.Center);
            title.color = Color.white;
            title.fontStyle = FontStyles.Bold;
            UILib.SetFixedLayoutSize(title.gameObject, flexibleWidth: 1f, preferredHeight: 34f);

            TMP_Text body = NativeUiFactory.Label(panel.Transform, "Apply your changes before closing?", "Confirmation Body", UILib.Defaults.Label);
            UILib.SetTextMetrics(body, 15f, TextAlignmentOptions.Center, autoSize: true, minFontSize: 12f);
            body.color = Color.white;
            UILib.SetFixedLayoutSize(body.gameObject, flexibleWidth: 1f, preferredHeight: 38f);

            GameObject row = NativeUiBuilder.HorizontalRow(panel.Transform, "Confirmation Actions", height: 36f, spacing: 10f).GameObject;
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandHeight = false;
            }
            UILib.SetFixedLayoutSize(row, flexibleWidth: 1f, preferredHeight: 36f);

            Button applyClose = CreateNativeButton(row.transform, "Apply & Close", (UnityEngine.Events.UnityAction)ApplyAndClose, "Apply & Close");
            UILib.SetFixedLayoutSize(applyClose.gameObject, flexibleWidth: 1f, preferredHeight: 36f);

            Button discardClose = CreateNativeButton(row.transform, "Discard & Close", (UnityEngine.Events.UnityAction)DiscardAndClose, "Discard & Close");
            UILib.SetFixedLayoutSize(discardClose.gameObject, flexibleWidth: 1f, preferredHeight: 36f);

            Button cancel = CreateNativeButton(row.transform, "Cancel", (UnityEngine.Events.UnityAction)(() => _confirmationRoot?.SetActive(false)), "Cancel");
            UILib.SetFixedLayoutSize(cancel.gameObject, flexibleWidth: 1f, preferredHeight: 36f);
        }

        private static Button CreateNativeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, string name)
        {
            Button button = NativeUiFactory.Button(parent, label, onClick, name);
            SettingsMenuView.NormalizeSettingsButton(button, 14f, new Vector2(150f, 36f));
            button.gameObject.SetActive(true);
            return button;
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

        private void OpenAsGameMenu()
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
    }
}
