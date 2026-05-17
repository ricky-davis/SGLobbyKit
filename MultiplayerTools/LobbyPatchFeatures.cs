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
        public static MySliderUI MaxPlayerSlider;
        public static TMP_InputField CustomLobbyNameInput;
        public static Toggle EnableGuestBangCommandsToggle;
        public static bool TogglesInitialized = false;

        public static string GetInputLobbyName()
        {
            if (CustomLobbyNameInput != null)
            {
                var customName = CustomLobbyNameInput.text;
                if (!string.IsNullOrWhiteSpace(customName))
                    return customName;
            }

            if (!string.IsNullOrWhiteSpace(MultiplayerToolsCore.ServerName))
                return MultiplayerToolsCore.ServerName;

            string playerName = GameInfo.Instance?.PlayerName;
            return string.IsNullOrWhiteSpace(playerName) ? "Sledding Lobby" : playerName + "'s Lobby";
        }

        public static void SetupCustomLobbyNameInput(UICreateLobby instance)
        {
            if (instance == null)
                return;

            dynamic label = instance.lobbyNameText;
            dynamic passwordInput = instance.passwordInputField;
            if (label == null || passwordInput == null)
                return;

            var layoutParent = label.transform.parent;

            // Create the Server Name Input field from the Password Input field
            bool needsLobbyNameInput = CustomLobbyNameInput == null || CustomLobbyNameInput.transform.parent != layoutParent;
            if (needsLobbyNameInput)
            {
                var labelIndex = label.transform.GetSiblingIndex();

                var cloneGO = Object.Instantiate(passwordInput.gameObject, layoutParent);
                cloneGO.name = "CustomLobbyNameInput";
                cloneGO.transform.SetSiblingIndex(labelIndex);

                var cloneInput = cloneGO.GetComponent<Il2CppTMPro.TMP_InputField>();
                var cloneRT = cloneGO.GetComponent<RectTransform>();
                
                cloneRT.localScale = Vector3.one;
                cloneRT.sizeDelta = new Vector2(473.99f, 47.04f);
                cloneInput.characterLimit = 64;
                cloneInput.lineLimit = 1;
                cloneInput.lineType = Il2CppTMPro.TMP_InputField.LineType.SingleLine;
                cloneInput.contentType = Il2CppTMPro.TMP_InputField.ContentType.Standard;
                cloneInput.inputType = Il2CppTMPro.TMP_InputField.InputType.Standard;
                cloneInput.interactable = true;

                dynamic gameInfo = GameInfo.Instance;
                cloneInput.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
                {
                    label.text = text;
                    MultiplayerToolsCore.SetServerName(text);
                }));
                cloneInput.text = !string.IsNullOrWhiteSpace(MultiplayerToolsCore.ServerName)
                    ? MultiplayerToolsCore.ServerName
                    : $"{gameInfo.PlayerName}'s Lobby";

                var placeholderText = cloneInput.placeholder?.GetComponent<Il2CppTMPro.TMP_Text>();
                if (placeholderText != null)
                {
                    var loc = placeholderText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                    if (loc != null)
                        Object.Destroy(loc);
                    placeholderText.text = "Lobby name...";
                }

                label.gameObject.SetActive(false);
                cloneGO.SetActive(true);
                CustomLobbyNameInput = cloneInput;
            }
            else
            {
                CustomLobbyNameInput.gameObject.SetActive(true);
                label.gameObject.SetActive(false);
            }
            // Setup the Max Players slider
            if (MaxPlayerSlider == null || MaxPlayerSlider != instance.maxPlayersSlider)
            {
                MaxPlayerSlider = instance.maxPlayersSlider;
                MaxPlayerSlider.slider.maxValue = 64f;
                MaxPlayerSlider.slider.value = Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64);
                MaxPlayerSlider.UpdateSliderValueDisplay();
                MaxPlayerSlider.slider.onValueChanged.AddListener((UnityEngine.Events.UnityAction<float>)((value) =>
                {
                    MultiplayerToolsCore.SetServerCapacity(Mathf.RoundToInt(value));
                }));
            }
            Toggle textChatOnlyToggle = instance.textChatOnlyToggle;
            if (EnableGuestBangCommandsToggle == null)
            {
                TogglesInitialized = false;
            }
            else if (EnableGuestBangCommandsToggle.transform.parent != textChatOnlyToggle.transform.parent)
            {
                EnableGuestBangCommandsToggle = null;
                TogglesInitialized = false;
            }

            if (TogglesInitialized == false)
            {
                Toggle publicLobbyToggle = instance.publicLobbyToggle;
                publicLobbyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPublicLobby(isOn);
                }));
                (MultiplayerToolsCore.IsPublicLobby ? publicLobbyToggle : publicLobbyToggle.group.m_Toggles[0]).isOn = true;


                Toggle passwordProtectedToggle = instance.passwordProtectedToggle;
                passwordProtectedToggle.isOn = MultiplayerToolsCore.IsPasswordProtected;
                passwordProtectedToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPasswordProtected(isOn);
                }));


                TMP_InputField passwordInputField = instance.passwordInputField;
                passwordInputField.text = MultiplayerToolsCore.LobbyPassword;
                passwordInputField.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
                {
                    MultiplayerToolsCore.SetLobbyPassword(text);
                }));


                Toggle peacefulModeToggle = instance.peacefulModeToggle;
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


                if (EnableGuestBangCommandsToggle == null)
                {
                    var cloneGO = Object.Instantiate(textChatOnlyToggle.gameObject, textChatOnlyToggle.transform.parent);
                    cloneGO.name = "EnableGuestBangCommandsToggle";
                    cloneGO.transform.SetSiblingIndex(textChatOnlyToggle.transform.GetSiblingIndex() + 1);

                    var cloneToggle = cloneGO.GetComponent<Toggle>();
                    if (cloneToggle != null)
                    {
                        cloneToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                        cloneToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                        {
                            MultiplayerToolsCore.SetEnableGuestBangCommands(isOn);
                        }));

                        var toggleLabel = cloneGO.GetComponentInChildren<TMP_Text>(true);
                        if (toggleLabel != null)
                        {
                            var loc = toggleLabel.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                            if (loc != null)
                                Object.Destroy(loc);
                            toggleLabel.text = "Enable Guest Bang Commands";
                        }

                        cloneGO.SetActive(true);
                        EnableGuestBangCommandsToggle = cloneToggle;
                    }
                }
                else
                {
                    EnableGuestBangCommandsToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                }
                TogglesInitialized = true;
            }
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
            lobbyName = LobbyPatchFeatures.GetInputLobbyName();
            maxPlayers = MaxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(MaxPlayerSlider.slider.value)
                : Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64);
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void EOSLobbyManager_CreateLobby_Prefix(EOSLobbyManager __instance, Lobby lobbyProperties)
        {
            if (lobbyProperties == null)
                return;

            int maxPlayers = MaxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(MaxPlayerSlider.slider.value)
                : MultiplayerToolsCore.ServerCapacity;
            lobbyProperties.MaxNumLobbyMembers = (uint)Mathf.Clamp(maxPlayers, 1, 64);
        }

        [HarmonyPatch(typeof(UICreateLobby), "ShowPanel")]
        [HarmonyPostfix]
        private static void UICreateLobby_ShowPanel_Postfix(UICreateLobby __instance)
        {
            SetupCustomLobbyNameInput(__instance);
        }

        [HarmonyPatch(typeof(UIMainMenu), "OnEnable")]
        [HarmonyPostfix]
        private static void UIMainMenu_OnEnable_Postfix(UIMainMenu __instance)
        {
            try
            {
                if (__instance == null || __instance.hostButton == null)
                    return;

                Transform hostRow = __instance.hostButton.transform.parent;
                if (hostRow == null)
                    return;

                Transform menuLayout = hostRow.parent;
                Transform oldQuickHostRow = menuLayout?.Find("horizontal layout (quick host)");
                if (oldQuickHostRow != null)
                    Object.DestroyImmediate(oldQuickHostRow.gameObject);

                __instance.hostButton.gameObject.SetActive(false);

                Button joinButton = __instance.joinButton ?? GameObject.Find("(Button) JOIN")?.GetComponent<Button>();
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

                Button quitButton = __instance.quitButton ?? GameObject.Find("(Button) Quit")?.GetComponent<Button>();
                Image quitButtonImage = quitButton != null ? quitButton.GetComponent<Image>() : null;
                if (quitButtonImage != null)
                    quitButtonImage.color = new Color(0.867f, 0.298f, 0.298f, 1f);
                Shadow quitButtonImageShadow = quitButton != null ? quitButton.GetComponent<Shadow>() : null;
                if (quitButtonImageShadow != null)
                    quitButtonImageShadow.effectColor = new Color(0.298f, 0f, 0f, 1f);

                HorizontalLayoutGroup hostRowLayout = hostRow.GetComponent<HorizontalLayoutGroup>();
                if (hostRowLayout != null)
                {
                    hostRowLayout.childControlWidth = true;
                    hostRowLayout.childForceExpandWidth = true;
                }

                RectTransform menuLayoutRect = menuLayout?.GetComponent<RectTransform>();
                if (menuLayoutRect != null)
                {
                    menuLayoutRect.localScale = new Vector3(0.78f, 0.78f, 1f);
                    menuLayoutRect.anchoredPosition = new Vector2(295f, 0f);
                }

                Transform visibleMainMenuPanel = menuLayout?.parent;
                Transform embedParent = visibleMainMenuPanel?.parent ?? __instance.transform;

                Transform createLobbyRoot = embedParent?.Find("UI_CreateLobby");
                UiReferenceController uiInstance = UiReferenceController.Instance;
                GameObject createLobbyPanel = uiInstance != null ? uiInstance.createLobby.panel : null;

                if (createLobbyRoot == null && createLobbyPanel != null)
                    createLobbyRoot = createLobbyPanel.transform;

                UICreateLobby createLobby = createLobbyRoot?.GetComponent<UICreateLobby>()
                    ?? createLobbyRoot?.GetComponentInChildren<UICreateLobby>(true)
                    ?? createLobbyPanel?.GetComponent<UICreateLobby>()
                    ?? createLobbyPanel?.GetComponentInParent<UICreateLobby>()
                    ?? createLobbyPanel?.GetComponentInChildren<UICreateLobby>(true);

                if (createLobbyRoot == null && createLobby != null)
                    createLobbyRoot = createLobby.transform;

                if (createLobbyRoot == null)
                {
                    Debug.LogWarning("[MultiplayerTools] Could not embed create lobby UI: UI_CreateLobby root was not found.");
                    return;
                }

                for (Transform parent = createLobbyRoot.parent; parent != null && parent != embedParent; parent = parent.parent)
                {
                    if (parent.name.Contains("CreateLobby") || parent.GetComponent<UICreateLobby>() != null)
                        createLobbyRoot = parent;
                }

                if (createLobby == null)
                    createLobby = createLobbyRoot.GetComponent<UICreateLobby>() ?? createLobbyRoot.GetComponentInChildren<UICreateLobby>(true);
                if (createLobbyPanel == null && createLobby != null)
                    createLobbyPanel = createLobby.gameObject;

                GameObject createLobbyRootObject = createLobbyRoot.gameObject;
                if (embedParent != null && createLobbyRoot.parent != embedParent)
                    createLobbyRoot.SetParent(embedParent, false);
                createLobbyRoot.SetAsLastSibling();

                RectTransform createLobbyRect = createLobbyRootObject.GetComponent<RectTransform>();
                if (createLobbyRect != null)
                {
                    createLobbyRect.anchorMin = new Vector2(0.5f, 0.5f);
                    createLobbyRect.anchorMax = new Vector2(0.5f, 0.5f);
                    createLobbyRect.pivot = new Vector2(0.5f, 0.5f);
                    createLobbyRect.localScale = new Vector3(0.9f, 0.9f, 1f);
                    createLobbyRect.anchoredPosition = new Vector2(-295f, 0f);
                }

                createLobbyRootObject.SetActive(true);
                Transform panelsContainer = null;
                for (int i = 0; i < createLobbyRoot.childCount; i++)
                {
                    Transform child = createLobbyRoot.GetChild(i);
                    if (child == null || child.name.Contains("BackgroundFade"))
                        continue;

                    child.gameObject.SetActive(true);
                    if (child.name.Contains("Panels"))
                        panelsContainer = child;
                }

                if (panelsContainer != null)
                {
                    for (int i = 0; i < panelsContainer.childCount; i++)
                    {
                        Transform child = panelsContainer.GetChild(i);
                        if (child != null && !child.name.Contains("Editor"))
                            child.gameObject.SetActive(true);
                    }
                }

                if (createLobbyPanel != null)
                {
                    for (Transform target = createLobbyPanel.transform; target != null; target = target.parent)
                    {
                        target.gameObject.SetActive(true);
                        if (target == createLobbyRoot)
                            break;
                    }
                }

                if (createLobby != null)
                {
                    for (Transform target = createLobby.transform; target != null; target = target.parent)
                    {
                        target.gameObject.SetActive(true);
                        if (target == createLobbyRoot)
                            break;
                    }
                }

                createLobby?.ShowPanel();
                SetupCustomLobbyNameInput(createLobby);

                foreach (CanvasGroup createLobbyCanvas in createLobbyRootObject.GetComponentsInChildren<CanvasGroup>(true))
                {
                    createLobbyCanvas.alpha = 1f;
                    createLobbyCanvas.interactable = true;
                    createLobbyCanvas.blocksRaycasts = true;
                }

                if (visibleMainMenuPanel != null)
                {
                    visibleMainMenuPanel.gameObject.SetActive(true);
                    CanvasGroup mainMenuCanvas = visibleMainMenuPanel.GetComponent<CanvasGroup>();
                    if (mainMenuCanvas != null)
                    {
                        mainMenuCanvas.alpha = 1f;
                        mainMenuCanvas.interactable = true;
                        mainMenuCanvas.blocksRaycasts = true;
                    }
                }

                Debug.Log($"[MultiplayerTools] Embedded create lobby UI: {createLobbyRootObject.name} under {embedParent?.name ?? "null"}");
                MelonCoroutines.Start(ActivateEmbeddedCreateLobbyForNextFrames(embedParent));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] Failed to embed create lobby UI: {ex}");
            }
        }

        private static IEnumerator ActivateEmbeddedCreateLobbyForNextFrames(Transform mainMenuRoot)
        {
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;

                Transform createLobbyRoot = mainMenuRoot?.Find("UI_CreateLobby");
                if (createLobbyRoot == null)
                    continue;

                createLobbyRoot.gameObject.SetActive(true);
                for (int i = 0; i < createLobbyRoot.childCount; i++)
                {
                    Transform child = createLobbyRoot.GetChild(i);
                    if (child == null || child.name.Contains("BackgroundFade"))
                        continue;

                    child.gameObject.SetActive(true);

                    if (!child.name.Contains("Panels"))
                        continue;

                    for (int j = 0; j < child.childCount; j++)
                    {
                        Transform panelChild = child.GetChild(j);
                        if (panelChild == null || panelChild.name.Contains("Editor"))
                            continue;

                        panelChild.gameObject.SetActive(true);
                    }
                }

                UICreateLobby createLobby = createLobbyRoot.GetComponent<UICreateLobby>()
                    ?? createLobbyRoot.GetComponentInChildren<UICreateLobby>(true);
                if (createLobby != null)
                    SetupCustomLobbyNameInput(createLobby);
            }
        }

        [HarmonyPatch(typeof(LobbyManager), "OnCreateLobbyComplete")]
        [HarmonyPostfix]
        private static void LobbyManager_OnCreateLobbyComplete_Postfix()
        {
            HideEmbeddedCreateLobby();
        }

        private static void HideEmbeddedCreateLobby()
        {
            foreach (UICreateLobby createLobby in Resources.FindObjectsOfTypeAll<UICreateLobby>())
            {
                if (createLobby == null)
                    continue;

                Transform createLobbyRoot = createLobby.transform;
                for (Transform parent = createLobbyRoot.parent; parent != null; parent = parent.parent)
                {
                    if (parent.name.Contains("CreateLobby") || parent.GetComponent<UICreateLobby>() != null)
                        createLobbyRoot = parent;
                }

                createLobbyRoot.gameObject.SetActive(false);
                break;
            }
        }

        [HarmonyPatch(typeof(UIMainMenu), "OnDisable")]
        [HarmonyPostfix]
        private static void UIMainMenu_OnDisable_Postfix()
        {
            HideEmbeddedCreateLobby();
        }

    }
}
