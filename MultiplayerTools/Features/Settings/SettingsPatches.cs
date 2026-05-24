using HarmonyLib;
using Il2Cpp;
using MultiplayerTools.Features.Settings;

namespace MultiplayerTools.Patches
{
    [HarmonyPatch]
    public static class SettingsPatches
    {
        [HarmonyPatch(typeof(UiReferenceController), "Update")]
        [HarmonyPostfix]
        private static void UiReferenceController_Update_Postfix()
        {
            SettingsMenuController.Instance.HandleEscape();
        }
    }
}
