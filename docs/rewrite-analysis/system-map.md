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

## UILib Responsibilities Today

`UILib` is now a temporary compatibility facade over the older helper implementation. The target adapter files exist and delegate into `UILib` while call sites migrate:

- `UiElement` owns the chainable `GameObject`/`Transform` wrapper; `UILib.Element` derives from it for compatibility.
- `NativeUiTemplates` owns template capture and access.
- `UiStyles` owns style-copying entry points.
- `UiLayout` owns rect/layout entry points.
- `NativeUiFactory` owns native control clone/create entry points.
- `NativeUiBuilder` owns common row/grid composition entry points.

The remaining `UILib` implementation still contains these responsibilities:

1. Template discovery and style capture
   - `DefaultReferences`
   - `CaptureDefaultsFrom`
   - `CaptureSceneDefaults`
   - hard-coded create-lobby and main-menu path searches.

2. Primitive factory and clone helpers
   - labels, buttons, inputs, toggles, sliders, scrollbars, panels, backgrounds, backdrops, scroll viewports.

3. Layout and normalization helpers
   - vertical/horizontal layout groups.
   - manual grid track layout.
   - layout element sizing.
   - cloned control stabilization and animation cleanup.

The remaining migration work is to move feature call sites to the adapter files, then obsolete or remove unused compatibility methods.

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

Patch hooks, native lobby view adaptation, and lobby submission data handling have been split. Remaining lobby rewrite work should continue reducing `LobbyUiController` by moving native control composition and binding details behind clearer adapter methods.

## State And Data Flow

- Preferences live in static `MelonPreferences_Entry` fields in `MultiplayerToolsCore`.
- Native create-lobby UI controls read static properties and call static setter methods immediately on value changes.
- The settings window should read static properties into a staged draft and call static setter methods only when Apply is pressed.
- Lobby creation prefixes read UI controls when available and fall back to preferences.
- The current settings menu applies changes immediately. The rewrite intentionally changes the settings window to staged edits with an Apply button.

## User Interaction Boundaries

- Chat command boundary: `!settings`.
- Native main menu boundary: `UIMainMenu.OnEnable`.
- Native create-lobby boundary: `UICreateLobby`, `LobbyManager.CreateLobby`, `EOSLobbyManager.CreateLobby`.
- Unity UI boundary: `Canvas`, `RectTransform`, layout groups, `TMP_Text`, `TMP_InputField`, `Toggle`, `Button`, `MySliderUI`.
