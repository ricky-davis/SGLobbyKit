# Migration Plan

## Goal

Rewrite the UI subsystem so it works like a focused Sledding Game UI adapter:

- clear primitives for cloning and modifying native UI controls.
- native template capture separated from control composition.
- declarative-ish view composition for settings using cloned Sledding Game UI.
- feature patch classes that only connect game events to view/controller services.
- preservation of current functionality and appearance.

This should be done incrementally. A single all-at-once replacement would be hard to validate in-game and would make visual regressions difficult to isolate.

## Proposed Target Structure

```text
MultiplayerTools/
├── UI/
│   ├── SleddingUiAdapter.cs
│   ├── SleddingUiPaths.cs
│   ├── UiElement.cs
│   ├── NativeUiBuilder.cs
│   ├── NativeUiFactory.cs
│   ├── UiLayout.cs
│   ├── UiStyles.cs
│   ├── NativeUiTemplates.cs
│   ├── UiClone.cs
│   ├── UiText.cs
│   ├── UiMenuHost.cs
│   └── Controls/
│       ├── SettingsForm.cs
│       ├── SettingField.cs
│       └── ScrollView.cs
├── Features/
│   ├── Settings/
│   │   ├── SettingsCommand.cs
│   │   ├── SettingsMenuController.cs
│   │   ├── SettingsMenuView.cs
│   │   ├── SettingsDraft.cs
│   │   └── SettingsSchema.cs
│   └── Lobby/
│       ├── LobbyPatches.cs
│       ├── LobbyUiController.cs
│       ├── LobbyMenuAdapter.cs
│       ├── LobbyCreateSubmission.cs
│       └── MainMenuAdapter.cs
```

Keep compatibility wrappers in `UILib` at first. Existing callers can move gradually while tests/in-game checks compare behavior.

The Sledding UI Adapter is an internal mod subsystem, not a public library for other mods. Its APIs should be clean enough for future MultiplayerTools features, but they do not need public versioning guarantees.

## New Sledding UI Adapter Model

### `SleddingUiAdapter`

The canonical long-term entry point for mod UI creation and native UI adaptation. `UILib` should remain as a temporary compatibility facade while call sites migrate.

### `SleddingUiPaths`

Centralizes known Sledding Game UI root names and transform paths such as `UI_CreateLobby`, `UI_BackgroundFade`, lobby panel background paths, password input paths, and main menu button names. Native adapters and template sources should consume these constants instead of embedding magic strings in feature code.

`ui-dump-full.json` may be used as a local inspection aid while deriving these constants, but it is not the committed source of truth.

### `UiElement`

Replacement for `UILib.Element`.

- Wraps `GameObject`.
- Provides `Rect`, `Transform`, `Show`, `Hide`, `Destroy`, `ParentTo`, `Move`, `Size`, `Anchors`, `Layout`.
- Should be a thin convenience wrapper only.

### `NativeUiTemplates`

Owns Sledding Game template discovery and caching.

- `CaptureFrom(Transform root, bool overwrite)`
- `CaptureScene(bool overwrite)`
- `Clear()`
- `Current` style/template object.

Move hard-coded game paths here, not in primitive creation. The factory should create from a captured native template catalog rather than search the scene itself.

Template capture should happen explicitly when a menu or feature adapter is activated, and should update the shared native template catalog/global defaults. `SettingsMenuController` captures before opening the settings menu, and `LobbyMenuAdapter` captures from the create-lobby UI when that native root is available. Factory methods should consume the shared catalog instead of deciding when to scan the scene.

Missing-template behavior should be strict about style. The Sledding UI Adapter should clone native UI and should not create plain fallback controls. If an expected native object or template is missing, skip the affected native adaptation and log a clear warning.

For the settings window, missing required native templates should abort opening the settings window and log a clear warning. Do not create plain fallback controls.

The settings window requires native templates for panel/background, text/label, button, input field, toggle, slider, and scrollbar/scroll viewport behavior.

### `UiStyles`

Owns style copying and fallback colors.

- `ApplyText`
- `ApplyButton`
- `ApplyToggle`
- `CopyImage`
- `CopyShadow`
- `CopyLayout`
- `SetTextMetrics`
- `SetInputTextStyle`

No scene searches or object creation except adding required components to the target.

### `NativeUiFactory`

Creates and clones UI primitives from captured Sledding Game templates.

- `Label`
- `PlainLabel`
- `Button`
- `Input`
- `Toggle`
- `Slider`
- `Scrollbar`
- `Panel`
- `Backdrop`
- `ScrollView`

Factory methods should take an options object when parameters exceed a small set. Example:

```csharp
factory.Input(parent, new InputOptions
{
    Name = "ServerNameInput",
    Text = model.ServerName,
    Placeholder = "",
    OnChanged = model.SetServerName
});
```

This is more conventional than long optional-parameter signatures and makes call sites readable, while still keeping the output visually native to Sledding Game.

### `UiLayout`

Owns layout group and rect utilities.

- `Stretch`
- `SetRect`
- `Vertical`
- `Horizontal`
- `FixedSize`
- `FlexibleSize`
- `GridRow`

Replace the manual `float[]` grid row API with a small `GridRowBuilder` or `TrackSpec[]`, so future rows can express fixed/flexible tracks clearly. Preserve the current calculated positions for parity.

### `NativeUiBuilder`

Small composition layer for forms built from native Sledding Game controls.

- `Panel`
- `Header`
- `PreferenceRow`
- `Divider`
- `ToggleRow`
- `InputRow`
- `SliderRow`
- `ScrollPanel`

This should not know about `MultiplayerToolsCore`; it builds UI from provided labels, values, and callbacks.

## Settings Rewrite

### Step 1: Extract Settings Schema

Create `SettingsSchema` in the settings feature folder. It lists simple fields and their bindings:

- field id.
- label.
- control type.
- current value getter.
- setter.
- min/max for sliders.
- formatting and sizing metadata.

Use a hybrid rendering model. Simple one-label/one-control rows are generated from `SettingsSchema`. Compound rows with special layout or multiple controls, such as password protection plus password input and join/leave message toggle plus size slider, remain explicit view-builder methods.

The settings window should use staged settings with `Apply` and `Apply & Close` buttons. Opening the window reads current values into a settings draft. Editing controls mutates the draft. Pressing `Apply` commits the full settings draft through existing `MultiplayerToolsCore` setters and keeps the window open. Pressing `Apply & Close` commits the full settings draft and closes the window.

Apply actions save preferences/defaults only for this rewrite. Updating an already-running lobby is explicitly deferred to a future feature.

Closing through `X` or Escape should close immediately only when there are no unapplied changes. If staged values differ from the last applied values, show a confirmation dialog with `Apply & Close`, `Discard & Close`, and `Cancel` choices.

The confirmation dialog should be a small modal built from cloned native Sledding UI templates through the Sledding UI Adapter.

`SettingsMenuController` owns a `SettingsDraft`. The view binds controls to the draft, and `MultiplayerToolsCore` remains the committed preference source.

Dirty detection compares the current draft to the last clean baseline. The baseline is created when the settings window opens and refreshed after a successful `Apply`. The window does not continuously compare against externally changed live preferences while open.

`MultiplayerToolsCore` remains responsible for committed preference values and setters only. Settings field order, labels, groups, control types, min/max values, and draft bindings live in the settings feature folder.

Native create-lobby controls do not use `SettingsDraft`; they continue to write committed preferences immediately as part of the built-in lobby creation flow.

### Step 2: Extract Settings Menu View

Move UI construction out of the Harmony class into `SettingsMenuView`.

`SettingsMenuView` should:

- accept a parent transform, UI factory/builder, settings schema, and close callback.
- create root/backdrop/panel/scroll content.
- include `Apply` and `Apply & Close` buttons that commit the current settings draft.
- include an unapplied-changes confirmation modal for close attempts.
- return a small handle with `Root`, `FirstSelectable`, and any controls needed for lifecycle.

### Step 3: Extract Settings Menu Controller

Move lifecycle into `SettingsMenuController`.

Responsibilities:

- open.
- close.
- find native UI parent across main menu, pause menu, active game, and lobby states.
- close chat.
- integrate with `UiReferenceController`.
- restore cursor state.
- handle Escape.
- own the settings draft lifecycle and apply/close behavior.

`SettingsCommand` becomes mostly:

- `HandleCommand(...) => SettingsMenuController.Instance.Open()`
- Harmony Escape postfix delegates to the controller.

Keep the `SettingsCommand` class name for integration stability, but move it under `Features/Settings` and make it thin chat/Harmony glue.

## Lobby Rewrite

### Step 1: Introduce Lobby UI Controller

Create a stateful `LobbyUiController` that owns cached native controls and idempotency state currently held in `LobbyPatchFeatures`, including the max-player slider, custom lobby name input, guest bang commands toggle, and toggle binding state.

`LobbyPatches` delegates Harmony events to `LobbyUiController.Instance`.

### Step 2: Extract Main Menu Adapter

Move `ConfigureMainMenu` into `MainMenuAdapter`.

Responsibilities:

- hide host button.
- move/show join button.
- style quit button.
- scale/move menu layout.
- return the menu panel root for create-lobby embedding.

### Step 3: Extract Lobby Menu Adapter

Move UI mutation methods into `LobbyMenuAdapter`.

Responsibilities:

- find/activate create-lobby root.
- ensure custom lobby name input.
- ensure max-player slider.
- hide close buttons.
- bind existing lobby options.
- add guest bang commands toggle.

Keep listener idempotency as a first-class concern. The current `_togglesInitialized` and cached toggle checks are fragile but important.

Listener ownership policy:

- For controls created or cloned by MultiplayerTools, clear and replace listeners as needed.
- For native Sledding Game controls, do not remove vanilla listeners. Track bound control instance IDs and only attach MultiplayerTools listeners once per native object instance.

### Step 4: Extract Lobby Submission Service

Move fallback and prefix submission logic into `LobbyCreateSubmission`.

Responsibilities:

- resolve lobby name from input/preference/player.
- resolve max players from slider/preference.
- apply values to `LobbyManager.CreateLobby` arguments.
- apply values to EOS lobby properties.

`LobbyPatchFeatures` should be replaced by `LobbyPatches`, following the same pattern as `SettingsCommand`: keep the patch/integration shell thin and move behavior into feature services.

Patch methods should become thin delegates.

## Recommended Implementation Phases

1. Add new `UI/` files and move clone/style/layout helpers from `UILib` while preserving `UILib` wrappers.
2. Move native UI object names and transform paths into `SleddingUiPaths`.
3. Move native template capture into `NativeUiTemplates`; leave `UILib.Capture*` as forwarding methods during migration, but make callers capture explicitly before building UI.
4. Move style/copy/layout methods into `UiStyles` and `UiLayout`; leave forwarding wrappers.
5. Introduce options types for new factory methods, then port `SettingsCommand` to them.
6. Extract `SettingsMenuView` and `SettingsMenuController`; reduce `SettingsCommand` to patch/command glue.
7. Extract `MainMenuAdapter`, `LobbyMenuAdapter`, and `LobbyCreateSubmission`; reduce `LobbyPatchFeatures` to Harmony glue.
8. Delete or deprecate unused `UILib` methods after all call sites are moved.
9. Perform in-game parity checks against `parity-checklist.md`.

## Naming Guidance

Prefer names that reflect standard UI concepts:

- `SleddingUiAdapter` as the canonical entry point instead of catch-all `UILib`.
- `SleddingUiPaths` for native object names and transform paths.
- `NativeUiFactory` for native-control clone/create methods.
- `NativeUiTemplates` for captured native references.
- `UiStyles` for copying colors/fonts/images/shadows.
- `UiLayout` for rect and layout group operations.
- `SettingsMenuView` for visual tree construction.
- `SettingsMenuController` for open/close lifecycle.
- `LobbyMenuAdapter` for mutating native create-lobby UI.

Avoid putting feature-specific object names or game paths in factory methods; keep them in native template sources/adapters.
