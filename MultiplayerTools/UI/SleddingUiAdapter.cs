using UnityEngine;

namespace MultiplayerTools
{
    internal static class SleddingUiAdapter
    {
        public static UILib.DefaultReferences Templates => UILib.Defaults;

        public static void CaptureSceneTemplates(bool overwriteExisting = false)
        {
            UILib.CaptureSceneDefaults(overwriteExisting);
        }

        public static void CaptureTemplatesFrom(Transform root, bool overwriteExisting = false)
        {
            UILib.CaptureDefaultsFrom(root, overwriteExisting);
        }

        public static void ClearTemplates()
        {
            UILib.ClearDefaults();
        }
    }
}

