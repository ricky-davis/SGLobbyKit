using UnityEngine;

namespace MultiplayerTools
{
    internal static class NativeUiTemplates
    {
        public static UILib.DefaultReferences Current => UILib.Defaults;

        public static bool HasAny => UILib.HasDefaults;

        public static bool HasCore => UILib.HasCoreDefaults;

        public static void CaptureScene(bool overwriteExisting = false)
        {
            UILib.CaptureSceneDefaults(overwriteExisting);
        }

        public static void CaptureFrom(Transform root, bool overwriteExisting = false)
        {
            UILib.CaptureDefaultsFrom(root, overwriteExisting);
        }

        public static void Clear()
        {
            UILib.ClearDefaults();
        }
    }
}
