using System;
using System.Collections;
using Il2Cpp;
using Il2CppTMPro;
using Il2Cpp_Scripts.UI.Pre_Game;
using MelonLoader;
using MultiplayerTools;
using UnityEngine;
using UnityEngine.UI;

namespace MultiplayerTools.Features.Lobby
{
    internal sealed class SearchLobbiesUiController
    {
        public static SearchLobbiesUiController Instance { get; } = new SearchLobbiesUiController();

        private bool _watcherRunning;
        private bool _panelLoopRunning;
        private bool _suppressCapture;

        private SearchLobbiesUiController()
        {
        }

        public void ConfigureMainMenuOnEnable(UIMainMenu mainMenu)
        {
            if (_watcherRunning)
                return;

            _watcherRunning = true;
            MelonCoroutines.Start(WatchLobbyExplorerPanel());
        }

        private IEnumerator WatchLobbyExplorerPanel()
        {
            bool wasActive = false;

            while (true)
            {
                yield return null;

                Transform root = NativeUiBackend.Find("UI_Lobbies").Transform;
                bool isOpen = IsOpen(root);
                if (isOpen && !wasActive && !_panelLoopRunning)
                    MelonCoroutines.Start(HandleLobbyExplorerOpened(root));

                wasActive = isOpen;
            }
        }

        private IEnumerator HandleLobbyExplorerOpened(Transform root)
        {
            _panelLoopRunning = true;
            _suppressCapture = true;

            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;

                if (!IsOpen(root))
                {
                    _suppressCapture = false;
                    _panelLoopRunning = false;
                    yield break;
                }

                ApplyLobbyExplorerPreferences(root);
            }

            _suppressCapture = false;

            while (IsOpen(root))
            {
                CaptureLobbyExplorerPreferenceChanges(root);
                yield return null;
            }

            _panelLoopRunning = false;
        }

        private void ApplyLobbyExplorerPreferences(Transform root)
        {
            if (root == null)
                return;

            try
            {
                LobbyExplorerControls controls = GetControls(root);

                ApplyMaxPlayers(controls.MaxPlayersFilterSlider);
                SetToggleWithoutNotify(controls.ShowLockedLobbiesToggle, MultiplayerToolsCore.SearchShowLocked);
                SetToggleWithoutNotify(controls.ShowModdedLobbiesToggle, MultiplayerToolsCore.SearchShowModded);
                SetToggleWithoutNotify(controls.ShowOnlyYourLanguageToggle, MultiplayerToolsCore.SearchShowYourLanguageOnly);
                SetToggleWithoutNotify(controls.OnlyPeacefulModeToggle, MultiplayerToolsCore.SearchOnlyPeacefulLobbies);
                SetToggleWithoutNotify(controls.OnlyNonPeacefulModeToggle, MultiplayerToolsCore.SearchHidePeacefulLobbies);
                SetToggleWithoutNotify(controls.CrossplayToggle, MultiplayerToolsCore.SearchCrossplay);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] SearchLobbiesUiController failed to apply UI_Lobbies preferences: {ex}");
            }
        }

        private void CaptureLobbyExplorerPreferenceChanges(Transform root)
        {
            if (root == null || _suppressCapture)
                return;

            try
            {
                LobbyExplorerControls controls = GetControls(root);

                if (controls.MaxPlayersFilterSlider?.slider != null)
                {
                    int value = Mathf.RoundToInt(controls.MaxPlayersFilterSlider.slider.value);
                    if (value != MultiplayerToolsCore.SearchMaxPlayers)
                        MultiplayerToolsCore.SetSearchMaxPlayers(value);
                }

                SaveToggleIfChanged(controls.ShowLockedLobbiesToggle, MultiplayerToolsCore.SearchShowLocked, MultiplayerToolsCore.SetSearchShowLocked);
                SaveToggleIfChanged(controls.ShowModdedLobbiesToggle, MultiplayerToolsCore.SearchShowModded, MultiplayerToolsCore.SetSearchShowModded);
                SaveToggleIfChanged(controls.ShowOnlyYourLanguageToggle, MultiplayerToolsCore.SearchShowYourLanguageOnly, MultiplayerToolsCore.SetSearchShowYourLanguageOnly);
                SaveToggleIfChanged(controls.OnlyPeacefulModeToggle, MultiplayerToolsCore.SearchOnlyPeacefulLobbies, MultiplayerToolsCore.SetSearchOnlyPeacefulLobbies);
                SaveToggleIfChanged(controls.OnlyNonPeacefulModeToggle, MultiplayerToolsCore.SearchHidePeacefulLobbies, MultiplayerToolsCore.SetSearchHidePeacefulLobbies);
                SaveToggleIfChanged(controls.CrossplayToggle, MultiplayerToolsCore.SearchCrossplay, MultiplayerToolsCore.SetSearchCrossplay);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MultiplayerTools] SearchLobbiesUiController failed to capture UI_Lobbies preferences: {ex}");
            }
        }

        private static bool IsOpen(Transform root)
        {
            Transform panel = root != null ? root.Find("Panel") : null;
            return panel != null && panel.gameObject != null && panel.gameObject.activeSelf;
        }

        private static void ApplyMaxPlayers(MySliderUI sliderUi)
        {
            if (sliderUi?.slider == null)
                return;

            Slider slider = sliderUi.slider;
            slider.minValue = 0f;
            slider.maxValue = 64f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(MultiplayerToolsCore.SearchMaxPlayers);
            sliderUi.UpdateSliderValueDisplay();
            RefreshMaxPlayersDisplay(sliderUi);
        }

        private static void RefreshMaxPlayersDisplay(MySliderUI sliderUi)
        {
            Transform sliderRoot = sliderUi != null ? sliderUi.transform : null;
            if (sliderRoot == null || sliderUi.slider == null)
                return;

            int value = Mathf.RoundToInt(sliderUi.slider.value);
            TMP_Text valueText = FindText(sliderRoot, "(Text) Slider Value Text");
            TMP_Text anyText = FindText(sliderRoot, "(Text) Slider ANY Text");

            if (valueText != null)
            {
                valueText.text = value.ToString();
                valueText.gameObject.SetActive(value != 0);
                valueText.ForceMeshUpdate();
            }

            if (anyText != null)
                anyText.gameObject.SetActive(value == 0);
        }

        private static void SetToggleWithoutNotify(Toggle toggle, bool value)
        {
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(value);
        }

        private static void SaveToggleIfChanged(Toggle toggle, bool preference, Action<bool> setPreference)
        {
            if (toggle != null && toggle.isOn != preference)
                setPreference(toggle.isOn);
        }

        private static LobbyExplorerControls GetControls(Transform root)
        {
            return new LobbyExplorerControls
            {
                MaxPlayersFilterSlider = FindSlider(root, "Panel/layout group/Filter Menu/(Slider) max players filter"),
                ShowLockedLobbiesToggle = FindToggle(root, "Panel/layout group/Filter Menu/Lobby Filters/(Toggle) show locked lobbies", "show locked lobbies"),
                ShowModdedLobbiesToggle = FindToggle(root, "Panel/layout group/Filter Menu/Lobby Filters/(Toggle) show modded lobbies", "show modded lobbies"),
                OnlyPeacefulModeToggle = FindToggle(root, "Panel/layout group/Filter Menu/peaceful/(Toggle) only peaceful lobbies", "only peaceful lobbies"),
                OnlyNonPeacefulModeToggle = FindToggle(root, "Panel/layout group/Filter Menu/peaceful/(Toggle) no peaceful lobbies", "no peaceful lobbies"),
                ShowOnlyYourLanguageToggle = FindToggle(root, "Panel/layout group/Filter Menu/Lobby Filters/(Toggle) show your language only", "show your language only"),
                CrossplayToggle = FindToggle(root, "Panel/layout group/Filter Menu/Lobby Filters/(Toggle) crossplay", "crossplay")
            };
        }

        private static MySliderUI FindSlider(Transform root, string path)
        {
            Transform transform = root?.Find(path);
            return transform != null
                ? transform.GetComponent<MySliderUI>() ?? transform.GetComponentInChildren<MySliderUI>(true)
                : root?.GetComponentInChildren<MySliderUI>(true);
        }

        private static Toggle FindToggle(Transform root, string path, string label)
        {
            Transform transform = root?.Find(path);
            Toggle toggle = transform != null ? transform.GetComponent<Toggle>() : null;
            if (toggle != null)
                return toggle;

            return FindToggleByLabel(root, label);
        }

        private static Toggle FindToggleByLabel(Transform root, string label)
        {
            if (root == null || string.IsNullOrWhiteSpace(label))
                return null;

            string normalizedLabel = label.Trim().ToLowerInvariant();
            foreach (Toggle toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                TMP_Text text = toggle.GetComponentInChildren<TMP_Text>(true);
                if (text?.text != null && text.text.Trim().ToLowerInvariant().Contains(normalizedLabel))
                    return toggle;
            }

            return null;
        }

        private static TMP_Text FindText(Transform root, string childName)
        {
            Transform child = root != null ? root.Find(childName) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private sealed class LobbyExplorerControls
        {
            public MySliderUI MaxPlayersFilterSlider { get; set; }
            public Toggle ShowLockedLobbiesToggle { get; set; }
            public Toggle ShowModdedLobbiesToggle { get; set; }
            public Toggle OnlyPeacefulModeToggle { get; set; }
            public Toggle OnlyNonPeacefulModeToggle { get; set; }
            public Toggle ShowOnlyYourLanguageToggle { get; set; }
            public Toggle CrossplayToggle { get; set; }
        }
    }
}
