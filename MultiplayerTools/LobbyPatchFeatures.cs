using HarmonyLib;
using Il2Cpp;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Binary;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.UI;
using Il2CppTMPro;
using Il2Cpp_Scripts.UI.Popups;

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

        private static void QuickHost()
        {
            if (MultiplayerToolsCore.IsPasswordProtected && string.IsNullOrWhiteSpace(MultiplayerToolsCore.LobbyPassword))
            {
                UiReferenceController uiInstance = UiReferenceController.Instance;
                if (uiInstance?.createLobby != null)
                {
                    uiInstance.OpenMenu(uiInstance.createLobby);
                    UICreateLobby createLobby = uiInstance.createLobby.panel?.GetComponent<UICreateLobby>();
                    if (createLobby?.passwordInputField != null)
                    {
                        // OpenMenu has already shown the panel; select the missing field immediately.
                        createLobby.passwordInputField.Select();
                        createLobby.passwordInputField.ActivateInputField();
                    }
                }
                else
                {
                    Debug.LogWarning("[MultiplayerTools] Quick Host failed: create lobby UI unavailable.");
                }

                PopupUiManager.Instance?.ShowPopup(PopupMessage.PasswordFieldIsEmpty, "", PopupType.Error);
                return;
            }

            GameInfo gameInfo = GameInfo.Instance;
            LobbyManager lobbyManager = gameInfo?.LobbyManager;
            if (lobbyManager == null)
            {
                Debug.LogWarning("[MultiplayerTools] Quick Host failed: lobby manager unavailable.");
                return;
            }

            string password = MultiplayerToolsCore.IsPasswordProtected ? MultiplayerToolsCore.LobbyPassword : string.Empty;
            string region = PlayerPrefsManager.Instance?.playerSavedSettings?.playerRegion;
            lobbyManager.CreateLobby(
                GetInputLobbyName(),
                Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64),
                MultiplayerToolsCore.IsPublicLobby,
                !MultiplayerToolsCore.IsTextChatOnly,
                MultiplayerToolsCore.IsPasswordProtected,
                password,
                MultiplayerToolsCore.IsPeacefulMode,
                LobbyUtilities.PlatformString(),
                string.IsNullOrEmpty(region) ? "default" : region,
                false);
        }

        public static void SetupCustomLobbyNameInput(UICreateLobby instance)
        {
            if (instance == null)
                return;
            
            // Create the Server Name Input field from the Password Input field
            if (CustomLobbyNameInput == null)
            {
                dynamic label = instance.lobbyNameText;
                dynamic passwordInput = instance.passwordInputField;
                var layoutParent = label.transform.parent;
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
            // Setup the Max Players slider
            if (MaxPlayerSlider == null){
                MaxPlayerSlider = instance.maxPlayersSlider;
                MaxPlayerSlider.slider.maxValue = 64f;
                MaxPlayerSlider.slider.value = Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64);
                MaxPlayerSlider.UpdateSliderValueDisplay();
                MaxPlayerSlider.slider.onValueChanged.AddListener((UnityEngine.Events.UnityAction<float>)((value) =>
                {
                    MultiplayerToolsCore.SetServerCapacity(Mathf.RoundToInt(value));
                }));
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


                Toggle textChatOnlyToggle = instance.textChatOnlyToggle;
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

                        var label = cloneGO.GetComponentInChildren<TMP_Text>(true);
                        if (label != null)
                        {
                            var loc = label.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                            if (loc != null)
                                Object.Destroy(loc);
                            label.text = "Enable Guest Bang Commands";
                        }

                        cloneGO.SetActive(true);
                        EnableGuestBangCommandsToggle = cloneToggle;
                    }
                }
                else
                {
                    EnableGuestBangCommandsToggle.isOn = MultiplayerToolsCore.EnableGuestBangCommands;
                }
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

        [HarmonyPatch(typeof(UIMainMenu), "OnHostClicked")]
        [HarmonyPrefix]
        private static bool UIMainMenu_OnHostClicked_Prefix(UIMainMenu __instance)
        {
            UiReferenceController uiInstance = UiReferenceController.Instance;
            uiInstance.OpenMenu(uiInstance.createLobby);
            return false;
        }

        [HarmonyPatch(typeof(UIMainMenu), "OnEnable")]
        [HarmonyPostfix]
        private static void UIMainMenu_OnEnable_Postfix(UIMainMenu __instance)
        {
            try
            {
                string lobbyName = GetInputLobbyName();
                if (lobbyName.Length > 18)
                    lobbyName = lobbyName.Substring(0, 15) + "...";

                int maxPlayers = MaxPlayerSlider?.slider != null
                    ? Mathf.RoundToInt(MaxPlayerSlider.slider.value)
                    : Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64);
                string leftSettings = $"Lobby Name: {lobbyName}\nMax Players: {maxPlayers}\nPublic/Invite: {(MultiplayerToolsCore.IsPublicLobby ? "Public" : "Invite")}";
                string rightSettings = $"Requires Password: {(MultiplayerToolsCore.IsPasswordProtected ? "Yes" : "No")}\nTextOnly: {(MultiplayerToolsCore.IsTextChatOnly ? "Yes" : "No")}\nPeaceful: {(MultiplayerToolsCore.IsPeacefulMode ? "Yes" : "No")}";

                GameObject buttonObject = GameObject.Find("(Button) Quick Host");
                if (buttonObject == null)
                {
                    if (__instance == null || __instance.hostButton == null)
                        return;

                    Transform originalRow = __instance.hostButton.transform.parent;
                    if (originalRow == null || originalRow.parent == null || originalRow.parent.Find("horizontal layout (quick host)") != null)
                        return;

                    GameObject rowClone = Object.Instantiate(originalRow.gameObject, originalRow.parent);
                    if (rowClone == null)
                        return;

                    rowClone.name = "horizontal layout (quick host)";
                    rowClone.transform.SetSiblingIndex(originalRow.GetSiblingIndex() + 1);

                    int buttonIndex = Mathf.Clamp(__instance.hostButton.transform.GetSiblingIndex(), 0, rowClone.transform.childCount - 1);
                    buttonObject = rowClone.transform.GetChild(buttonIndex).gameObject;
                    for (int i = rowClone.transform.childCount - 1; i >= 0; i--)
                    {
                        GameObject child = rowClone.transform.GetChild(i).gameObject;
                        if (child != buttonObject)
                            Object.DestroyImmediate(child);
                    }

                    buttonObject.name = "(Button) Quick Host";
                }

                LayoutGroup buttonLayout = buttonObject.GetComponent<LayoutGroup>();
                if (buttonLayout != null)
                    buttonLayout.enabled = false;

                ContentSizeFitter contentSizeFitter = buttonObject.GetComponent<ContentSizeFitter>();
                if (contentSizeFitter != null)
                    contentSizeFitter.enabled = false;

                foreach (Image childImage in buttonObject.GetComponentsInChildren<Image>(true))
                {
                    if (childImage.gameObject != buttonObject)
                        childImage.gameObject.SetActive(false);
                }

                TMP_Text titleText = null;
                TMP_Text leftText = null;
                TMP_Text rightText = null;
                TMP_Text sourceText = null;
                foreach (TMP_Text candidate in buttonObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (candidate == null)
                        continue;

                    if (candidate.gameObject.name == "Quick Host Title")
                        titleText = candidate;
                    else if (candidate.gameObject.name == "Quick Host Settings Left")
                        leftText = candidate;
                    else if (candidate.gameObject.name == "Quick Host Settings Right")
                        rightText = candidate;
                    else if (sourceText == null)
                        sourceText = candidate;
                }

                TMP_Text templateText = titleText ?? leftText ?? rightText ?? sourceText;
                if (templateText == null)
                    return;

                if (titleText == null)
                {
                    GameObject titleObject = Object.Instantiate(templateText.gameObject, buttonObject.transform);
                    titleObject.name = "Quick Host Title";
                    titleText = titleObject.GetComponent<TMP_Text>();
                }

                if (leftText == null)
                {
                    GameObject leftObject = Object.Instantiate(templateText.gameObject, buttonObject.transform);
                    leftObject.name = "Quick Host Settings Left";
                    leftText = leftObject.GetComponent<TMP_Text>();
                }

                if (rightText == null)
                {
                    GameObject rightObject = Object.Instantiate(templateText.gameObject, buttonObject.transform);
                    rightObject.name = "Quick Host Settings Right";
                    rightText = rightObject.GetComponent<TMP_Text>();
                }

                if (titleText != null && titleText.transform.parent != buttonObject.transform)
                    titleText.transform.SetParent(buttonObject.transform, false);
                if (leftText != null && leftText.transform.parent != buttonObject.transform)
                    leftText.transform.SetParent(buttonObject.transform, false);
                if (rightText != null && rightText.transform.parent != buttonObject.transform)
                    rightText.transform.SetParent(buttonObject.transform, false);

                foreach (TMP_Text candidate in buttonObject.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (candidate != titleText && candidate != leftText && candidate != rightText)
                        candidate.gameObject.SetActive(false);
                }

                if (titleText != null)
                {
                    titleText.gameObject.SetActive(true);
                    titleText.transform.SetAsLastSibling();
                    var loc = titleText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                    if (loc != null)
                        Object.DestroyImmediate(loc);

                    titleText.text = "QUICK HOST";
                    titleText.fontSize = 30f;
                    titleText.characterSpacing = 0f;
                    titleText.wordSpacing = 0f;
                    titleText.lineSpacing = 0f;
                    titleText.alignment = TextAlignmentOptions.Center;
                    titleText.enableAutoSizing = false;
                    titleText.enableWordWrapping = false;
                    RectTransform titleRect = titleText.GetComponent<RectTransform>();
                    if (titleRect != null)
                    {
                        titleRect.localScale = Vector3.one;
                        titleRect.anchorMin = new Vector2(0.36f, 0f);
                        titleRect.anchorMax = new Vector2(0.64f, 1f);
                        titleRect.offsetMin = Vector2.zero;
                        titleRect.offsetMax = Vector2.zero;
                    }
                    titleText.ForceMeshUpdate();
                }

                if (leftText != null)
                {
                    leftText.gameObject.SetActive(true);
                    leftText.transform.SetAsLastSibling();
                    var loc = leftText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                    if (loc != null)
                        Object.DestroyImmediate(loc);

                    leftText.text = leftSettings;
                    leftText.fontSize = 13f;
                    leftText.characterSpacing = 0f;
                    leftText.wordSpacing = 0f;
                    leftText.lineSpacing = 0f;
                    leftText.alignment = TextAlignmentOptions.MidlineLeft;
                    leftText.enableAutoSizing = false;
                    leftText.enableWordWrapping = false;
                    Color leftColor = leftText.color;
                    leftColor.a = 0.86f;
                    leftText.color = leftColor;

                    RectTransform leftRect = leftText.GetComponent<RectTransform>();
                    if (leftRect != null)
                    {
                        leftRect.localScale = Vector3.one;
                        leftRect.anchorMin = new Vector2(0f, 0f);
                        leftRect.anchorMax = new Vector2(0.36f, 1f);
                        leftRect.offsetMin = new Vector2(16f, 8f);
                        leftRect.offsetMax = new Vector2(-8f, -8f);
                    }
                    leftText.ForceMeshUpdate();
                }

                if (rightText != null)
                {
                    rightText.gameObject.SetActive(true);
                    rightText.transform.SetAsLastSibling();
                    var loc = rightText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                    if (loc != null)
                        Object.DestroyImmediate(loc);

                    rightText.text = rightSettings;
                    rightText.fontSize = 13f;
                    rightText.characterSpacing = 0f;
                    rightText.wordSpacing = 0f;
                    rightText.lineSpacing = 0f;
                    rightText.alignment = TextAlignmentOptions.MidlineRight;
                    rightText.enableAutoSizing = false;
                    rightText.enableWordWrapping = false;
                    Color rightColor = rightText.color;
                    rightColor.a = 0.86f;
                    rightText.color = rightColor;

                    RectTransform rightRect = rightText.GetComponent<RectTransform>();
                    if (rightRect != null)
                    {
                        rightRect.localScale = Vector3.one;
                        rightRect.anchorMin = new Vector2(0.64f, 0f);
                        rightRect.anchorMax = new Vector2(1f, 1f);
                        rightRect.offsetMin = new Vector2(8f, 8f);
                        rightRect.offsetMax = new Vector2(-16f, -8f);
                    }
                    rightText.ForceMeshUpdate();
                }

                Image image = buttonObject.GetComponent<Image>();
                if (image != null)
                    image.color = new Color(0.3f, 0.7f, 0.3f, 1f);

                Button quickHostButton = buttonObject.GetComponent<Button>();
                if (quickHostButton != null)
                {
                    quickHostButton.onClick.RemoveAllListeners();
                    quickHostButton.onClick.AddListener((UnityEngine.Events.UnityAction)QuickHost);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] Failed to inject Quick Host button: {ex}");
            }
        }

    }
}
