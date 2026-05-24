using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MultiplayerTools
{
    internal static class NativeUiFactory
    {
        public static TMP_Text Label(Transform parent, string text, string name = "Label", TMP_Text template = null)
        {
            return UILib.CreatePlainLabel(parent, text, name, template);
        }

        public static Button Button(Transform parent, string label, UnityAction onClick = null, string name = "Button")
        {
            return UILib.CloneButton(NativeUiTemplates.Current.Button, parent, name, label, onClick);
        }

        public static TMP_InputField Input(
            Transform parent,
            string text = "",
            string placeholder = "",
            string name = "Input Field",
            UnityAction<string> onValueChanged = null)
        {
            TMP_InputField input = UILib.CloneInputField(NativeUiTemplates.Current.InputField, parent, name, text, placeholder);
            if (input != null && onValueChanged != null)
                input.onValueChanged.AddListener(onValueChanged);
            return input;
        }

        public static Toggle Toggle(
            Transform parent,
            string label,
            bool isOn,
            UnityAction<bool> onValueChanged = null,
            string name = "Toggle")
        {
            return UILib.CloneToggle(NativeUiTemplates.Current.Toggle, parent, name, label, isOn, onValueChanged);
        }

        public static MySliderUI Slider(
            Transform parent,
            string name = "Slider",
            float? minValue = null,
            float? maxValue = null,
            float? value = null,
            UnityAction<float> onValueChanged = null)
        {
            return UILib.CreateSlider(parent, name, NativeUiTemplates.Current.Slider, minValue, maxValue, value, onValueChanged);
        }

        public static Scrollbar Scrollbar(Transform parent, string name = "Scrollbar")
        {
            return UILib.CreateScrollbar(parent, name, NativeUiTemplates.Current.Scrollbar);
        }

        public static UiElement Panel(Transform parent, string name = "Panel")
        {
            return UILib.CreatePanel(parent, name);
        }

        public static Image Backdrop(Transform parent, Image template = null)
        {
            return UILib.CreateScreenBackdrop(parent, template: template);
        }

        public static UILib.ScrollViewportResult ScrollView(Transform parent, string name = "Scroll Viewport")
        {
            return UILib.CreateScrollViewport(parent, name);
        }
    }
}
