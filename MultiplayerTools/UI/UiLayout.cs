using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTools
{
    internal static class UiLayout
    {
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

        public static void SetFixedSize(GameObject gameObject, float? preferredWidth = null, float? preferredHeight = null)
        {
            UILib.SetFixedLayoutSize(gameObject, preferredWidth: preferredWidth, preferredHeight: preferredHeight);
        }

        public static void Stretch(GameObject gameObject)
        {
            UILib.Stretch(gameObject);
        }
    }
}
