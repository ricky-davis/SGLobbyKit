using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;

namespace MultiplayerTools.Patches
{

    [HarmonyPatch]
    public static class LobbyPatchFeatures
    {
        private static MySliderUI _maxPlayerSlider;
        private static TMP_InputField _customLobbyNameInput;
        private static Toggle _enableGuestBangCommandsToggle;
        private static bool _togglesInitialized;

        private static string GetInputLobbyName()
        {
            if (_customLobbyNameInput != null)
            {
                var customName = _customLobbyNameInput.text;
                if (!string.IsNullOrWhiteSpace(customName))
                    return customName;
            }

            if (!string.IsNullOrWhiteSpace(MultiplayerToolsCore.ServerName))
                return MultiplayerToolsCore.ServerName;

            string playerName = GameInfo.Instance?.PlayerName;
            return string.IsNullOrWhiteSpace(playerName) ? "Sledding Lobby" : playerName + "'s Lobby";
        }

        private static void EnsureLobbyNameInput(UICreateLobby createLobby)
        {
            dynamic lobbyNameText = createLobby.lobbyNameText;
            dynamic passwordInput = createLobby.passwordInputField;
            if (lobbyNameText == null || passwordInput == null)
                return;

            Transform layoutParent = lobbyNameText.transform.parent;
            bool needsInput = _customLobbyNameInput == null || _customLobbyNameInput.transform.parent != layoutParent;
            if (!needsInput)
            {
                UILib.Assume(_customLobbyNameInput).Show();
                UILib.Assume((GameObject)lobbyNameText.gameObject).Hide();
                return;
            }

            int labelIndex = lobbyNameText.transform.GetSiblingIndex();
            TMP_InputField input = UILib.CloneInputField(
                (TMP_InputField)passwordInput,
                layoutParent,
                name: "CustomLobbyNameInput",
                siblingIndex: labelIndex);
            if (input == null)
                return;

            UILib.SetRect(input, sizeDelta: new Vector2(473.99f, 47.04f), scale: Vector3.one);
            input.characterLimit = 64;
            input.lineLimit = 1;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.inputType = TMP_InputField.InputType.Standard;
            input.interactable = true;
            UILib.SetInputTextStyle(input, 18f, autoSize: true, minFontSize: 12f);

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
                UILib.SetText(placeholderText, "Lobby name...");

            UILib.Assume((GameObject)lobbyNameText.gameObject).Hide();
            UILib.Assume(input).Show();
            _customLobbyNameInput = input;
        }

        private static void EnsureMaxPlayerSlider(UICreateLobby createLobby)
        {
            MySliderUI slider = createLobby.maxPlayersSlider;
            if (slider == null || slider.slider == null)
                return;

            bool needsListener = _maxPlayerSlider != slider;
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

        private static void HideCreateLobbyCloseButton(UICreateLobby createLobby)
        {
            Transform createLobbyRoot = FindCreateLobbyRoot(createLobby?.transform);
            Transform searchRoot = createLobbyRoot ?? createLobby?.transform;
            if (searchRoot == null)
                return;

            HideButton(searchRoot.Find("Panels/Lobby Settings (mini)/ButtonContainer/(Button) Close Menu")?.GetComponent<Button>());
            HideButton(searchRoot.Find("ButtonContainer/(Button) Close Menu")?.GetComponent<Button>());

            foreach (Button button in searchRoot.GetComponentsInChildren<Button>(true))
            {
                if (button == null)
                    continue;

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string labelText = label != null ? label.text : string.Empty;
                if (button.name.Contains("Close Menu") || button.name.Contains("Close") || labelText == "X" || labelText == "×")
                    HideButton(button);
            }
        }

        private static void HideButton(Button button)
        {
            if (button == null)
                return;

            button.interactable = false;
            button.gameObject.SetActive(false);
        }

        private static void EnsureLobbyOptionBindings(UICreateLobby createLobby)
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
            publicLobbyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
            {
                MultiplayerToolsCore.SetIsPublicLobby(isOn);
            }));
            (MultiplayerToolsCore.IsPublicLobby ? publicLobbyToggle : publicLobbyToggle.group.m_Toggles[0]).isOn = true;

            Toggle passwordProtectedToggle = createLobby.passwordProtectedToggle;
            passwordProtectedToggle.isOn = MultiplayerToolsCore.IsPasswordProtected;
            passwordProtectedToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
            {
                MultiplayerToolsCore.SetIsPasswordProtected(isOn);
            }));

            TMP_InputField passwordInputField = createLobby.passwordInputField;
            passwordInputField.text = MultiplayerToolsCore.LobbyPassword;
            passwordInputField.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
            {
                MultiplayerToolsCore.SetLobbyPassword(text);
            }));

            Toggle peacefulModeToggle = createLobby.peacefulModeToggle;
            peacefulModeToggle.isOn = MultiplayerToolsCore.IsPeacefulMode;
            peacefulModeToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
            {
                MultiplayerToolsCore.SetIsPeacefulMode(isOn);
            }));

            textChatOnlyToggle.isOn = MultiplayerToolsCore.IsTextChatOnly;
            textChatOnlyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
            {
                MultiplayerToolsCore.SetIsTextChatOnly(isOn);
            }));

            EnsureGuestBangCommandsToggle(textChatOnlyToggle);
            _togglesInitialized = true;
        }

        private static void EnsureGuestBangCommandsToggle(Toggle textChatOnlyToggle)
        {
            if (_enableGuestBangCommandsToggle != null)
            {
                _enableGuestBangCommandsToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                return;
            }

            Toggle toggle = UILib.CloneToggle(
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

            UILib.Assume(toggle).Show();
            _enableGuestBangCommandsToggle = toggle;
        }

        [HarmonyPatch(typeof(LobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void LobbyManager_CreateLobby_Prefix(
            ref string lobbyName,
            ref int maxPlayers,
            ref bool isPublic,
            ref bool proximityChatEnabled,
            ref bool passwordProtected,
            ref string password,
            ref bool peacefulMode,
            ref string platform,
            ref string region,
            ref bool crossplayEnabled)
        {
            lobbyName = GetInputLobbyName();

            int selectedMaxPlayers = _maxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(_maxPlayerSlider.slider.value)
                : maxPlayers;
            if (selectedMaxPlayers <= 0)
                selectedMaxPlayers = MultiplayerToolsCore.ServerCapacity;

            maxPlayers = Mathf.Clamp(selectedMaxPlayers, 1, 64);
            MultiplayerToolsCore.SetServerCapacity(maxPlayers);
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void EOSLobbyManager_CreateLobby_Prefix(EOSLobbyManager __instance, Lobby lobbyProperties)
        {
            if (lobbyProperties == null)
                return;

            int maxPlayers = _maxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(_maxPlayerSlider.slider.value)
                : MultiplayerToolsCore.ServerCapacity;
            lobbyProperties.MaxNumLobbyMembers = (uint)Mathf.Clamp(maxPlayers, 1, 64);
        }

        [HarmonyPatch(typeof(UIMainMenu), "OnEnable")]
        [HarmonyPostfix]
        private static void UIMainMenu_OnEnable_Postfix(UIMainMenu __instance)
        {
            void SetupCreateLobbyPanel(UICreateLobby createLobby)
            {
                if (createLobby == null)
                    return;

                UILib.CaptureDefaultsFrom(createLobby.transform, overwriteExisting: false);
                EnsureLobbyNameInput(createLobby);
                EnsureMaxPlayerSlider(createLobby);
                HideCreateLobbyCloseButton(createLobby);
                EnsureLobbyOptionBindings(createLobby);
            }

            IEnumerator ActivateEmbeddedCreateLobbyForNextFrames(Transform menuPanelRoot)
            {
                // Vanilla disables UI_CreateLobby/Panels shortly after OnEnable.
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return null;

                    Transform refreshedRoot = FindCreateLobbyRoot(menuPanelRoot);
                    if (refreshedRoot == null)
                        continue;

                    SetupCreateLobbyPanel(ActivateEmbeddedCreateLobby(refreshedRoot, showPanel: true));
                }
            }

            try
            {
                Transform menuPanel = ConfigureMainMenu(__instance);
                UILib.CaptureDefaultsFrom(menuPanel, overwriteExisting: false);
                Transform createLobbyRoot = FindCreateLobbyRoot(menuPanel);
                if (createLobbyRoot == null)
                {
                    Debug.LogWarning("[MultiplayerTools] Could not embed create lobby UI: UI_CreateLobby root was not found.");
                    return;
                }

                UILib.Assume(createLobbyRoot)
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

        private static Transform ConfigureMainMenu(UIMainMenu mainMenu)
        {
            if (mainMenu == null || mainMenu.hostButton == null)
                return mainMenu?.transform.Find("Panel") ?? mainMenu?.transform;

            Transform hostRow = mainMenu.hostButton.transform.parent;
            Transform menuLayout = hostRow?.parent;
            if (menuLayout == null)
                return mainMenu.transform;

            Transform oldQuickHostRow = menuLayout.Find("horizontal layout (quick host)");
            if (oldQuickHostRow != null)
                UILib.Assume(oldQuickHostRow).DestroyImmediate();

            UILib.Assume(mainMenu.hostButton).Hide();

            Button joinButton = mainMenu.joinButton ?? GameObject.Find("(Button) JOIN")?.GetComponent<Button>();
            if (joinButton != null)
            {
                UILib.Assume(joinButton)
                    .SiblingIndex(0)
                    .Show()
                    .Layout(flexibleWidth: 1f, preferredWidth: -1f);
            }

            Button quitButton = mainMenu.quitButton ?? GameObject.Find("(Button) Quit")?.GetComponent<Button>();
            UILib.SetButtonColors(quitButton, new Color(0.867f, 0.298f, 0.298f, 1f), new Color(0.298f, 0f, 0f, 1f));

            HorizontalLayoutGroup hostRowLayout = hostRow.GetComponent<HorizontalLayoutGroup>();
            if (hostRowLayout != null)
            {
                hostRowLayout.childControlWidth = true;
                hostRowLayout.childForceExpandWidth = true;
            }

            UILib.Assume(menuLayout)
                .Scale(new Vector3(0.78f, 0.78f, 1f))
                .Move(new Vector2(295f, 0f));

            return menuLayout.parent ?? mainMenu.transform;
        }

        private static Transform FindCreateLobbyRoot(Transform mainMenuRoot)
        {
            for (Transform current = mainMenuRoot; current != null; current = current.parent)
            {
                if (current.name == "UI_CreateLobby")
                    return current;
            }

            Transform root = mainMenuRoot?.Find("UI_CreateLobby");
            if (root != null)
                return root;

            return UiReferenceController.Instance != null
                ? UiReferenceController.Instance.createLobby.panel?.transform
                : null;
        }

        private static UICreateLobby ActivateEmbeddedCreateLobby(Transform createLobbyRoot, bool showPanel)
        {
            if (createLobbyRoot == null)
                return null;

            UILib.Assume(createLobbyRoot).Show();
            UILib.SetChildrenActive(createLobbyRoot, true, skipNameContains: "BackgroundFade");
            for (int i = 0; i < createLobbyRoot.childCount; i++)
            {
                Transform child = createLobbyRoot.GetChild(i);
                if (child == null || child.name.Contains("BackgroundFade"))
                    continue;

                if (child.name.Contains("Panels"))
                    UILib.SetChildrenActive(child, true, skipNameContains: "Editor");
            }

            UICreateLobby createLobby = createLobbyRoot.GetComponent<UICreateLobby>()
                ?? createLobbyRoot.GetComponentInChildren<UICreateLobby>(true);
            if (createLobby != null)
            {
                UILib.ActivatePathToRoot(createLobby.transform, createLobbyRoot);

                if (showPanel)
                    createLobby.ShowPanel();
            }

            UILib.SetCanvasGroups(createLobbyRoot, alpha: 1f, interactable: true, blocksRaycasts: true);

            return createLobby;
        }

    }
}
