using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using EosLobby = Il2CppPlayEveryWare.EpicOnlineServices.Samples.Lobby;
using UnityObject = UnityEngine.Object;

namespace MultiplayerTools.Features.Lobby
{
    internal sealed class LobbyUiController
    {
        private readonly HashSet<int> _boundNativeControlIds = new HashSet<int>();
        private readonly MainMenuAdapter _mainMenuAdapter = new MainMenuAdapter();
        private readonly LobbyMenuAdapter _lobbyMenuAdapter = new LobbyMenuAdapter();
        private MySliderUI _maxPlayerSlider;
        private TMP_InputField _customLobbyNameInput;
        private Toggle _enableGuestBangCommandsToggle;
        private bool _togglesInitialized;

        public static LobbyUiController Instance { get; } = new LobbyUiController();

        private LobbyUiController()
        {
        }

        public bool TryMarkNativeControlBound(UnityObject control)
        {
            if (control == null)
                return false;

            return _boundNativeControlIds.Add(control.GetInstanceID());
        }

        public void ClearNativeControlBindings()
        {
            _boundNativeControlIds.Clear();
        }

        public void ApplyLobbyManagerCreateLobby(ref string lobbyName, ref int maxPlayers)
        {
            LobbyCreateSubmission.Apply(ref lobbyName, ref maxPlayers, _customLobbyNameInput, _maxPlayerSlider);
        }

        public void ApplyEosCreateLobby(EosLobby lobbyProperties)
        {
            LobbyCreateSubmission.ApplyToEosLobby(lobbyProperties, _maxPlayerSlider);
        }

        public void ConfigureMainMenuOnEnable(UIMainMenu mainMenu)
        {
            try
            {
                Transform menuPanel = _mainMenuAdapter.Configure(mainMenu);
                SleddingUiAdapter.CaptureTemplatesFrom(menuPanel, overwriteExisting: false);
                Transform createLobbyRoot = _lobbyMenuAdapter.FindCreateLobbyRoot(menuPanel);
                if (createLobbyRoot == null)
                {
                    Debug.LogWarning($"[MultiplayerTools] Could not embed create lobby UI: {SleddingUiPaths.CreateLobbyRoot} root was not found.");
                    return;
                }

                SleddingUiAdapter.Assume(createLobbyRoot)
                    .Reparent(menuPanel)
                    .LastSibling()
                    .CenterAnchors()
                    .Scale(new Vector3(0.9f, 0.9f, 1f))
                    .Move(new Vector2(-295f, 0f));

                Debug.Log($"[MultiplayerTools] Embedded create lobby UI: {createLobbyRoot.name} under {menuPanel?.name ?? "null"}");
                MelonCoroutines.Start(ActivateEmbeddedCreateLobbyForNextFrames(menuPanel));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] Failed to embed create lobby UI: {ex}");
            }
        }

        private void EnsureLobbyNameInput(UICreateLobby createLobby)
        {
            dynamic lobbyNameText = createLobby.lobbyNameText;
            dynamic passwordInput = createLobby.passwordInputField;
            if (lobbyNameText == null || passwordInput == null)
                return;

            Transform layoutParent = lobbyNameText.transform.parent;
            bool needsInput = _customLobbyNameInput == null || _customLobbyNameInput.transform.parent != layoutParent;
            if (!needsInput)
            {
                SleddingUiAdapter.Assume(_customLobbyNameInput).Show();
                SleddingUiAdapter.Assume((GameObject)lobbyNameText.gameObject).Hide();
                return;
            }

            int labelIndex = lobbyNameText.transform.GetSiblingIndex();
            TMP_InputField input = NativeUiFactory.InputFromTemplate(
                (TMP_InputField)passwordInput,
                layoutParent,
                name: "CustomLobbyNameInput",
                siblingIndex: labelIndex);
            if (input == null)
                return;

            UiLayout.SetRect(input, sizeDelta: new Vector2(473.99f, 47.04f), scale: Vector3.one);
            input.characterLimit = 64;
            input.lineLimit = 1;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.inputType = TMP_InputField.InputType.Standard;
            input.interactable = true;
            UiStyles.SetInputTextStyle(input, 18f, autoSize: true, minFontSize: 12f);

            input.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
            {
                lobbyNameText.text = text;
                MultiplayerToolsCore.SetServerName(text);
            }));
            string playerName = GameInfo.Instance?.PlayerName ?? "Sledding";
            input.text = !string.IsNullOrWhiteSpace(MultiplayerToolsCore.ServerName)
                ? MultiplayerToolsCore.ServerName
                : $"{playerName}'s Lobby";

            TMP_Text placeholderText = input.placeholder?.GetComponent<TMP_Text>();
            if (placeholderText != null)
                UiStyles.SetText(placeholderText, "Lobby name...");

            SleddingUiAdapter.Assume((GameObject)lobbyNameText.gameObject).Hide();
            SleddingUiAdapter.Assume(input).Show();
            _customLobbyNameInput = input;
        }

        private void EnsureMaxPlayerSlider(UICreateLobby createLobby)
        {
            MySliderUI slider = createLobby.maxPlayersSlider;
            if (slider == null || slider.slider == null)
                return;

            bool needsListener = TryMarkNativeControlBound(slider.slider);
            _maxPlayerSlider = slider;
            _maxPlayerSlider.slider.maxValue = 64f;
            _maxPlayerSlider.slider.SetValueWithoutNotify(Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64));
            _maxPlayerSlider.UpdateSliderValueDisplay();

            if (!needsListener)
                return;

            _maxPlayerSlider.slider.onValueChanged.AddListener((UnityEngine.Events.UnityAction<float>)((value) =>
            {
                MultiplayerToolsCore.SetServerCapacity(Mathf.RoundToInt(value));
            }));
        }

        private void EnsureLobbyOptionBindings(UICreateLobby createLobby)
        {
            Toggle textChatOnlyToggle = createLobby.textChatOnlyToggle;
            if (textChatOnlyToggle == null)
                return;

            if (_enableGuestBangCommandsToggle == null)
            {
                _togglesInitialized = false;
            }
            else if (_enableGuestBangCommandsToggle.transform.parent != textChatOnlyToggle.transform.parent)
            {
                _enableGuestBangCommandsToggle = null;
                _togglesInitialized = false;
            }

            if (_togglesInitialized)
            {
                _enableGuestBangCommandsToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                return;
            }

            Toggle publicLobbyToggle = createLobby.publicLobbyToggle;
            if (TryMarkNativeControlBound(publicLobbyToggle))
            {
                publicLobbyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPublicLobby(isOn);
                }));
            }
            (MultiplayerToolsCore.IsPublicLobby ? publicLobbyToggle : publicLobbyToggle.group.m_Toggles[0]).isOn = true;

            Toggle passwordProtectedToggle = createLobby.passwordProtectedToggle;
            passwordProtectedToggle.isOn = MultiplayerToolsCore.IsPasswordProtected;
            if (TryMarkNativeControlBound(passwordProtectedToggle))
            {
                passwordProtectedToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPasswordProtected(isOn);
                }));
            }

            TMP_InputField passwordInputField = createLobby.passwordInputField;
            passwordInputField.text = MultiplayerToolsCore.LobbyPassword;
            if (TryMarkNativeControlBound(passwordInputField))
            {
                passwordInputField.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
                {
                    MultiplayerToolsCore.SetLobbyPassword(text);
                }));
            }

            Toggle peacefulModeToggle = createLobby.peacefulModeToggle;
            peacefulModeToggle.isOn = MultiplayerToolsCore.IsPeacefulMode;
            if (TryMarkNativeControlBound(peacefulModeToggle))
            {
                peacefulModeToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPeacefulMode(isOn);
                }));
            }

            textChatOnlyToggle.isOn = MultiplayerToolsCore.IsTextChatOnly;
            if (TryMarkNativeControlBound(textChatOnlyToggle))
            {
                textChatOnlyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsTextChatOnly(isOn);
                }));
            }

            EnsureGuestBangCommandsToggle(textChatOnlyToggle);
            _togglesInitialized = true;
        }

        private void EnsureGuestBangCommandsToggle(Toggle textChatOnlyToggle)
        {
            if (_enableGuestBangCommandsToggle != null)
            {
                _enableGuestBangCommandsToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                return;
            }

            Toggle toggle = NativeUiFactory.ToggleFromTemplate(
                textChatOnlyToggle,
                textChatOnlyToggle.transform.parent,
                name: "EnableGuestBangCommandsToggle",
                label: "Enable Guest !Bang Commands",
                isOn: MultiplayerToolsCore.EnableGuestBangCommands,
                onValueChanged: (UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetEnableGuestBangCommands(isOn);
                }),
                siblingIndex: textChatOnlyToggle.transform.GetSiblingIndex() + 1);
            if (toggle == null)
                return;

            SleddingUiAdapter.Assume(toggle).Show();
            _enableGuestBangCommandsToggle = toggle;
        }

        private void SetupCreateLobbyPanel(UICreateLobby createLobby)
        {
            if (createLobby == null)
                return;

            SleddingUiAdapter.CaptureTemplatesFrom(createLobby.transform, overwriteExisting: false);
            EnsureLobbyNameInput(createLobby);
            EnsureMaxPlayerSlider(createLobby);
            _lobbyMenuAdapter.HideCreateLobbyCloseButton(createLobby);
            EnsureLobbyOptionBindings(createLobby);
        }

        private IEnumerator ActivateEmbeddedCreateLobbyForNextFrames(Transform menuPanelRoot)
        {
            // Vanilla disables UI_CreateLobby/Panels shortly after OnEnable.
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;

                Transform refreshedRoot = _lobbyMenuAdapter.FindCreateLobbyRoot(menuPanelRoot);
                if (refreshedRoot == null)
                    continue;

                SetupCreateLobbyPanel(_lobbyMenuAdapter.ActivateEmbeddedCreateLobby(refreshedRoot, showPanel: true));
            }
        }
    }
}
