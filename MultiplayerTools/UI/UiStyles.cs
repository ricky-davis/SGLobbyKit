using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTools
{
    internal static class UiStyles
    {
        public static void ApplyText(TMP_Text target, TMP_Text template = null)
        {
            UILib.ApplyTextStyle(target, template);
        }

        public static void ApplyButton(Button button, Button template = null)
        {
            UILib.ApplyButtonStyle(button, template);
        }

        public static void ApplyToggle(Toggle toggle, Toggle template = null)
        {
            UILib.ApplyToggleStyle(toggle, template);
        }

        public static Image CopyImage(Image template, GameObject target)
        {
            return UILib.CopyImage(template, target);
        }

        public static Shadow CopyShadow(Shadow template, GameObject target)
        {
            return UILib.CopyShadow(template, target);
        }

        public static LayoutElement CopyLayout(LayoutElement template, GameObject target)
        {
            return UILib.CopyLayout(template, target);
        }

        public static void SetTextMetrics(
            TMP_Text text,
            float fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool autoSize = false,
            float minFontSize = 10f)
        {
            UILib.SetTextMetrics(text, fontSize, alignment, autoSize, minFontSize);
        }

        public static void SetInputTextStyle(TMP_InputField input, float fontSize, bool autoSize = false, float minFontSize = 10f)
        {
            UILib.SetInputTextStyle(input, fontSize, autoSize: autoSize, minFontSize: minFontSize);
        }
    }
}
