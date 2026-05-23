# Repository Map

## Project

MultiplayerTools is a C# `net6.0` MelonLoader mod for Sledding Game. It patches an Il2Cpp Unity game with Harmony and builds against assemblies under `$(GamePath)/MelonLoader/Il2CppAssemblies` plus MelonLoader and Harmony assemblies under `$(GamePath)/MelonLoader/net6`.

The project disables default compile item discovery and explicitly compiles six source files from `MultiplayerTools/`.

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

- `UILib.cs`
  - Large static Unity UI helper library.
  - Captures templates from existing game UI.
  - Clones and creates buttons, labels, inputs, toggles, sliders, panels, scroll views, rows, and layout helpers.
  - Also contains game-specific discovery paths for `UI_CreateLobby`, main menu buttons, and lobby panel backgrounds.

- `SettingsCommand.cs`
  - Harmony patch and command handler for a custom settings menu.
  - Builds a modal settings panel with current `UILib` helpers.
  - Directly binds controls to `MultiplayerToolsCore` preference setters.
  - Owns menu open/close lifecycle, chat closing, cursor state, and Escape handling.

- `LobbyPatchFeatures.cs`
  - Harmony patches for the main menu and lobby creation.
  - Embeds `UI_CreateLobby` into the main menu.
  - Replaces the lobby name label with an input.
  - Extends max players to 64.
  - Adds/binds lobby option controls.
  - Hides close buttons and adjusts main menu layout.

- `Utils.cs`
  - General game/player helper functions.
  - Not directly part of the UI rewrite scope.

## Current Rewrite Scope

The requested overhaul is focused on:

- `UILib.cs`
- `SettingsCommand.cs`
- `LobbyPatchFeatures.cs`

These files currently form one broad UI subsystem but do not have clear ownership boundaries. The rewrite should split them into a conventional UI toolkit layer, game template/style layer, feature-specific views, and Harmony integration patches.

