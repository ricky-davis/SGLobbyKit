# System Map

## Startup Flow

1. `MultiplayerToolsCore.OnInitializeMelon` creates MelonPreferences entries and saves them.
2. `HarmonyInstance.PatchAll()` applies patches from `ChatSystem`, `SettingsCommand`, `LobbyPatchFeatures`, and player join/leave patches.
3. Scene load starts reference loading in `MultiplayerToolsCore.LoadReferences`.
4. UI behavior is activated lazily by patched game methods or chat commands.

## UI Entry Points

- `ChatSystem` command table registers `!settings`.
- `SettingsCommand.HandleCommand` opens the mod settings menu.
- `LobbyPatches.UIMainMenu_OnEnable_Postfix` delegates native main menu and embedded create-lobby setup to `LobbyUiController`.
- `LobbyPatches` prefixes lobby creation calls and delegates selected lobby name/max-player enforcement to `LobbyUiController`.

## Native UI Adapter Responsibilities Today

The UI helper layer is split by responsibility:

- `SleddingUiAdapter` wraps existing native objects and coordinates template capture.
- `NativeUiTemplates` owns access to the shared captured template catalog.
- `NativeUiTemplateSet` stores captured native button, label, input, toggle, slider, scrollbar, panel, image, and shadow templates.
- `NativeUiFactory` creates/clones native-style controls from captured templates.
- `NativeUiBuilder` owns common row/grid composition helpers.
- `UiStyles` owns text/image/shadow style helpers.
- `UiLayout` owns rect, layout, activation, and cloned-control stabilization helpers.
- `UiElement` owns the chainable `GameObject`/`Transform` wrapper.
- `NativeUiBackend` is the internal implementation detail for native object lookup, template capture, cloning, styling, and layout.

Feature code should use the focused helper classes above instead of calling `NativeUiBackend` directly.

## Settings Menu Responsibilities Today

`SettingsCommand` is now a thin command/Harmony shell. `SettingsMenuController` owns lifecycle and staged state, while `SettingsMenuView` owns native-template view construction.

- Command handler for `!settings`.
- Root object creation and destruction.
- Native menu integration through `UiReferenceController.OpenMenu`.
- Forced chat close through `ChatManager` and Harmony `Traverse`.
- Cursor restore on close.
- Escape-to-close patch.
- Modal backdrop, panel, scroll viewport, header, close button.
- Row builders for inputs, toggles, dividers, sliders, and grid rows.
- Required native template validation before opening.
- Staged draft binding through `SettingsDraft`; preferences are committed only by Apply actions.

Simple one-label/one-control settings rows are generated from `SettingsSchema`. Password and join/leave message rows remain explicit compound builders.

## Lobby Patch Responsibilities Today

`LobbyPatches` is now a thin Harmony shell. `LobbyUiController` coordinates the lobby feature and delegates focused work to `MainMenuAdapter`, `LobbyMenuAdapter`, and `LobbyCreateSubmission`.

The lobby feature owns:

- Custom lobby name fallback logic.
- Replacement/cloning of the lobby name field.
- Max-player slider mutation to support 64 players.
- Binding native lobby toggles/password input to preferences.
- Adding the guest bang command toggle to native lobby settings.
- Main menu layout changes.
- Embedded create-lobby activation coroutine.
- Native close-button hiding.

Patch hooks, native lobby view adaptation, and lobby submission data handling have been split behind focused adapter/controller methods.

## State And Data Flow

- Preferences live in static `MelonPreferences_Entry` fields in `MultiplayerToolsCore`.
- Native create-lobby UI controls read static properties and call static setter methods immediately on value changes.
- The settings window should read static properties into a staged draft and call static setter methods only when Apply is pressed.
- Lobby creation prefixes read UI controls when available and fall back to preferences.
- The settings window reads static properties into a staged draft and calls static setter methods only when Apply is pressed.

## User Interaction Boundaries

- Chat command boundary: `!settings`.
- Native main menu boundary: `UIMainMenu.OnEnable`.
- Native create-lobby boundary: `UICreateLobby`, `LobbyManager.CreateLobby`, `EOSLobbyManager.CreateLobby`.
- Unity UI boundary: `Canvas`, `RectTransform`, layout groups, `TMP_Text`, `TMP_InputField`, `Toggle`, `Button`, `MySliderUI`.
