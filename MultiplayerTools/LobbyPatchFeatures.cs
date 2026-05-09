using HarmonyLib;
using Il2Cpp;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
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
        public static Il2CppTMPro.TMP_InputField CustomLobbyNameInput;

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

            return GameInfo.Instance.PlayerName + "'s Lobby";
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

            Toggle publicLobbyToggle = instance.publicLobbyToggle;
            if (publicLobbyToggle != null)
            {
                if (MultiplayerToolsCore.IsPublicLobby)
                {
                    publicLobbyToggle.isOn = true;
                }
                else
                {
                    publicLobbyToggle.group.m_Toggles[0].isOn = true;
                }
                publicLobbyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPublicLobby(isOn);
                }));
            }

            Toggle passwordProtectedToggle = instance.passwordProtectedToggle;
            if (passwordProtectedToggle != null)
            {
                passwordProtectedToggle.isOn = MultiplayerToolsCore.IsPasswordProtected;
                passwordProtectedToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPasswordProtected(isOn);
                }));
            }

            TMP_InputField passwordInputField = instance.passwordInputField;
            if (passwordInputField != null)
            {
                passwordInputField.text = MultiplayerToolsCore.LobbyPassword;
                passwordInputField.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
                {
                    MultiplayerToolsCore.SetLobbyPassword(text);
                }));
            }

            Toggle peacefulModeToggle = instance.peacefulModeToggle;
            if (peacefulModeToggle != null)
            {
                peacefulModeToggle.isOn = MultiplayerToolsCore.IsPeacefulMode;
                peacefulModeToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsPeacefulMode(isOn);
                }));
            }

            Toggle textChatOnlyToggle = instance.textChatOnlyToggle;
            if (textChatOnlyToggle != null)
            {
                textChatOnlyToggle.isOn = MultiplayerToolsCore.IsTextChatOnly;
                textChatOnlyToggle.onValueChanged.AddListener((UnityEngine.Events.UnityAction<bool>)((isOn) =>
                {
                    MultiplayerToolsCore.SetIsTextChatOnly(isOn);
                }));
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
            maxPlayers = MaxPlayerSlider != null
                ? (int)MaxPlayerSlider.slider.value
                : Mathf.Clamp(MultiplayerToolsCore.ServerCapacity, 1, 64);
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void EOSLobbyManager_CreateLobby_Prefix(EOSLobbyManager __instance, Lobby lobbyProperties)
        {
            lobbyProperties.MaxNumLobbyMembers = (uint)LobbyPatchFeatures.MaxPlayerSlider.slider.value;
        }

        [HarmonyPatch(typeof(UICreateLobby), "ShowPanel")]
        [HarmonyPostfix]
        private static void UICreateLobby_ShowPanel_Postfix(UICreateLobby __instance)
        {
            LobbyPatchFeatures.SetupCustomLobbyNameInput(__instance);
        }
    }
}
