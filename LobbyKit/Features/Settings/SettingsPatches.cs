using HarmonyLib;
using Il2Cpp;
using LobbyKit.Features.Settings;

namespace LobbyKit.Patches
{
    [HarmonyPatch]
    public static class SettingsPatches
    {
        // The game closes the current (top) menu on Escape / the in-game back action via
        // UiReferenceController.CloseMenu. While OUR settings menu is the top menu, route that through our own
        // close logic so Escape behaves exactly like the in-menu Close (X) button: prompt when there are
        // unapplied changes, otherwise close cleanly — instead of the game silently discarding the menu (which
        // also left the controller's state stale and corrupted the next open).
        [HarmonyPatch(typeof(UiReferenceController), "CloseMenu")]
        [HarmonyPrefix]
        private static bool CloseMenu_Prefix(UiReferenceController __instance)
        {
            return SettingsMenuController.Instance.InterceptGameCloseMenu(__instance);
        }
    }
}
