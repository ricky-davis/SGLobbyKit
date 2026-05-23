using System;

namespace MultiplayerTools.Features.Settings
{
    internal sealed class SettingsDraft
    {
        public bool EnableGuestBangCommands { get; set; }
        public string ServerName { get; set; }
        public int ServerCapacity { get; set; }
        public bool IsPublicLobby { get; set; }
        public bool IsPasswordProtected { get; set; }
        public string LobbyPassword { get; set; }
        public bool IsPeacefulMode { get; set; }
        public bool IsTextChatOnly { get; set; }
        public string MessageOfTheDay { get; set; }
        public bool ShowJoinMessages { get; set; }
        public bool ShowLeaveMessages { get; set; }
        public int JoinMessageSize { get; set; }
        public int LeaveMessageSize { get; set; }

        public static SettingsDraft FromCurrent()
        {
            return new SettingsDraft
            {
                EnableGuestBangCommands = MultiplayerToolsCore.EnableGuestBangCommands,
                ServerName = MultiplayerToolsCore.ServerName,
                ServerCapacity = MultiplayerToolsCore.ServerCapacity,
                IsPublicLobby = MultiplayerToolsCore.IsPublicLobby,
                IsPasswordProtected = MultiplayerToolsCore.IsPasswordProtected,
                LobbyPassword = MultiplayerToolsCore.LobbyPassword,
                IsPeacefulMode = MultiplayerToolsCore.IsPeacefulMode,
                IsTextChatOnly = MultiplayerToolsCore.IsTextChatOnly,
                MessageOfTheDay = MultiplayerToolsCore.MessageOfTheDay,
                ShowJoinMessages = MultiplayerToolsCore.ShowJoinMessages,
                ShowLeaveMessages = MultiplayerToolsCore.ShowLeaveMessages,
                JoinMessageSize = MultiplayerToolsCore.JoinMessageSize,
                LeaveMessageSize = MultiplayerToolsCore.LeaveMessageSize
            };
        }

        public SettingsDraft Clone()
        {
            return new SettingsDraft
            {
                EnableGuestBangCommands = EnableGuestBangCommands,
                ServerName = ServerName,
                ServerCapacity = ServerCapacity,
                IsPublicLobby = IsPublicLobby,
                IsPasswordProtected = IsPasswordProtected,
                LobbyPassword = LobbyPassword,
                IsPeacefulMode = IsPeacefulMode,
                IsTextChatOnly = IsTextChatOnly,
                MessageOfTheDay = MessageOfTheDay,
                ShowJoinMessages = ShowJoinMessages,
                ShowLeaveMessages = ShowLeaveMessages,
                JoinMessageSize = JoinMessageSize,
                LeaveMessageSize = LeaveMessageSize
            };
        }

        public bool ValueEquals(SettingsDraft other)
        {
            if (other == null)
                return false;

            return EnableGuestBangCommands == other.EnableGuestBangCommands &&
                   string.Equals(ServerName, other.ServerName, StringComparison.Ordinal) &&
                   ServerCapacity == other.ServerCapacity &&
                   IsPublicLobby == other.IsPublicLobby &&
                   IsPasswordProtected == other.IsPasswordProtected &&
                   string.Equals(LobbyPassword, other.LobbyPassword, StringComparison.Ordinal) &&
                   IsPeacefulMode == other.IsPeacefulMode &&
                   IsTextChatOnly == other.IsTextChatOnly &&
                   string.Equals(MessageOfTheDay, other.MessageOfTheDay, StringComparison.Ordinal) &&
                   ShowJoinMessages == other.ShowJoinMessages &&
                   ShowLeaveMessages == other.ShowLeaveMessages &&
                   JoinMessageSize == other.JoinMessageSize &&
                   LeaveMessageSize == other.LeaveMessageSize;
        }

        public void Apply()
        {
            MultiplayerToolsCore.SetEnableGuestBangCommands(EnableGuestBangCommands);
            MultiplayerToolsCore.SetServerName(ServerName);
            MultiplayerToolsCore.SetServerCapacity(ServerCapacity);
            MultiplayerToolsCore.SetIsPublicLobby(IsPublicLobby);
            MultiplayerToolsCore.SetIsPasswordProtected(IsPasswordProtected);
            MultiplayerToolsCore.SetLobbyPassword(LobbyPassword);
            MultiplayerToolsCore.SetIsPeacefulMode(IsPeacefulMode);
            MultiplayerToolsCore.SetIsTextChatOnly(IsTextChatOnly);
            MultiplayerToolsCore.SetMessageOfTheDay(MessageOfTheDay);
            MultiplayerToolsCore.SetShowJoinMessages(ShowJoinMessages);
            MultiplayerToolsCore.SetShowLeaveMessages(ShowLeaveMessages);
            MultiplayerToolsCore.SetJoinMessageSize(JoinMessageSize);
            MultiplayerToolsCore.SetLeaveMessageSize(LeaveMessageSize);
        }
    }
}

