# System Map

## Startup Flow

1. `MultiplayerToolsCore.OnInitializeMelon` creates MelonPreferences entries and saves them.
2. `HarmonyInstance.PatchAll()` applies patches from `ChatSystem`, `SettingsCommand`, `LobbyPatchFeatures`, and player join/leave patches.
3. Scene load starts reference loading in `MultiplayerToolsCore.LoadReferences`.
4. UI behavior is activated lazily by patched game methods or chat commands.

## UI Entry Points

- `ChatSystem` command table registers `!settings`.
- `SettingsCommand.HandleCommand` opens the mod settings menu.
- `LobbyPatchFeatures.UIMainMenu_OnEnable_Postfix` modifies the native main menu and embedded create-lobby UI.
- `LobbyPatchFeatures` prefixes lobby creation calls to enforce the selected lobby name and max players.

## UILib Responsibilities Today

`UILib` mixes four responsibilities:

1. Element handle/wrapper
   - `Element` wraps `GameObject` and provides chainable transform/layout helpers.

2. Template discovery and style capture
   - `DefaultReferences`
   - `CaptureDefaultsFrom`
   - `CaptureSceneDefaults`
   - hard-coded create-lobby and main-menu path searches.

3. Primitive factory and clone helpers
   - labels, buttons, inputs, toggles, sliders, scrollbars, panels, backgrounds, backdrops, scroll viewports.

4. Layout and normalization helpers
   - vertical/horizontal layout groups.
   - manual grid track layout.
   - layout element sizing.
   - cloned control stabilization and animation cleanup.

The target shape should keep these capabilities but make each responsibility explicit.

## Settings Menu Responsibilities Today

`SettingsCommand` owns:

- Command handler for `!settings`.
- Root object creation and destruction.
- Native menu integration through `UiReferenceController.OpenMenu`.
- Forced chat close through `ChatManager` and Harmony `Traverse`.
- Cursor restore on close.
- Escape-to-close patch.
- Modal backdrop, panel, scroll viewport, header, close button.
- Row builders for inputs, toggles, dividers, sliders, and grid rows.
- Direct preference binding to `MultiplayerToolsCore`.

The target shape should move view construction into a dedicated settings view builder and keep the Harmony/command class thin.

## Lobby Patch Responsibilities Today

`LobbyPatchFeatures` owns:

- Custom lobby name fallback logic.
- Replacement/cloning of the lobby name field.
- Max-player slider mutation to support 64 players.
- Binding native lobby toggles/password input to preferences.
- Adding the guest bang command toggle to native lobby settings.
- Prefix patches for `LobbyManager.CreateLobby` and `EOSLobbyManager.CreateLobby`.
- Main menu layout changes.
- Embedded create-lobby activation coroutine.
- Native close-button hiding.

The target shape should split patch hooks, native lobby view adaptation, and lobby submission data handling.

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
