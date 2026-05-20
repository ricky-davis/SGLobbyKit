using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MultiplayerTools
{
    public static class UILib
    {
        private static int _lastAutoCaptureFrame = -1;
        private static readonly Color FallbackPanelColor = new Color(0.09f, 0.5f, 0.74f, 0.97f);
        private static readonly Color FallbackButtonColor = new Color(0.09f, 0.62f, 0.82f, 1f);
        private static readonly Color FallbackInputColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color FallbackToggleCheckColor = new Color(0.17f, 0.74f, 0.45f, 1f);

        public sealed class Element
        {
            public Element(GameObject gameObject)
            {
                GameObject = gameObject;
            }

            public GameObject GameObject { get; }
            public Transform Transform => GameObject != null ? GameObject.transform : null;
            public RectTransform RectTransform => GameObject != null ? GameObject.GetComponent<RectTransform>() : null;
            public bool Exists => GameObject != null;

            public T Get<T>() where T : Component
            {
                return GameObject != null ? GameObject.GetComponent<T>() : null;
            }

            public T GetInChildren<T>(bool includeInactive = true) where T : Component
            {
                return GameObject != null ? GameObject.GetComponentInChildren<T>(includeInactive) : null;
            }

            public Element Show()
            {
                return SetActive(true);
            }

            public Element Hide()
            {
                return SetActive(false);
            }

            public Element SetActive(bool active)
            {
                if (GameObject != null)
                    GameObject.SetActive(active);
                return this;
            }

            public Element Reparent(Transform parent, bool worldPositionStays = false)
            {
                if (Transform != null && parent != null)
                    Transform.SetParent(parent, worldPositionStays);
                return this;
            }

            public Element SiblingIndex(int index)
            {
                if (Transform != null)
                    Transform.SetSiblingIndex(index);
                return this;
            }

            public Element LastSibling()
            {
                if (Transform != null)
                    Transform.SetAsLastSibling();
                return this;
            }

            public Element FirstSibling()
            {
                if (Transform != null)
                    Transform.SetAsFirstSibling();
                return this;
            }

            public Element Move(Vector2 anchoredPosition)
            {
                if (RectTransform != null)
                    RectTransform.anchoredPosition = anchoredPosition;
                return this;
            }

            public Element Resize(Vector2 sizeDelta)
            {
                if (RectTransform != null)
                    RectTransform.sizeDelta = sizeDelta;
                return this;
            }

            public Element Scale(float scale)
            {
                return Scale(new Vector3(scale, scale, 1f));
            }

            public Element Scale(Vector3 scale)
            {
                if (RectTransform != null)
                    RectTransform.localScale = scale;
                else if (Transform != null)
                    Transform.localScale = scale;
                return this;
            }

            public Element Anchor(Vector2 anchorMin, Vector2 anchorMax)
            {
                if (RectTransform != null)
                {
                    RectTransform.anchorMin = anchorMin;
                    RectTransform.anchorMax = anchorMax;
                }
                return this;
            }

            public Element Pivot(Vector2 pivot)
            {
                if (RectTransform != null)
                    RectTransform.pivot = pivot;
                return this;
            }

            public Element CenterAnchors()
            {
                Vector2 center = new Vector2(0.5f, 0.5f);
                return Anchor(center, center).Pivot(center);
            }

            public Element CanvasGroup(float alpha = 1f, bool interactable = true, bool blocksRaycasts = true)
            {
                UILib.SetCanvasGroups(Transform, alpha, interactable, blocksRaycasts);
                return this;
            }

            public Element Layout(
                float? preferredWidth = null,
                float? preferredHeight = null,
                float? flexibleWidth = null,
                float? flexibleHeight = null,
                float? minWidth = null,
                float? minHeight = null)
            {
                UILib.SetLayout(GameObject, preferredWidth, preferredHeight, flexibleWidth, flexibleHeight, minWidth, minHeight);
                return this;
            }

            public Element Destroy()
            {
                if (GameObject != null)
                    Object.Destroy(GameObject);
                return this;
            }

            public Element DestroyImmediate()
            {
                if (GameObject != null)
                    Object.DestroyImmediate(GameObject);
                return this;
            }
        }

        public sealed class DefaultReferences
        {
            public Button Button;
            public TMP_Text Label;
            public TMP_Text HeaderLabel;
            public TMP_Text ButtonLabel;
            public TMP_Text ToggleLabel;
            public TMP_Text SliderLabel;
            public TMP_Text InputText;
            public TMP_Text InputPlaceholder;
            public TMP_InputField InputField;
            public Toggle Toggle;
            public MySliderUI Slider;
            public Scrollbar Scrollbar;
            public Image Background;
            public Shadow Shadow;
            public GameObject Panel;
        }

        public static DefaultReferences Defaults { get; } = new DefaultReferences();
        public static bool AutoCaptureSceneDefaults { get; set; } = true;

        public static bool HasDefaults =>
            Defaults.Button != null ||
            Defaults.Label != null ||
            Defaults.HeaderLabel != null ||
            Defaults.ButtonLabel != null ||
            Defaults.ToggleLabel != null ||
            Defaults.SliderLabel != null ||
            Defaults.InputText != null ||
            Defaults.InputPlaceholder != null ||
            Defaults.InputField != null ||
            Defaults.Toggle != null ||
            Defaults.Slider != null ||
            Defaults.Scrollbar != null ||
            Defaults.Background != null ||
            Defaults.Shadow != null ||
            Defaults.Panel != null;

        public static bool HasCoreDefaults =>
            Defaults.Button != null &&
            Defaults.Label != null &&
            Defaults.InputField != null &&
            Defaults.Toggle != null;

        public static void ClearDefaults()
        {
            Defaults.Button = null;
            Defaults.Label = null;
            Defaults.HeaderLabel = null;
            Defaults.ButtonLabel = null;
            Defaults.ToggleLabel = null;
            Defaults.SliderLabel = null;
            Defaults.InputText = null;
            Defaults.InputPlaceholder = null;
            Defaults.InputField = null;
            Defaults.Toggle = null;
            Defaults.Slider = null;
            Defaults.Scrollbar = null;
            Defaults.Background = null;
            Defaults.Shadow = null;
            Defaults.Panel = null;
            _lastAutoCaptureFrame = -1;
        }

        public static void SetDefaults(DefaultReferences references, bool overwriteExisting = true)
        {
            if (references == null)
                return;

            Defaults.Button = PickDefault(Defaults.Button, references.Button, overwriteExisting);
            Defaults.Label = PickDefault(Defaults.Label, references.Label, overwriteExisting);
            Defaults.HeaderLabel = PickDefault(Defaults.HeaderLabel, references.HeaderLabel, overwriteExisting);
            Defaults.ButtonLabel = PickDefault(Defaults.ButtonLabel, references.ButtonLabel, overwriteExisting);
            Defaults.ToggleLabel = PickDefault(Defaults.ToggleLabel, references.ToggleLabel, overwriteExisting);
            Defaults.SliderLabel = PickDefault(Defaults.SliderLabel, references.SliderLabel, overwriteExisting);
            Defaults.InputText = PickDefault(Defaults.InputText, references.InputText, overwriteExisting);
            Defaults.InputPlaceholder = PickDefault(Defaults.InputPlaceholder, references.InputPlaceholder, overwriteExisting);
            Defaults.InputField = PickDefault(Defaults.InputField, references.InputField, overwriteExisting);
            Defaults.Toggle = PickDefault(Defaults.Toggle, references.Toggle, overwriteExisting);
            Defaults.Slider = PickDefault(Defaults.Slider, references.Slider, overwriteExisting);
            Defaults.Scrollbar = PickDefault(Defaults.Scrollbar, references.Scrollbar, overwriteExisting);
            Defaults.Background = PickDefault(Defaults.Background, references.Background, overwriteExisting);
            Defaults.Shadow = PickDefault(Defaults.Shadow, references.Shadow, overwriteExisting);
            Defaults.Panel = PickDefault(Defaults.Panel, references.Panel, overwriteExisting);
        }

        public static void SetDefault(Button template, bool overwriteExisting = true)
        {
            Defaults.Button = PickDefault(Defaults.Button, template, overwriteExisting);
            Defaults.ButtonLabel = PickDefault(Defaults.ButtonLabel, template != null ? template.GetComponentInChildren<TMP_Text>(true) : null, overwriteExisting);
            Defaults.Shadow = PickDefault(Defaults.Shadow, template != null ? template.GetComponent<Shadow>() : null, overwriteExisting);
        }

        public static void SetDefault(TMP_Text template, bool overwriteExisting = true)
        {
            Defaults.Label = PickDefault(Defaults.Label, template, overwriteExisting);
        }

        public static void SetDefault(TMP_InputField template, bool overwriteExisting = true)
        {
            Defaults.InputField = PickDefault(Defaults.InputField, template, overwriteExisting);
            Defaults.InputText = PickDefault(Defaults.InputText, template != null ? template.textComponent : null, overwriteExisting);
            Defaults.InputPlaceholder = PickDefault(Defaults.InputPlaceholder, template != null && template.placeholder != null ? template.placeholder.GetComponent<TMP_Text>() : null, overwriteExisting);
        }

        public static void SetDefault(Toggle template, bool overwriteExisting = true)
        {
            Defaults.Toggle = PickDefault(Defaults.Toggle, template, overwriteExisting);
            Defaults.ToggleLabel = PickDefault(Defaults.ToggleLabel, template != null ? template.GetComponentInChildren<TMP_Text>(true) : null, overwriteExisting);
        }

        public static void SetDefault(MySliderUI template, bool overwriteExisting = true)
        {
            Defaults.Slider = PickDefault(Defaults.Slider, template, overwriteExisting);
            Defaults.SliderLabel = PickDefault(Defaults.SliderLabel, template != null ? template.GetComponentInChildren<TMP_Text>(true) : null, overwriteExisting);
        }

        public static void SetDefault(Scrollbar template, bool overwriteExisting = true)
        {
            Defaults.Scrollbar = PickDefault(Defaults.Scrollbar, template, overwriteExisting);
        }

        public static void SetDefault(Image template, bool overwriteExisting = true)
        {
            Defaults.Background = PickDefault(Defaults.Background, template, overwriteExisting);
        }

        public static void SetDefault(Shadow template, bool overwriteExisting = true)
        {
            Defaults.Shadow = PickDefault(Defaults.Shadow, template, overwriteExisting);
        }

        public static void SetPanelDefault(GameObject template, bool overwriteExisting = true)
        {
            Image background = template != null
                ? template.GetComponent<Image>() ?? FindBackgroundImage(template.transform)
                : null;
            Shadow shadow = background != null ? background.GetComponent<Shadow>() : template != null ? template.GetComponent<Shadow>() : null;

            Defaults.Panel = PickDefault(Defaults.Panel, background != null ? background.gameObject : template, overwriteExisting);
            Defaults.Background = PickDefault(Defaults.Background, background, overwriteExisting);
            Defaults.Shadow = PickDefault(Defaults.Shadow, shadow, overwriteExisting);
        }

        public static void CaptureDefaultsFrom(Transform root, bool overwriteExisting = false)
        {
            if (root == null)
                return;

            Button button = FindButtonTemplate(root);
            Toggle toggle = FindToggleTemplate(root);
            MySliderUI slider = root.GetComponentInChildren<MySliderUI>(true);
            TMP_InputField input = root.GetComponentInChildren<TMP_InputField>(true);
            TMP_Text buttonLabel = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            TMP_Text toggleLabel = toggle != null ? toggle.GetComponentInChildren<TMP_Text>(true) : null;
            TMP_Text sliderLabel = slider != null ? slider.GetComponentInChildren<TMP_Text>(true) : null;
            TMP_Text inputText = input != null ? input.textComponent : null;
            TMP_Text inputPlaceholder = input != null && input.placeholder != null ? input.placeholder.GetComponent<TMP_Text>() : null;

            SetDefaults(new DefaultReferences
            {
                Button = button,
                Label = root.GetComponentInChildren<TMP_Text>(true),
                ButtonLabel = buttonLabel,
                ToggleLabel = toggleLabel,
                SliderLabel = sliderLabel,
                InputText = inputText,
                InputPlaceholder = inputPlaceholder,
                InputField = input,
                Toggle = toggle,
                Slider = slider,
                Scrollbar = root.GetComponentInChildren<Scrollbar>(true),
                Background = FindBackgroundImage(root),
                Shadow = root.GetComponentInChildren<Shadow>(true),
                Panel = FindPanelTemplate(root)
            }, overwriteExisting);
        }

        public static void CaptureSceneDefaults(bool overwriteExisting = false)
        {
            GameObject createLobby = FindNamedObject("UI_CreateLobby");
            if (createLobby != null)
            {
                CaptureCreateLobbyDefaults(createLobby.transform, overwriteExisting);
                CaptureDefaultsFrom(createLobby.transform, overwriteExisting);
            }

            Button hostButton = FindNamedComponent<Button>("(Button) HOST", "(Button) Join", "(Button) JOIN");
            if (hostButton != null)
                SetDefault(hostButton, overwriteExisting);

            TMP_Text settingsHeader = FindNamedComponent<TMP_Text>(
                "(Text) quick changes",
                "(Text) SECTION TITLE",
                "(Text) chat related section");
            Defaults.HeaderLabel = PickDefault(Defaults.HeaderLabel, settingsHeader, overwriteExisting);

            GameObject mainMenuPanel = FindNamedObject("UI_MainMenu")?.transform.Find("Panel")?.gameObject;
            if (mainMenuPanel != null)
                CaptureDefaultsFrom(mainMenuPanel.transform, overwriteExisting);
        }

        public static Element Assume(GameObject gameObject)
        {
            return new Element(gameObject);
        }

        public static Element Assume(Component component)
        {
            return new Element(component != null ? component.gameObject : null);
        }

        public static Element Assume(Transform transform)
        {
            return new Element(transform != null ? transform.gameObject : null);
        }

        public static Element Find(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return new Element(null);

            Transform child = root.Find(path);
            return new Element(child != null ? child.gameObject : null);
        }

        public static Element Find(string sceneObjectName)
        {
            return new Element(string.IsNullOrEmpty(sceneObjectName) ? null : FindNamedObject(sceneObjectName));
        }

        public static Element Create(string name, Transform parent = null, bool active = true)
        {
            GameObject gameObject = new GameObject(string.IsNullOrEmpty(name) ? "UI Element" : name);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);

            EnsureRectTransform(gameObject);
            gameObject.SetActive(active);
            return new Element(gameObject);
        }

        public static Canvas CreateOverlayCanvas(
            string name = "UI Canvas",
            int sortingOrder = 30000,
            Vector2? referenceResolution = null)
        {
            EnsureEventSystem();
            CanvasScaler templateScaler = Object.FindObjectOfType<CanvasScaler>();

            GameObject canvasObject = Create(name).GameObject;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = templateScaler != null ? templateScaler.uiScaleMode : CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution ?? (templateScaler != null ? templateScaler.referenceResolution : new Vector2(2560f, 1440f));
            if (templateScaler != null)
            {
                scaler.screenMatchMode = templateScaler.screenMatchMode;
                scaler.matchWidthOrHeight = templateScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = templateScaler.referencePixelsPerUnit;
            }

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
                return;

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        public static Element Clone(GameObject template, Transform parent, string name = null, int? siblingIndex = null, bool active = true)
        {
            if (template == null)
                return new Element(null);

            GameObject clone = Object.Instantiate(template, parent);
            if (clone == null)
                return new Element(null);

            if (!string.IsNullOrEmpty(name))
                clone.name = name;
            if (siblingIndex.HasValue)
                clone.transform.SetSiblingIndex(siblingIndex.Value);

            clone.SetActive(active);
            return new Element(clone);
        }

        public static T CloneComponent<T>(T template, Transform parent, string name = null, int? siblingIndex = null, bool active = true)
            where T : Component
        {
            return Clone(template != null ? template.gameObject : null, parent, name, siblingIndex, active).Get<T>();
        }

        public static TMP_Text CloneText(TMP_Text template, Transform parent, string text, string name = null, int? siblingIndex = null)
        {
            TMP_Text clone = CloneComponent(template, parent, name, siblingIndex);
            return SetText(clone, text);
        }

        public static Button CloneButton(
            Button template,
            Transform parent,
            string name = null,
            string label = null,
            UnityAction onClick = null,
            int? siblingIndex = null,
            bool clearListeners = true)
        {
            Button clone = CloneComponent(template, parent, name, siblingIndex);
            if (clone == null)
                return null;

            if (label != null)
                SetText(clone.GetComponentInChildren<TMP_Text>(true), label);
            if (clearListeners)
                clone.onClick.RemoveAllListeners();
            ApplyButtonStyle(clone, template);
            if (onClick != null)
            {
                clone.onClick.AddListener(onClick);
            }

            return clone;
        }

        public static TMP_InputField CloneInputField(
            TMP_InputField template,
            Transform parent,
            string name = null,
            string text = null,
            string placeholder = null,
            int? siblingIndex = null,
            bool clearListeners = true)
        {
            TMP_InputField clone = CloneComponent(template, parent, name, siblingIndex);
            if (clone == null)
                return null;

            if (clearListeners)
                ClearInputFieldEvents(clone);
            if (text != null)
                clone.text = text;

            TMP_Text placeholderText = clone.placeholder != null ? clone.placeholder.GetComponent<TMP_Text>() : null;
            if (placeholderText != null && placeholder != null)
                SetText(placeholderText, placeholder);

            return clone;
        }

        public static Toggle CloneToggle(
            Toggle template,
            Transform parent,
            string name = null,
            string label = null,
            bool? isOn = null,
            UnityAction<bool> onValueChanged = null,
            int? siblingIndex = null,
            bool clearListeners = true)
        {
            Toggle clone = CloneComponent(template, parent, name, siblingIndex);
            if (clone == null)
                return null;

            if (label != null)
                SetText(clone.GetComponentInChildren<TMP_Text>(true), label);
            if (clearListeners)
                clone.onValueChanged.RemoveAllListeners();
            if (isOn.HasValue)
                clone.isOn = isOn.Value;
            ApplyToggleStyle(clone, template);
            if (onValueChanged != null)
                clone.onValueChanged.AddListener(onValueChanged);

            return clone;
        }

        public static TMP_Text CreateLabel(
            Transform parent,
            string text,
            string name = "Label",
            TMP_Text template = null,
            int? siblingIndex = null,
            bool active = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Label == null);
            TMP_Text label = CloneText(template ?? Defaults.Label, parent, text, name, siblingIndex);
            if (label == null)
                label = CreateFallbackLabel(parent, name, text);

            ResetLayoutSizing(label.gameObject);
            ApplyTextStyle(label, template ?? Defaults.Label);
            if (label != null)
                label.gameObject.SetActive(active);
            return label;
        }

        public static TMP_Text CreatePlainLabel(
            Transform parent,
            string text,
            string name = "Label",
            TMP_Text template = null,
            bool active = true)
        {
            Element element = Create(string.IsNullOrEmpty(name) ? "Label" : name, parent, active);
            TMP_Text label = element.GameObject.AddComponent<TextMeshProUGUI>();
            ApplyTextStyle(label, template ?? Defaults.Label);
            label.raycastTarget = false;
            SetText(label, text);
            ResetLayoutSizing(label.gameObject);
            return label;
        }

        public static Button CreateButton(
            Transform parent,
            string label,
            UnityAction onClick = null,
            string name = "Button",
            Button template = null,
            int? siblingIndex = null,
            bool active = true,
            bool cloneTemplate = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Button == null);
            Button button = cloneTemplate
                ? CloneButton(template ?? Defaults.Button, parent, name, label, onClick, siblingIndex)
                : null;
            if (button == null)
                button = CreateFallbackButton(parent, name, label, onClick, siblingIndex);

            ResetLayoutSizing(button.gameObject);
            ApplyButtonStyle(button, template ?? Defaults.Button);
            if (button != null)
                button.gameObject.SetActive(active);
            return button;
        }

        public static TMP_InputField CreateInputField(
            Transform parent,
            string text = "",
            string placeholder = null,
            string name = "Input Field",
            TMP_InputField template = null,
            UnityAction<string> onValueChanged = null,
            int? siblingIndex = null,
            bool active = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.InputField == null);
            TMP_InputField inputField = CloneInputField(template ?? Defaults.InputField, parent, name, text, placeholder, siblingIndex);
            if (inputField == null)
                inputField = CreateFallbackInputField(parent, name, text, placeholder, siblingIndex);
            if (inputField == null)
                return null;

            ResetLayoutSizing(inputField.gameObject);
            if (onValueChanged != null)
                inputField.onValueChanged.AddListener(onValueChanged);
            inputField.gameObject.SetActive(active);
            return inputField;
        }

        public static Toggle CreateToggle(
            Transform parent,
            string label,
            bool isOn = false,
            UnityAction<bool> onValueChanged = null,
            string name = "Toggle",
            Toggle template = null,
            int? siblingIndex = null,
            bool active = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Toggle == null);
            Toggle toggle = CloneToggle(template ?? Defaults.Toggle, parent, name, label, isOn, onValueChanged, siblingIndex);
            if (toggle == null)
                toggle = CreateFallbackToggle(parent, name, label, isOn, onValueChanged, siblingIndex);

            ResetLayoutSizing(toggle.gameObject);
            ApplyToggleStyle(toggle, template ?? Defaults.Toggle);
            if (toggle != null)
                toggle.gameObject.SetActive(active);
            return toggle;
        }

        public static MySliderUI CreateSlider(
            Transform parent,
            string name = "Slider",
            MySliderUI template = null,
            float? minValue = null,
            float? maxValue = null,
            float? value = null,
            UnityAction<float> onValueChanged = null,
            int? siblingIndex = null,
            bool active = true,
            bool clearListeners = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Slider == null);
            MySliderUI sliderUi = CloneComponent(template ?? Defaults.Slider, parent, name, siblingIndex);
            if (sliderUi == null || sliderUi.slider == null)
                return sliderUi;

            if (clearListeners)
            {
                sliderUi.slider.onValueChanged.RemoveAllListeners();
                sliderUi.OnSliderValueFinishedChanging = null;
            }
            if (minValue.HasValue)
                sliderUi.slider.minValue = minValue.Value;
            if (maxValue.HasValue)
                sliderUi.slider.maxValue = maxValue.Value;
            if (value.HasValue)
                sliderUi.slider.SetValueWithoutNotify(value.Value);
            if (onValueChanged != null)
                sliderUi.slider.onValueChanged.AddListener(onValueChanged);

            sliderUi.UpdateSliderValueDisplay();
            sliderUi.gameObject.SetActive(active);
            return sliderUi;
        }

        public static Scrollbar CreateScrollbar(
            Transform parent,
            string name = "Scrollbar",
            Scrollbar template = null,
            float? value = null,
            float? size = null,
            UnityAction<float> onValueChanged = null,
            int? siblingIndex = null,
            bool active = true,
            bool clearListeners = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Scrollbar == null);
            Scrollbar scrollbar = CloneComponent(template ?? Defaults.Scrollbar, parent, name, siblingIndex);
            if (scrollbar == null)
                return null;

            if (clearListeners)
                scrollbar.onValueChanged.RemoveAllListeners();
            if (size.HasValue)
                scrollbar.size = size.Value;
            if (value.HasValue)
                scrollbar.value = value.Value;
            if (onValueChanged != null)
                scrollbar.onValueChanged.AddListener(onValueChanged);

            scrollbar.gameObject.SetActive(active);
            return scrollbar;
        }

        public static Element CreatePanel(
            Transform parent,
            string name = "Panel",
            GameObject template = null,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null,
            int? siblingIndex = null,
            bool active = true,
            bool cloneTemplate = false)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Panel == null && Defaults.Background == null);
            Image explicitPanelBackground = template == null ? FindLobbyPanelBackground() : null;
            GameObject panelTemplate = template
                ?? (explicitPanelBackground != null ? explicitPanelBackground.gameObject : null)
                ?? Defaults.Panel
                ?? (Defaults.Background != null ? Defaults.Background.gameObject : null);

            if (cloneTemplate && panelTemplate != null)
            {
                Element clone = Clone(panelTemplate, parent, name, siblingIndex, active);
                NormalizeClonedUiRoot(clone.GameObject);
                SetRect(clone.RectTransform, anchoredPosition, sizeDelta);
                return clone;
            }

            Element panel = Create(name, parent, active);
            if (siblingIndex.HasValue)
                panel.SiblingIndex(siblingIndex.Value);

            Image panelImageTemplate = explicitPanelBackground ?? (panelTemplate != null ? panelTemplate.GetComponent<Image>() : null);
            panelImageTemplate ??= Defaults.Background;
            Image panelImage = CopyImage(panelImageTemplate, panel.GameObject);
            if (panelImage != null)
            {
                panelImage.raycastTarget = true;
                if (panelTemplate == null && Defaults.Background == null)
                    panelImage.color = FallbackPanelColor;
            }
            else if (panelTemplate == null && Defaults.Background == null)
            {
                panelImage = panel.GameObject.AddComponent<Image>();
                panelImage.color = FallbackPanelColor;
                panelImage.raycastTarget = true;
            }

            Shadow shadowTemplate = panelImageTemplate != null ? panelImageTemplate.GetComponent<Shadow>() : null;
            shadowTemplate ??= panelTemplate != null ? panelTemplate.GetComponent<Shadow>() : null;
            shadowTemplate ??= Defaults.Shadow;
            if (shadowTemplate != null)
                CopyShadow(shadowTemplate, panel.GameObject);

            LayoutElement layoutTemplate = panelTemplate != null ? panelTemplate.GetComponent<LayoutElement>() : null;
            if (layoutTemplate != null)
                CopyLayout(layoutTemplate, panel.GameObject);

            SetRect(panel.RectTransform, anchoredPosition, sizeDelta);
            return panel;
        }

        public static Image CreateBackground(
            Transform parent,
            string name = "Background",
            Image template = null,
            Color? color = null,
            Vector2? sizeDelta = null,
            Vector2? anchoredPosition = null,
            int? siblingIndex = null,
            bool active = true)
        {
            CaptureSceneDefaultsIf(template == null && Defaults.Background == null);
            Element backgroundElement = Create(name, parent, active);
            if (siblingIndex.HasValue)
                backgroundElement.SiblingIndex(siblingIndex.Value);

            Image background = CopyImage(template ?? Defaults.Background ?? (Defaults.Button != null ? Defaults.Button.GetComponent<Image>() : null), backgroundElement.GameObject);
            if (background == null)
                return null;

            if (color.HasValue)
                background.color = color.Value;
            else if (template == null && Defaults.Background == null && Defaults.Button == null)
                background.color = FallbackPanelColor;
            SetRect(background, anchoredPosition, sizeDelta);
            return background;
        }

        public static Image CreateScreenBackdrop(
            Transform parent,
            string name = "Backdrop",
            Image template = null,
            Color? color = null)
        {
            Element backdrop = Create(name, parent);
            Image image = CopyImage(template, backdrop.GameObject);
            if (image == null)
                return null;

            image.color = color ?? (template != null ? template.color : new Color(0f, 0f, 0f, 0.45f));
            image.raycastTarget = true;
            Stretch(backdrop.GameObject);
            return image;
        }

        public static RectTransform EnsureRectTransform(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            return rectTransform != null ? rectTransform : gameObject.AddComponent<RectTransform>();
        }

        public static void SetRect(
            Component component,
            Vector2? anchoredPosition = null,
            Vector2? sizeDelta = null,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Vector3? scale = null)
        {
            SetRect(component != null ? component.GetComponent<RectTransform>() : null, anchoredPosition, sizeDelta, anchorMin, anchorMax, pivot, scale);
        }

        public static void SetRect(
            RectTransform rectTransform,
            Vector2? anchoredPosition = null,
            Vector2? sizeDelta = null,
            Vector2? anchorMin = null,
            Vector2? anchorMax = null,
            Vector2? pivot = null,
            Vector3? scale = null)
        {
            if (rectTransform == null)
                return;

            if (anchoredPosition.HasValue)
                rectTransform.anchoredPosition = anchoredPosition.Value;
            if (sizeDelta.HasValue)
                rectTransform.sizeDelta = sizeDelta.Value;
            if (anchorMin.HasValue)
                rectTransform.anchorMin = anchorMin.Value;
            if (anchorMax.HasValue)
                rectTransform.anchorMax = anchorMax.Value;
            if (pivot.HasValue)
                rectTransform.pivot = pivot.Value;
            if (scale.HasValue)
                rectTransform.localScale = scale.Value;
        }

        public static void Reparent(Component component, Transform parent, bool worldPositionStays = false, int? siblingIndex = null)
        {
            if (component == null || parent == null)
                return;

            component.transform.SetParent(parent, worldPositionStays);
            if (siblingIndex.HasValue)
                component.transform.SetSiblingIndex(siblingIndex.Value);
        }

        public static void SetCanvasGroups(
            Transform root,
            float alpha = 1f,
            bool interactable = true,
            bool blocksRaycasts = true,
            bool includeInactive = true,
            bool setRootActive = false)
        {
            if (root == null)
                return;

            if (setRootActive)
                root.gameObject.SetActive(true);
            foreach (CanvasGroup canvasGroup in root.GetComponentsInChildren<CanvasGroup>(includeInactive))
            {
                canvasGroup.alpha = alpha;
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = blocksRaycasts;
            }
        }

        public static void ActivatePathToRoot(Transform start, Transform root)
        {
            for (Transform target = start; target != null; target = target.parent)
            {
                target.gameObject.SetActive(true);
                if (target == root)
                    break;
            }
        }

        public static void SetChildrenActive(Transform parent, bool active, string skipNameContains = null)
        {
            if (parent == null)
                return;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child == null)
                    continue;
                if (!string.IsNullOrEmpty(skipNameContains) && child.name.Contains(skipNameContains))
                    continue;

                child.gameObject.SetActive(active);
            }
        }

        public static VerticalLayoutGroup SetVerticalLayout(
            GameObject gameObject,
            RectOffset padding = null,
            float spacing = 0f,
            bool childControlWidth = true,
            bool childControlHeight = true,
            bool childForceExpandWidth = true,
            bool childForceExpandHeight = false)
        {
            if (gameObject == null)
                return null;

            VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = gameObject.AddComponent<VerticalLayoutGroup>();

            layout.padding = padding ?? new RectOffset();
            layout.spacing = spacing;
            layout.childControlWidth = childControlWidth;
            layout.childControlHeight = childControlHeight;
            layout.childForceExpandWidth = childForceExpandWidth;
            layout.childForceExpandHeight = childForceExpandHeight;
            return layout;
        }

        public static HorizontalLayoutGroup SetHorizontalLayout(
            GameObject gameObject,
            float spacing = 0f,
            RectOffset padding = null,
            bool childControlWidth = true,
            bool childControlHeight = true,
            bool childForceExpandWidth = false,
            bool childForceExpandHeight = false)
        {
            if (gameObject == null)
                return null;

            HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.padding = padding ?? new RectOffset();
            layout.spacing = spacing;
            layout.childControlWidth = childControlWidth;
            layout.childControlHeight = childControlHeight;
            layout.childForceExpandWidth = childForceExpandWidth;
            layout.childForceExpandHeight = childForceExpandHeight;
            return layout;
        }

        public static Element CreateHorizontalRow(Transform parent, string name = "Row", float height = 38f, float spacing = 14f)
        {
            Element row = Create(name, parent);
            SetHorizontalLayout(row.GameObject, spacing);
            SetFixedLayoutSize(row.GameObject, preferredHeight: height);
            return row;
        }

        public static LayoutElement SetFixedLayoutSize(
            GameObject gameObject,
            float? preferredWidth = null,
            float? preferredHeight = null,
            float? flexibleWidth = null,
            float? flexibleHeight = null)
        {
            LayoutElement layout = SetLayout(
                gameObject,
                preferredWidth,
                preferredHeight,
                flexibleWidth,
                flexibleHeight,
                preferredWidth,
                preferredHeight);

            if (layout != null)
            {
                layout.ignoreLayout = false;
                layout.layoutPriority = 1;
            }

            return layout;
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
            if (gameObject == null)
                return null;

            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();

            if (preferredWidth.HasValue)
                layout.preferredWidth = preferredWidth.Value;
            if (preferredHeight.HasValue)
                layout.preferredHeight = preferredHeight.Value;
            if (flexibleWidth.HasValue)
                layout.flexibleWidth = flexibleWidth.Value;
            if (flexibleHeight.HasValue)
                layout.flexibleHeight = flexibleHeight.Value;
            if (minWidth.HasValue)
                layout.minWidth = minWidth.Value;
            if (minHeight.HasValue)
                layout.minHeight = minHeight.Value;

            return layout;
        }

        public static void ResetLayoutSizing(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            RectTransform rect = EnsureRectTransform(gameObject);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;

            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                return;

            layout.ignoreLayout = false;
            layout.minWidth = -1f;
            layout.minHeight = -1f;
            layout.preferredWidth = -1f;
            layout.preferredHeight = -1f;
            layout.flexibleWidth = -1f;
            layout.flexibleHeight = -1f;
            layout.layoutPriority = 1;
        }

        public static void StabilizeClonedControl(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            LeanTween.cancel(gameObject);
            gameObject.transform.localScale = Vector3.one;

            foreach (ContentSizeFitter fitter in gameObject.GetComponentsInChildren<ContentSizeFitter>(true))
            {
                if (fitter != null)
                    fitter.enabled = false;
            }

            foreach (Selectable selectable in gameObject.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable != null)
                    selectable.transition = Selectable.Transition.ColorTint;
            }

            foreach (AnimationSelectableUi selectableUi in gameObject.GetComponentsInChildren<AnimationSelectableUi>(true))
            {
                if (selectableUi == null)
                    continue;

                LeanTween.cancel(selectableUi.gameObject);
                selectableUi.transform.localScale = Vector3.one;
                selectableUi.enabled = false;
                selectableUi.OnSelect = null;
                selectableUi.OnDeselect = null;
                Object.Destroy(selectableUi);
            }

            foreach (Component component in gameObject.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetIl2CppType().Name == "AnimationSelectableUi")
                {
                    if (component is Behaviour behaviour)
                        behaviour.enabled = false;
                    Object.Destroy(component);
                }
            }

            NormalizeClonedUiRoot(gameObject);
        }

        public static void SetTextMetrics(
            TMP_Text textComponent,
            float fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left,
            bool autoSize = false,
            float minFontSize = 12f)
        {
            if (textComponent == null)
                return;

            textComponent.fontSize = fontSize;
            textComponent.fontSizeMax = fontSize;
            textComponent.fontSizeMin = minFontSize;
            textComponent.enableAutoSizing = autoSize;
            textComponent.alignment = alignment;
            textComponent.fontStyle = FontStyles.Normal;
            textComponent.enableWordWrapping = false;
            textComponent.overflowMode = TextOverflowModes.Ellipsis;
            textComponent.characterSpacing = 0f;
            textComponent.wordSpacing = 0f;
            textComponent.lineSpacing = 0f;
            textComponent.paragraphSpacing = 0f;
            textComponent.ForceMeshUpdate();
        }

        public static void SetInputTextStyle(
            TMP_InputField input,
            float fontSize,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            bool autoSize = false,
            float minFontSize = 14f)
        {
            if (input == null)
                return;

            if (input.textComponent != null)
            {
                if (Defaults.InputText != null)
                    ApplyTextStyle(input.textComponent, Defaults.InputText);
                SetTextMetrics(input.textComponent, fontSize, alignment, autoSize, minFontSize);
            }

            TMP_Text placeholder = input.placeholder != null ? input.placeholder.GetComponent<TMP_Text>() : null;
            if (placeholder != null)
            {
                TMP_Text placeholderTemplate = Defaults.InputPlaceholder ?? Defaults.InputText;
                if (placeholderTemplate != null)
                    ApplyTextStyle(placeholder, placeholderTemplate);
                SetTextMetrics(placeholder, fontSize, alignment, autoSize, minFontSize);
            }
        }

        public static TMP_Text SetText(TMP_Text textComponent, string text, bool removeLocalization = true)
        {
            if (textComponent == null)
                return null;

            if (removeLocalization)
                RemoveLocalization(textComponent);

            textComponent.text = text ?? string.Empty;
            textComponent.ForceMeshUpdate();
            return textComponent;
        }

        public static void RemoveLocalization(Component component)
        {
            if (component == null)
                return;

            foreach (Component candidate in component.GetComponents<Component>())
            {
                if (candidate == null)
                    continue;

                string typeName = candidate.GetIl2CppType().Name;
                if (typeName.Contains("LocalizeStringEvent") ||
                    typeName.Contains("Localisation") ||
                    typeName.Contains("Localization"))
                {
                    Object.Destroy(candidate);
                }
            }
        }

        public static void ApplyTextStyle(TMP_Text target, TMP_Text template = null)
        {
            if (target == null)
                return;

            template ??= Defaults.Label;
            if (template == null)
            {
                target.color = Color.white;
                return;
            }

            target.font = template.font;
            target.fontSharedMaterial = template.fontSharedMaterial;
            target.fontStyle = template.fontStyle;
            target.color = template.color;
            target.alignment = template.alignment;
            target.enableWordWrapping = template.enableWordWrapping;
            target.overflowMode = template.overflowMode;
            target.characterSpacing = template.characterSpacing;
            target.wordSpacing = template.wordSpacing;
            target.lineSpacing = template.lineSpacing;
            target.paragraphSpacing = template.paragraphSpacing;
        }

        public static void ApplyButtonStyle(Button button, Button template = null)
        {
            if (button == null)
                return;

            template ??= Defaults.Button;
            Image imageTemplate = template != null ? template.GetComponent<Image>() : Defaults.Background;
            Image image = CopyImage(imageTemplate, button.gameObject);
            if (image != null)
            {
                if (imageTemplate == null)
                    image.color = FallbackButtonColor;
                button.targetGraphic = image;
            }

            Shadow shadowTemplate = template != null ? template.GetComponent<Shadow>() : Defaults.Shadow;
            if (shadowTemplate != null)
                CopyShadow(shadowTemplate, button.gameObject);

            if (template != null)
            {
                button.transition = template.transition;
                button.colors = template.colors;
                button.spriteState = template.spriteState;
                button.animationTriggers = template.animationTriggers;
                button.navigation = template.navigation;
            }

            ApplyTextStyle(button.GetComponentInChildren<TMP_Text>(true),
                template != null ? template.GetComponentInChildren<TMP_Text>(true) : (Defaults.ButtonLabel ?? Defaults.Label));
            NormalizeClonedUiRoot(button.gameObject);
        }

        public static void ApplyToggleStyle(Toggle toggle, Toggle template = null)
        {
            if (toggle == null)
                return;

            template ??= Defaults.Toggle;
            Image templateBackground = FindChildImage(template != null ? template.transform : null, "Background");
            Image templateCheckmark = FindChildImage(template != null ? template.transform : null, "Checkmark");

            GameObject background = FindOrCreateChild(toggle.transform, "Background");
            Image backgroundImage = CopyImage(templateBackground ?? Defaults.Background, background);
            if (backgroundImage != null && templateBackground == null && Defaults.Background == null)
                backgroundImage.color = FallbackInputColor;

            RectTransform backgroundRect = EnsureRectTransform(background);
            RectTransform templateBackgroundRect = templateBackground != null ? templateBackground.GetComponent<RectTransform>() : null;
            if (templateBackgroundRect != null)
                CopyRect(templateBackgroundRect, backgroundRect);
            else
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                backgroundRect.anchoredPosition = new Vector2(10f, 0f);
                backgroundRect.localScale = Vector3.one;
                backgroundRect.sizeDelta = new Vector2(20f, 20f);
            }

            GameObject checkmark = FindOrCreateChild(background.transform, "Checkmark");
            Image checkmarkImage = CopyImage(templateCheckmark, checkmark);
            if (checkmarkImage != null && templateCheckmark == null)
                checkmarkImage.color = FallbackToggleCheckColor;

            RectTransform checkmarkRect = EnsureRectTransform(checkmark);
            RectTransform templateCheckmarkRect = templateCheckmark != null ? templateCheckmark.GetComponent<RectTransform>() : null;
            if (templateCheckmarkRect != null)
                CopyRect(templateCheckmarkRect, checkmarkRect);
            else
            {
                checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
                checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
                checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
                checkmarkRect.anchoredPosition = Vector2.zero;
                checkmarkRect.localScale = Vector3.one;
                checkmarkRect.sizeDelta = new Vector2(10f, 10f);
            }

            toggle.targetGraphic = backgroundImage;
            toggle.graphic = checkmarkImage;
            if (toggle.GetComponent<MyToggleUI>() == null)
                toggle.gameObject.AddComponent<MyToggleUI>();

            if (template != null)
            {
                toggle.transition = template.transition;
                toggle.colors = template.colors;
                toggle.spriteState = template.spriteState;
                toggle.animationTriggers = template.animationTriggers;
                toggle.navigation = template.navigation;
            }

            ApplyTextStyle(toggle.GetComponentInChildren<TMP_Text>(true),
                template != null ? template.GetComponentInChildren<TMP_Text>(true) : (Defaults.ToggleLabel ?? Defaults.Label));
            NormalizeClonedUiRoot(toggle.gameObject);
        }

        public static Image SetImageColor(Component component, Color color)
        {
            Image image = component != null ? component.GetComponent<Image>() : null;
            if (image != null)
                image.color = color;
            return image;
        }

        public static Shadow SetShadowColor(Component component, Color color)
        {
            Shadow shadow = component != null ? component.GetComponent<Shadow>() : null;
            if (shadow != null)
                shadow.effectColor = color;
            return shadow;
        }

        public static void SetButtonColors(Button button, Color imageColor, Color shadowColor)
        {
            SetImageColor(button, imageColor);
            SetShadowColor(button, shadowColor);
        }

        public static Shadow ApplyDefaultShadow(Component component)
        {
            return Defaults.Shadow != null ? CopyShadow(Defaults.Shadow, component) : null;
        }

        public static Shadow CopyShadow(Shadow template, GameObject target)
        {
            return target != null ? CopyShadow(template, target.transform) : null;
        }

        public static Shadow CopyShadow(Shadow template, Component component)
        {
            if (component == null)
                return null;

            Shadow shadow = component.GetComponent<Shadow>();
            if (shadow == null)
                shadow = component.gameObject.AddComponent<Shadow>();

            if (template != null)
            {
                shadow.effectColor = template.effectColor;
                shadow.effectDistance = template.effectDistance;
                shadow.useGraphicAlpha = template.useGraphicAlpha;
            }

            return shadow;
        }

        public static Image CopyImage(Image template, GameObject target)
        {
            if (target == null)
                return null;

            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();

            if (template != null)
            {
                CopyGraphic(template, image);
                image.sprite = template.sprite;
                image.type = template.type;
                image.preserveAspect = template.preserveAspect;
                image.fillCenter = template.fillCenter;
                image.fillMethod = template.fillMethod;
                image.fillAmount = template.fillAmount;
                image.fillClockwise = template.fillClockwise;
                image.fillOrigin = template.fillOrigin;
                image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
            }

            return image;
        }

        public static void CopyGraphic(Graphic template, Graphic target)
        {
            if (template == null || target == null)
                return;

            target.color = template.color;
            target.material = template.material;
            target.raycastTarget = template.raycastTarget;
        }

        public static void CopyRect(RectTransform template, RectTransform target)
        {
            if (template == null || target == null)
                return;

            target.anchorMin = template.anchorMin;
            target.anchorMax = template.anchorMax;
            target.pivot = template.pivot;
            target.anchoredPosition = template.anchoredPosition;
            target.sizeDelta = template.sizeDelta;
            target.offsetMin = template.offsetMin;
            target.offsetMax = template.offsetMax;
            target.localScale = Vector3.one;
        }

        public static LayoutElement CopyLayout(LayoutElement template, GameObject target)
        {
            if (target == null)
                return null;

            LayoutElement layout = target.GetComponent<LayoutElement>();
            if (layout == null)
                layout = target.AddComponent<LayoutElement>();

            if (template != null)
            {
                layout.ignoreLayout = template.ignoreLayout;
                layout.minWidth = template.minWidth;
                layout.minHeight = template.minHeight;
                layout.preferredWidth = template.preferredWidth;
                layout.preferredHeight = template.preferredHeight;
                layout.flexibleWidth = template.flexibleWidth;
                layout.flexibleHeight = template.flexibleHeight;
                layout.layoutPriority = template.layoutPriority;
            }

            return layout;
        }

        public static void ClearInputFieldEvents(TMP_InputField inputField)
        {
            if (inputField == null)
                return;

            inputField.onValueChanged.RemoveAllListeners();
            inputField.onSubmit.RemoveAllListeners();
            inputField.onSelect.RemoveAllListeners();
            inputField.onDeselect.RemoveAllListeners();
        }

        private static TMP_Text CreateFallbackLabel(Transform parent, string name, string text)
        {
            Element element = Create(string.IsNullOrEmpty(name) ? "Label" : name, parent);
            TMP_Text label = element.GameObject.AddComponent<TextMeshProUGUI>();
            ApplyTextStyle(label, Defaults.Label);
            label.raycastTarget = false;
            SetText(label, text);
            return label;
        }

        private static Button CreateFallbackButton(
            Transform parent,
            string name,
            string label,
            UnityAction onClick,
            int? siblingIndex)
        {
            Element element = Create(string.IsNullOrEmpty(name) ? "Button" : name, parent);
            if (siblingIndex.HasValue)
                element.SiblingIndex(siblingIndex.Value);

            Image image = CopyImage(Defaults.Button != null ? Defaults.Button.GetComponent<Image>() : Defaults.Background, element.GameObject);
            if (image != null && Defaults.Button == null && Defaults.Background == null)
                image.color = FallbackButtonColor;

            Button button = element.GameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ApplyButtonStyle(button, Defaults.Button);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            TMP_Text text = CreateFallbackLabel(element.Transform, "Text", label);
            Stretch(text.gameObject);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_InputField CreateFallbackInputField(
            Transform parent,
            string name,
            string text,
            string placeholder,
            int? siblingIndex)
        {
            Element element = Create(string.IsNullOrEmpty(name) ? "Input Field" : name, parent);
            if (siblingIndex.HasValue)
                element.SiblingIndex(siblingIndex.Value);

            Image image = CopyImage(
                Defaults.InputField != null ? Defaults.InputField.GetComponent<Image>() :
                Defaults.Button != null ? Defaults.Button.GetComponent<Image>() :
                Defaults.Background,
                element.GameObject);
            if (image != null && Defaults.InputField == null && Defaults.Button == null && Defaults.Background == null)
                image.color = FallbackInputColor;

            TMP_InputField input = element.GameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = image;
            input.lineType = TMP_InputField.LineType.SingleLine;

            TMP_Text placeholderText = CreateInputText(element.Transform, "Placeholder", placeholder ?? string.Empty, new Color(0.42f, 0.45f, 0.5f, 0.85f));
            TMP_Text textComponent = CreateInputText(element.Transform, "Text", text ?? string.Empty, Color.black);
            input.placeholder = placeholderText;
            input.textComponent = textComponent;
            input.text = text ?? string.Empty;
            return input;
        }

        private static TMP_Text CreateInputText(Transform parent, string name, string text, Color color)
        {
            TMP_Text textComponent = CreateFallbackLabel(parent, name, text);
            textComponent.color = color;
            textComponent.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform rect = Stretch(textComponent.gameObject);
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            return textComponent;
        }

        private static Toggle CreateFallbackToggle(
            Transform parent,
            string name,
            string label,
            bool isOn,
            UnityAction<bool> onValueChanged,
            int? siblingIndex)
        {
            Element element = Create(string.IsNullOrEmpty(name) ? "Toggle" : name, parent);
            if (siblingIndex.HasValue)
                element.SiblingIndex(siblingIndex.Value);

            Toggle toggle = element.GameObject.AddComponent<Toggle>();
            if (!string.IsNullOrEmpty(label))
            {
                TMP_Text labelText = CreateFallbackLabel(element.Transform, "Text", label);
                RectTransform labelRect = Stretch(labelText.gameObject);
                labelRect.offsetMin = new Vector2(46f, 0f);
                labelText.alignment = TextAlignmentOptions.MidlineLeft;
            }

            toggle.isOn = isOn;
            ApplyToggleStyle(toggle, Defaults.Toggle);
            if (onValueChanged != null)
                toggle.onValueChanged.AddListener(onValueChanged);
            return toggle;
        }

        private static T PickDefault<T>(T current, T candidate, bool overwriteExisting) where T : class
        {
            if (candidate == null)
                return current;
            return overwriteExisting || current == null ? candidate : current;
        }

        private static void CaptureSceneDefaultsIf(bool shouldCapture)
        {
            if (!shouldCapture || !AutoCaptureSceneDefaults || _lastAutoCaptureFrame == Time.frameCount)
                return;

            _lastAutoCaptureFrame = Time.frameCount;
            CaptureSceneDefaults(overwriteExisting: false);
        }

        private static void CaptureCreateLobbyDefaults(Transform root, bool overwriteExisting)
        {
            if (root == null)
                return;

            Image background = root.Find("Panels/Lobby Settings (mini)/(Image) Background")?.GetComponent<Image>();
            Button button = root.Find("Panels/Lobby Settings (mini)/(Button) CONFIRM HOST")?.GetComponent<Button>()
                ?? root.Find("Panels/Lobby Settings (mini)/ButtonContainer/(Button) Close Menu")?.GetComponent<Button>();
            TMP_InputField input = root.Find("Panels/Lobby Settings (mini)/CustomLobbyNameInput")?.GetComponent<TMP_InputField>()
                ?? root.Find("Panels/Lobby Settings (mini)/password toggles/(Input) lobby setting password")?.GetComponent<TMP_InputField>();
            Toggle toggle = root.Find("Panels/Lobby Settings (mini)/lobby type toggles/(Toggle) Game Type public/private")?.GetComponent<Toggle>()
                ?? root.Find("Panels/Lobby Settings (mini)/password toggles/(Toggle) uses password")?.GetComponent<Toggle>();
            MySliderUI slider = root.Find("Panels/Lobby Settings (mini)/(Slider) player count slider")?.GetComponent<MySliderUI>();
            Scrollbar scrollbar = root.GetComponentInChildren<Scrollbar>(true);
            TMP_Text label = root.Find("Panels/Lobby Settings (mini)/(Text) Settings Text")?.GetComponent<TMP_Text>()
                ?? root.Find("Panels/Lobby Settings (mini)/(Text) Lobby Settings")?.GetComponent<TMP_Text>()
                ?? button?.GetComponentInChildren<TMP_Text>(true)
                ?? toggle?.GetComponentInChildren<TMP_Text>(true);
            TMP_Text inputText = input != null ? input.textComponent : null;
            TMP_Text inputPlaceholder = input != null && input.placeholder != null ? input.placeholder.GetComponent<TMP_Text>() : null;

            SetDefaults(new DefaultReferences
            {
                Button = button,
                Label = label,
                ButtonLabel = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null,
                ToggleLabel = toggle != null ? toggle.GetComponentInChildren<TMP_Text>(true) : null,
                SliderLabel = slider != null ? slider.GetComponentInChildren<TMP_Text>(true) : null,
                InputText = inputText,
                InputPlaceholder = inputPlaceholder,
                InputField = input,
                Toggle = toggle,
                Slider = slider,
                Scrollbar = scrollbar,
                Background = background,
                Shadow = background != null ? background.GetComponent<Shadow>() : null,
                Panel = background != null ? background.gameObject : null
            }, overwriteExisting);
        }

        private static Button FindButtonTemplate(Transform root)
        {
            if (root == null)
                return null;

            Button fallback = null;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button == null || button.GetComponent<Image>() == null)
                    continue;

                string name = button.name.ToLowerInvariant();
                if (name.Contains("host") || name.Contains("join") || name.Contains("settings") || name.Contains("create"))
                    return button;

                fallback ??= button;
            }

            return fallback;
        }

        private static Toggle FindToggleTemplate(Transform root)
        {
            if (root == null)
                return null;

            Toggle fallback = null;
            foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle == null)
                    continue;

                if (toggle.GetComponent<MyToggleUI>() != null &&
                    FindChildImage(toggle.transform, "Background") != null &&
                    FindChildImage(toggle.transform, "Checkmark") != null)
                {
                    return toggle;
                }

                fallback ??= toggle;
            }

            return fallback;
        }

        private static T FindNamedComponent<T>(params string[] names) where T : Component
        {
            foreach (string name in names)
            {
                GameObject gameObject = FindNamedObject(name);
                T component = gameObject != null ? gameObject.GetComponent<T>() : null;
                if (component != null)
                    return component;
            }

            return null;
        }

        private static GameObject FindNamedObject(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            GameObject activeObject = GameObject.Find(name);
            if (activeObject != null)
                return activeObject;

            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject != null && gameObject.name == name)
                    return gameObject;
            }

            return null;
        }

        private static GameObject FindPanelTemplate(Transform root)
        {
            Image background = FindBackgroundImage(root);
            return background != null ? background.gameObject : null;
        }

        private static Image FindLobbyPanelBackground()
        {
            Transform lobbyBackground = FindNamedObject("UI_CreateLobby")?.transform.Find("Panels/Lobby Settings (mini)/(Image) Background");
            Image image = lobbyBackground != null ? lobbyBackground.GetComponent<Image>() : null;
            if (image != null)
                return image;

            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject == null || gameObject.name != "(Image) Background")
                    continue;

                image = gameObject.GetComponent<Image>();
                if (image != null && HasAncestor(gameObject.transform, "Lobby Settings"))
                    return image;
            }

            return null;
        }

        private static bool HasAncestor(Transform transform, string nameContains)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name.Contains(nameContains))
                    return true;
            }

            return false;
        }

        private static Image FindBackgroundImage(Transform root)
        {
            if (root == null)
                return null;

            Image fallback = null;
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                string imageName = image.name.ToLowerInvariant();
                string spriteName = image.sprite != null ? image.sprite.name.ToLowerInvariant() : string.Empty;
                if (IsControlImage(image) ||
                    imageName.Contains("fade") ||
                    imageName.Contains("icon") ||
                    spriteName.Contains("icon"))
                {
                    continue;
                }

                if (imageName.Contains("panel") || imageName.Contains("background"))
                    return image;

                if (fallback == null)
                    fallback = image;
            }

            return fallback ?? root.GetComponentInChildren<Image>(true);
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            Image childImage = FindChildImage(parent, name);
            if (childImage != null)
                return childImage.gameObject;

            Transform directChild = parent.Find(name);
            if (directChild != null)
                return directChild.gameObject;

            return Create(name, parent).GameObject;
        }

        private static Image FindChildImage(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image.name == name)
                    return image;
            }

            return null;
        }

        private static void NormalizeClonedUiRoot(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            foreach (RectTransform rect in gameObject.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect != null)
                    rect.localScale = Vector3.one;
            }
        }

        public static RectTransform Stretch(GameObject gameObject)
        {
            RectTransform rect = EnsureRectTransform(gameObject);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static bool IsControlImage(Image image)
        {
            if (image == null)
                return false;

            for (Transform current = image.transform; current != null; current = current.parent)
            {
                if (current.GetComponent<Button>() != null ||
                    current.GetComponent<Toggle>() != null ||
                    current.GetComponent<Slider>() != null ||
                    current.GetComponent<Scrollbar>() != null ||
                    current.GetComponent<TMP_InputField>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
