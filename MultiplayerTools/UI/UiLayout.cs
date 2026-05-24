using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTools
{
    internal static class UiLayout
    {
        public static VerticalLayoutGroup SetVertical(
            GameObject gameObject,
            RectOffset padding = null,
            float spacing = 0f,
            bool childControlWidth = true,
            bool childControlHeight = true,
            bool childForceExpandWidth = true,
            bool childForceExpandHeight = false)
        {
            return UILib.SetVerticalLayout(
                gameObject,
                padding,
                spacing,
                childControlWidth,
                childControlHeight,
                childForceExpandWidth,
                childForceExpandHeight);
        }

        public static void SetRect(
            Component component,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Vector2? anchoredPosition = null,
            Vector2? sizeDelta = null,
            Vector3? scale = null)
        {
            UILib.SetRect(component, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, scale);
        }

        public static LayoutElement SetLayout(
            GameObject gameObject,
            float? preferredWidth = null,
            float? preferredHeight = null,
            float? flexibleWidth = null,
            float? flexibleHeight = null,
            float? minWidth = null,
            float? minHeight = null)
        {
            return UILib.SetLayout(gameObject, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight, minWidth, minHeight);
        }

        public static void SetFixedSize(
            GameObject gameObject,
            float? preferredWidth = null,
            float? preferredHeight = null,
            float? flexibleWidth = null,
            float? flexibleHeight = null)
        {
            UILib.SetFixedLayoutSize(
                gameObject,
                preferredWidth: preferredWidth,
                preferredHeight: preferredHeight,
                flexibleWidth: flexibleWidth,
                flexibleHeight: flexibleHeight);
        }

        public static void Stretch(GameObject gameObject)
        {
            UILib.Stretch(gameObject);
        }

        public static void LayoutGridTracks(UILib.GridTrackRow row, float spacing, float[] trackWidths)
        {
            UILib.LayoutGridTracks(row, spacing: spacing, trackWidths: trackWidths);
        }

        public static void StabilizeClonedControl(GameObject gameObject)
        {
            UILib.StabilizeClonedControl(gameObject);
        }

        public static void SetCanvasGroups(Transform root, float alpha, bool interactable, bool blocksRaycasts)
        {
            UILib.SetCanvasGroups(root, alpha, interactable, blocksRaycasts);
        }

        public static void ActivatePathToRoot(Transform start, Transform root)
        {
            UILib.ActivatePathToRoot(start, root);
        }

        public static void SetChildrenActive(Transform parent, bool active, string skipNameContains = null)
        {
            UILib.SetChildrenActive(parent, active, skipNameContains);
        }
    }
}
