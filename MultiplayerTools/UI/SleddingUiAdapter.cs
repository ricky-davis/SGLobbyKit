using UnityEngine;

namespace MultiplayerTools
{
    internal static class SleddingUiAdapter
    {
        public static UILib.DefaultReferences Templates => UILib.Defaults;

        public static void CaptureSceneTemplates(bool overwriteExisting = false)
        {
            NativeUiTemplates.CaptureScene(overwriteExisting);
        }

        public static void CaptureTemplatesFrom(Transform root, bool overwriteExisting = false)
        {
            NativeUiTemplates.CaptureFrom(root, overwriteExisting);
        }

        public static void ClearTemplates()
        {
            NativeUiTemplates.Clear();
        }
    }
}
