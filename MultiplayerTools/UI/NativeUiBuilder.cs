using UnityEngine;

namespace MultiplayerTools
{
    internal static class NativeUiBuilder
    {
        public static UiElement HorizontalRow(Transform parent, string name = "Row", float height = 38f, float spacing = 14f)
        {
            return UILib.CreateHorizontalRow(parent, name, height, spacing);
        }

        public static UiElement FlexRow(
            Transform parent,
            string name = "Row",
            float height = 38f,
            float spacing = 14f,
            RectOffset padding = null)
        {
            return UILib.CreateFlexRow(parent, name, height, spacing, padding);
        }

        public static UILib.GridTrackRow GridTrackRow(
            Transform parent,
            string name = "Grid Row",
            float height = 38f,
            float spacing = 8f,
            RectOffset padding = null,
            float[] trackWidths = null)
        {
            return UILib.CreateGridTrackRow(parent, name, height, spacing, padding, trackWidths);
        }
    }
}
