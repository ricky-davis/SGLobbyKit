using HarmonyLib;
using Il2Cpp;
using MultiplayerTools.Features.Settings;
using Il2Cpp_Scripts.Player;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class SettingsCommand
    {
        public static void HandleCommand(PlayerControl playerControl, string args)
        {
            SettingsMenuController.Instance.Open(SettingsMenuView.Build, SettingsMenuView.HasRequiredTemplates);
        }

        public static bool IsOpen => SettingsMenuController.Instance.IsOpen;

        [HarmonyPatch(typeof(UiReferenceController), "Update")]
        [HarmonyPostfix]
        private static void UiReferenceController_Update_Postfix()
        {
            SettingsMenuController.Instance.HandleEscape();
        }
    }
}
