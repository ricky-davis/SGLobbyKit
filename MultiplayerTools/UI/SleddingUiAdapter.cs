using UnityEngine;

namespace MultiplayerTools
{
    internal static class SleddingUiAdapter
    {
        public static UILib.DefaultReferences Templates => UILib.Defaults;

        public static UiElement Assume(GameObject gameObject)
        {
            return UILib.Assume(gameObject);
        }

        public static UiElement Assume(Component component)
        {
            return UILib.Assume(component);
        }

        public static UiElement Assume(Transform transform)
        {
            return UILib.Assume(transform);
        }

        public static UiElement Find(string sceneObjectName)
        {
            return UILib.Find(sceneObjectName);
        }

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
