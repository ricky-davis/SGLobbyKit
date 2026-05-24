# Repository Map

## Project

MultiplayerTools is a C# `net6.0` MelonLoader mod for Sledding Game. It patches an Il2Cpp Unity game with Harmony and builds against assemblies under `$(GamePath)/MelonLoader/Il2CppAssemblies` plus MelonLoader and Harmony assemblies under `$(GamePath)/MelonLoader/net6`.

The project disables default compile item discovery and explicitly lists every compiled source file in `MultiplayerTools/MultiplayerTools.csproj`.

## Build And Runtime

- Solution: `MultiplayerTools.sln`
- Project: `MultiplayerTools/MultiplayerTools.csproj`
- Target framework: `net6.0`
- Runtime/mod host: MelonLoader
- Patch system: Harmony
- UI runtime: Unity UI, TextMeshPro, Il2Cpp game UI components
- Post-build behavior: copies the output DLL into `$(GamePath)/Mods`
- Important local property: `$(GamePath)` must be defined for game assembly references.

No test project, formatter config, or CI workflow exists in this repository.

## Source Files

- `MultiplayerToolsCore.cs`
  - Melon mod entry point.
  - Registers preferences.
  - Applies Harmony patches.
  - Tracks local player/host state.
  - Handles join/leave side effects and message settings.

- `ChatSystem.cs`
  - Harmony patches for chat processing.
  - Implements bang commands, host checks, command help, MOTD, teleport commands, and `!settings`.
  - Calls `SettingsCommand.HandleCommand` for the host-only settings menu.

- `UI/SleddingUiAdapter.cs`
  - Entry point for Sledding-native UI adaptation.
  - Wraps existing native objects and coordinates explicit template capture.

- `UI/SleddingUiPaths.cs`
  - Centralizes known native Sledding UI object names and transform paths.

- `UI/UiElement.cs`
  - Chainable wrapper around Unity `GameObject`, `Transform`, and `RectTransform`.

- `UI/NativeUiBackend.cs`
  - Internal implementation for native object lookup, template capture, cloning, styling, layout, and activation helpers.
  - Kept behind focused helper classes so feature code does not call a catch-all UI library.

- `UI/NativeUiTemplateSet.cs`
  - Holds captured native Sledding UI templates.

- `UI/ScrollViewport.cs`
  - Data object returned when building a native-style scroll viewport.

- `UI/GridTrackRow.cs`
  - Data object for manual grid-row track layout.

- `UI/NativeUiTemplates.cs`
  - Owns native template capture, clearing, and access to the current template catalog.

- `UI/UiStyles.cs`
  - Facade for native text, button, toggle, image, shadow, and layout style copying.

- `UI/UiLayout.cs`
  - Facade for rect and layout helpers.

- `UI/NativeUiFactory.cs`
  - Facade for creating/cloning native labels, buttons, inputs, toggles, sliders, panels, backdrops, and scroll views.

- `UI/NativeUiBuilder.cs`
  - Facade for common row/grid composition helpers.

- `SettingsCommand.cs`
  - Harmony patch and command handler for a custom settings menu.
  - Delegates menu lifecycle to `Features/Settings/SettingsMenuController`.

- `Features/Settings/SettingsMenuController.cs`
  - Owns settings open/close lifecycle, chat closing, cursor state, Escape handling, staged draft apply/discard behavior, and required-template validation.

- `Features/Settings/SettingsMenuView.cs`
  - Builds the modal settings panel from native UI templates.
  - Uses `SettingsSchema` for simple one-label/one-control rows and explicit builders for compound password/join/leave rows.

- `Features/Settings/SettingsDraft.cs`
  - Stages settings edits before Apply commits the full draft through `MultiplayerToolsCore` setters.

- `Features/Settings/SettingsSchema.cs`
  - Describes simple settings fields used by the settings view.

- `LobbyPatchFeatures.cs`
  - Thin Harmony patch shell named `LobbyPatches`.
  - Delegates main menu enable and lobby creation patches to `Features/Lobby/LobbyUiController`.

- `Features/Lobby/LobbyUiController.cs`
  - Stateful controller for embedded create-lobby UI.
  - Owns cached mod-added/native lobby controls and native listener binding IDs.
  - Coordinates main menu adaptation, create-lobby adaptation, and lobby submission values.

- `Features/Lobby/MainMenuAdapter.cs`
  - Adjusts the native main menu layout around the embedded create-lobby UI.
  - Hides the vanilla host button, repositions join/quit controls, and scales/moves the menu layout.

- `Features/Lobby/LobbyMenuAdapter.cs`
  - Finds and activates the native `UI_CreateLobby` root.
  - Preserves the retry activation behavior needed because the vanilla menu disables panels after enable.
  - Hides native create-lobby close buttons for the embedded main menu experience.

- `Features/Lobby/LobbyCreateSubmission.cs`
  - Computes lobby name and max-player values for `LobbyManager.CreateLobby` and `EOSLobbyManager.CreateLobby`.
  - Falls back to saved preferences and player name when custom UI controls are unavailable.

- `Utils.cs`
  - General game/player helper functions.
  - Not directly part of the UI rewrite scope.

## Current Rewrite Scope

The requested overhaul is focused on:

- `UI/NativeUiBackend.cs`
- `SettingsCommand.cs`
- `LobbyPatchFeatures.cs`

The old catch-all UI subsystem has been split into native UI adapter helpers, feature-specific views/controllers, and thin Harmony integration patches.
