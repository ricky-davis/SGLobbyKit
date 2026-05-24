using Il2Cpp;
using Il2Cpp_Scripts.Managers;
using UnityEngine;
using Il2CppTMPro;
using EosLobby = Il2CppPlayEveryWare.EpicOnlineServices.Samples.Lobby;

namespace MultiplayerTools.Features.Lobby
{
    internal static class LobbyCreateSubmission
    {
        public static void Apply(ref string lobbyName, ref int maxPlayers, TMP_InputField customLobbyNameInput, MySliderUI maxPlayerSlider)
        {
            lobbyName = GetLobbyName(customLobbyNameInput);

            int selectedMaxPlayers = maxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(maxPlayerSlider.slider.value)
                : maxPlayers;
            if (selectedMaxPlayers <= 0)
                selectedMaxPlayers = MultiplayerToolsCore.ServerCapacity;

            maxPlayers = Mathf.Clamp(selectedMaxPlayers, 1, 64);
            MultiplayerToolsCore.SetServerCapacity(maxPlayers);
        }

        public static void ApplyToEosLobby(EosLobby lobbyProperties, MySliderUI maxPlayerSlider)
        {
            if (lobbyProperties == null)
                return;

            int maxPlayers = maxPlayerSlider?.slider != null
                ? Mathf.RoundToInt(maxPlayerSlider.slider.value)
                : MultiplayerToolsCore.ServerCapacity;
            lobbyProperties.MaxNumLobbyMembers = (uint)Mathf.Clamp(maxPlayers, 1, 64);
        }

        private static string GetLobbyName(TMP_InputField customLobbyNameInput)
        {
            if (customLobbyNameInput != null)
            {
                var customName = customLobbyNameInput.text;
                if (!string.IsNullOrWhiteSpace(customName))
                    return customName;
            }

            if (!string.IsNullOrWhiteSpace(MultiplayerToolsCore.ServerName))
                return MultiplayerToolsCore.ServerName;

            string playerName = GameInfo.Instance?.PlayerName;
            return string.IsNullOrWhiteSpace(playerName) ? "Sledding Lobby" : playerName + "'s Lobby";
        }
    }
}
