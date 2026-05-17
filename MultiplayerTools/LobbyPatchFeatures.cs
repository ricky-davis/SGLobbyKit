using System.Collections;
using HarmonyLib;
using Il2Cpp;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;
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
                _customLobbyNameInput.gameObject.SetActive(true);
                lobbyNameText.gameObject.SetActive(false);
                return;
            }

            int labelIndex = lobbyNameText.transform.GetSiblingIndex();
            GameObject inputObject = Object.Instantiate(passwordInput.gameObject, layoutParent);
            inputObject.name = "CustomLobbyNameInput";
            inputObject.transform.SetSiblingIndex(labelIndex);

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            if (input == null || inputRect == null)
                return;

            inputRect.localScale = Vector3.one;
            inputRect.sizeDelta = new Vector2(473.99f, 47.04f);
            input.characterLimit = 64;
            input.lineLimit = 1;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.inputType = TMP_InputField.InputType.Standard;
            input.interactable = true;

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
            {
                var loc = placeholderText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                if (loc != null)
                    Object.Destroy(loc);
                placeholderText.text = "Lobby name...";
            }

            lobbyNameText.gameObject.SetActive(false);
            inputObject.SetActive(true);
            _customLobbyNameInput = input;
        }

        private static void EnsureMaxPlayerSlider(UICreateLobby createLobby)
        {
            MySliderUI slider = createLobby.maxPlayersSlider;
            if (slider == null || slider.slider == null)
                return;

            _maxPlayerSlider = slider;
            _maxPlayerSlider.slider.maxValue = 64f;
            _maxPlayerSlider.slider.SetValueWithoutNotify(Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64));
            _maxPlayerSlider.UpdateSliderValueDisplay();
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

            GameObject toggleObject = Object.Instantiate(textChatOnlyToggle.gameObject, textChatOnlyToggle.transform.parent);
            toggleObject.name = "EnableGuestBangCommandsToggle";
            toggleObject.transform.SetSiblingIndex(textChatOnlyToggle.transform.GetSiblingIndex() + 1);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            if (toggle == null)
                return;

            toggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
            toggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
            {
                MultiplayerToolsCore.SetEnableGuestBangCommands(isOn);
            }));

            TMP_Text toggleLabel = toggleObject.GetComponentInChildren<TMP_Text>(true);
            if (toggleLabel != null)
            {
                var loc = toggleLabel.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                if (loc != null)
                    Object.Destroy(loc);
                toggleLabel.text = "Enable Guest Bang Commands";
            }

            toggleObject.SetActive(true);
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

                EnsureLobbyNameInput(createLobby);
                EnsureMaxPlayerSlider(createLobby);
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
                Transform createLobbyRoot = FindCreateLobbyRoot(menuPanel);
                if (createLobbyRoot == null)
                {
                    Debug.LogWarning("[MultiplayerTools] Could not embed create lobby UI: UI_CreateLobby root was not found.");
                    return;
                }

                if (menuPanel != null && createLobbyRoot.parent != menuPanel)
                    createLobbyRoot.SetParent(menuPanel, false);
                createLobbyRoot.SetAsLastSibling();

                RectTransform createLobbyRect = createLobbyRoot.GetComponent<RectTransform>();
                if (createLobbyRect != null)
                {
                    createLobbyRect.anchorMin = new Vector2(0.5f, 0.5f);
                    createLobbyRect.anchorMax = new Vector2(0.5f, 0.5f);
                    createLobbyRect.pivot = new Vector2(0.5f, 0.5f);
                    createLobbyRect.localScale = new Vector3(0.9f, 0.9f, 1f);
                    createLobbyRect.anchoredPosition = new Vector2(-295f, 0f);
                }

                //SetupCreateLobbyPanel(ActivateEmbeddedCreateLobby(createLobbyRoot, showPanel: true));

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
                Object.DestroyImmediate(oldQuickHostRow.gameObject);

            mainMenu.hostButton.gameObject.SetActive(false);

            Button joinButton = mainMenu.joinButton ?? GameObject.Find("(Button) JOIN")?.GetComponent<Button>();
            if (joinButton != null)
            {
                joinButton.transform.SetSiblingIndex(0);
                joinButton.gameObject.SetActive(true);

                LayoutElement joinLayout = joinButton.GetComponent<LayoutElement>();
                if (joinLayout == null)
                    joinLayout = joinButton.gameObject.AddComponent<LayoutElement>();
                joinLayout.flexibleWidth = 1f;
                joinLayout.preferredWidth = -1f;
            }

            Button quitButton = mainMenu.quitButton ?? GameObject.Find("(Button) Quit")?.GetComponent<Button>();
            Image quitImage = quitButton != null ? quitButton.GetComponent<Image>() : null;
            if (quitImage != null)
                quitImage.color = new Color(0.867f, 0.298f, 0.298f, 1f);

            Shadow quitShadow = quitButton != null ? quitButton.GetComponent<Shadow>() : null;
            if (quitShadow != null)
                quitShadow.effectColor = new Color(0.298f, 0f, 0f, 1f);

            HorizontalLayoutGroup hostRowLayout = hostRow.GetComponent<HorizontalLayoutGroup>();
            if (hostRowLayout != null)
            {
                hostRowLayout.childControlWidth = true;
                hostRowLayout.childForceExpandWidth = true;
            }

            RectTransform menuLayoutRect = menuLayout.GetComponent<RectTransform>();
            if (menuLayoutRect != null)
            {
                menuLayoutRect.localScale = new Vector3(0.78f, 0.78f, 1f);
                menuLayoutRect.anchoredPosition = new Vector2(295f, 0f);
            }

            return menuLayout.parent ?? mainMenu.transform;
        }

        private static Transform FindCreateLobbyRoot(Transform mainMenuRoot)
        {
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

            createLobbyRoot.gameObject.SetActive(true);
            for (int i = 0; i < createLobbyRoot.childCount; i++)
            {
                Transform child = createLobbyRoot.GetChild(i);
                if (child == null || child.name.Contains("BackgroundFade"))
                    continue;

                child.gameObject.SetActive(true);
                if (child.name.Contains("Panels"))
                {
                    for (int j = 0; j < child.childCount; j++)
                    {
                        Transform panelChild = child.GetChild(j);
                        if (panelChild != null && !panelChild.name.Contains("Editor"))
                            panelChild.gameObject.SetActive(true);
                    }
                }
            }

            UICreateLobby createLobby = createLobbyRoot.GetComponent<UICreateLobby>()
                ?? createLobbyRoot.GetComponentInChildren<UICreateLobby>(true);
            if (createLobby != null)
            {
                for (Transform target = createLobby.transform; target != null; target = target.parent)
                {
                    target.gameObject.SetActive(true);
                    if (target == createLobbyRoot)
                        break;
                }

                if (showPanel)
                    createLobby.ShowPanel();
            }

            foreach (CanvasGroup canvasGroup in createLobbyRoot.GetComponentsInChildren<CanvasGroup>(true))
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            return createLobby;
        }

    }
}
