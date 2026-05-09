using HarmonyLib;
using Il2Cpp;
using Il2CppPlayEveryWare.EpicOnlineServices.Samples;
using Il2Cpp_Scripts.Managers;
using Il2Cpp_Scripts.UI.Pre_Game;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiplayerTools.Patches
{
    public static class LobbyPatchFeatures
    {
        public static dynamic MaxPlayerSlider;
        public static Il2CppTMPro.TMP_InputField CustomLobbyNameInput;

        public static void SetReferences(PlayerReferenceManager prm)
        {
            ChatSystem.AddFakeServerPlayerReference();
        }

        public static void SetMaxPlayers(int maxPlayers)
        {
            if (MaxPlayerSlider == null)
                return;

            dynamic s = MaxPlayerSlider.slider;
            s.minValue = 1f;
            s.maxValue = (float)maxPlayers;
            s.wholeNumbers = true;

            if (s.value > 64f)
                s.value = 64f;

            if (s.value < 1f)
                s.value = 1f;

            MaxPlayerSlider.UpdateSliderValueDisplay();
        }

        public static string GetPatchedLobbyName()
        {
            if (CustomLobbyNameInput != null)
            {
                var customName = CustomLobbyNameInput.text;
                if (!string.IsNullOrWhiteSpace(customName))
                    return customName;
            }

            return GameInfo.Instance.PlayerName + "'s Lobby";
        }

        public static void SetupCustomLobbyNameInput(UICreateLobby instance)
        {
            if (instance == null)
                return;

            dynamic mySlider = instance.maxPlayersSlider;
            if (mySlider == null)
                return;

            MaxPlayerSlider = mySlider;

            dynamic s = mySlider.slider;
            if (s == null)
                return;

            SetMaxPlayers(64);

            if (CustomLobbyNameInput != null)
                return;

            dynamic label = instance.lobbyNameText;
            dynamic passwordInput = instance.passwordInputField;
            var labelTransform = label.transform;
            var layoutParent = labelTransform.parent;
            var labelIndex = labelTransform.GetSiblingIndex();

            var cloneGO = Object.Instantiate(passwordInput.gameObject, layoutParent);
            cloneGO.name = "CustomLobbyNameInput";

            var cloneTransform = cloneGO.transform;
            cloneTransform.SetSiblingIndex(labelIndex);

            var cloneInput = cloneGO.GetComponent<Il2CppTMPro.TMP_InputField>();
            var cloneRT = cloneGO.GetComponent<RectTransform>();

            cloneRT.localScale = Vector3.one;
            cloneRT.sizeDelta = new Vector2(473.99f, 47.04f);

            cloneInput.text = "";
            cloneInput.characterLimit = 64;
            cloneInput.lineLimit = 1;
            cloneInput.contentType = Il2CppTMPro.TMP_InputField.ContentType.Standard;
            cloneInput.inputType = Il2CppTMPro.TMP_InputField.InputType.Standard;
            cloneInput.lineType = Il2CppTMPro.TMP_InputField.LineType.SingleLine;
            cloneInput.interactable = true;

            dynamic gameInfo = GameInfo.Instance;
            cloneInput.onValueChanged.AddListener((UnityEngine.Events.UnityAction<string>)((text) =>
            {
                label.text = text;
            }));
            cloneInput.text = $"{gameInfo.PlayerName}'s Lobby";

            if (cloneInput.placeholder != null)
            {
                var placeholderText = cloneInput.placeholder.GetComponent<Il2CppTMPro.TMP_Text>();
                if (placeholderText != null)
                {
                    var loc = placeholderText.GetComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
                    if (loc != null)
                        Object.Destroy(loc);
                    placeholderText.text = "Lobby name...";
                }
            }

            label.gameObject.SetActive(false);
            cloneGO.SetActive(true);
            CustomLobbyNameInput = cloneInput;
        }
    }

    [HarmonyPatch]
    public static class MaxPlayersPatch
    {
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
            lobbyName = LobbyPatchFeatures.GetPatchedLobbyName();
            maxPlayers = (int)LobbyPatchFeatures.MaxPlayerSlider.slider.value;
        }

        [HarmonyPatch(typeof(EOSLobbyManager), "CreateLobby")]
        [HarmonyPrefix]
        private static void EOSLobbyManager_CreateLobby_Prefix(EOSLobbyManager __instance, Lobby lobbyProperties)
        {
            lobbyProperties.MaxNumLobbyMembers = (uint)LobbyPatchFeatures.MaxPlayerSlider.slider.value;
        }

        [HarmonyPatch(typeof(UICreateLobby), "Awake")]
        [HarmonyPostfix]
        private static void UICreateLobby_Awake_Postfix(UICreateLobby __instance)
        {
            LobbyPatchFeatures.SetupCustomLobbyNameInput(__instance);
        }
    }
}
