# Risks And Open Questions

## Risks

- `UILib` contains many hidden game-specific assumptions. Moving helpers without preserving `UI_CreateLobby` template discovery can break visual parity.
- Unity/Il2Cpp inactive object lookup is required. Replacing `Resources.FindObjectsOfTypeAll` with active-only searches would break template capture.
- Native UI activation is timing-sensitive. `LobbyPatchFeatures` currently retries for 10 frames because vanilla disables create-lobby panels after `OnEnable`.
- Listener duplication is possible on repeated main-menu enables. The rewrite needs explicit idempotency for all native control bindings.
- `ToggleGroup` behavior for public/private toggles depends on native internals (`publicLobbyToggle.group.m_Toggles[0]`). This is brittle and should be isolated before changing.
- `SettingsCommand` currently saves preferences immediately. The rewrite intentionally changes settings window behavior to staged edits with an Apply button, so validation must distinguish settings-window behavior from native create-lobby behavior.
- `SettingsCommand` uses Harmony `Traverse` to force chat state closed. This may be version-sensitive but should be preserved behind a named service.
- The project has no automated UI tests. Most validation must be in-game or by inspecting Unity object trees.
- Missing native templates should surface as clear warnings or skipped UI, not plain fallback controls.
- The manual grid row layout depends on actual rect widths after layout rebuilds. A cleaner layout abstraction must preserve its computed positions.

## Open Questions

- Should the rewrite preserve the static `UILib` API as a compatibility facade long term, or remove it after all call sites are migrated? Resolved: preserve it temporarily, then migrate toward `SleddingUiAdapter` as the canonical name.
- Should settings remain immediate-save for parity, or should the later staged apply behavior from `TODO.md` be included as a separate approved feature? Resolved: the settings window should use staged edits with an Apply button.
- Should settings field definitions live beside `MultiplayerToolsCore` preferences, or in the settings feature folder with delegates into the core? Resolved: settings field definitions live in the settings feature folder.
- Should template capture happen once per scene, once per menu open, or opportunistically as now? Resolved: capture explicitly on menu open or feature activation, updating the shared native template catalog/global defaults.
- Is `ui-dump-full.json` intended as the source of truth for native UI paths and object names? Resolved: no, it is a dev-only inspection reference.
- Which in-game scenes need to be checked for settings menu parent resolution: main menu, pause menu, active game, and lobby? Resolved: all four are supported states.
- Should the Sledding UI Adapter be internal-only, or designed as a reusable public helper for future mod features? Resolved: internal-only, reusable within this mod.

## Resolved Decisions

- `UILib` should become a Sledding Game UI adapter/toolkit, not a game-agnostic Unity UI library. Its purpose is to clone and modify existing Sledding Game UI features so mod UI keeps the native style.
- `UILib` should be treated as a temporary compatibility facade. The long-term canonical entry point should be `SleddingUiAdapter`, supported by explicit native UI helper types.
- Known native UI object names and transform paths should be centralized in `SleddingUiPaths` rather than scattered across adapters and factories.
- Native UI template capture should happen on menu open or feature activation. Factories should consume captured templates rather than performing normal scene searches themselves.
- Missing-template behavior should be strict about style. The adapter should not create plain fallback controls; it should clone native UI or log and skip affected native adaptations.
- Settings window should abort opening and log clearly if required native templates are missing.
- Required settings templates are panel/background, text/label, button, input field, toggle, slider, and scrollbar/scroll viewport behavior.
- Apply commits the full settings draft.
- Settings window edits should be staged and committed with `Apply` and `Apply & Close` buttons. `Apply` commits every setting shown in the settings window and keeps the window open; `Apply & Close` commits and closes.
- `X` and Escape should prompt when staged settings have unapplied changes. The confirmation should offer `Apply & Close`, `Discard & Close`, and `Cancel`.
- The unapplied-changes confirmation should be a custom modal built from cloned native Sledding UI templates.
- `SettingsMenuController` should own a `SettingsDraft`; the view binds to the draft and core preferences remain the committed source.
- Settings dirty detection should compare against the last clean draft baseline from open/apply, not continuously against live preferences.
- Native create-lobby controls should remain immediate-save and should not use `SettingsDraft`.
- ADR: staged settings window behavior is recorded in `docs/adr/0001-stage-settings-window-edits.md`.
- ADR: Sledding UI Adapter architecture is recorded in `docs/adr/0002-use-sledding-ui-adapter.md`.
- Settings Apply saves preferences/defaults only in this rewrite. Updating an active lobby is deferred.
- Settings field definitions should live in the settings feature folder. `MultiplayerToolsCore` remains the committed preference source, not the UI schema owner.
- Settings rendering should be hybrid: simple rows come from `SettingsSchema`, while compound rows remain explicit builder methods.
- `SettingsCommand` should keep its class name but move under `Features/Settings` and become thin chat/Harmony glue.
- `LobbyPatchFeatures` should become `LobbyPatches`, a thin Harmony patch shell with behavior moved into lobby feature services.
- `LobbyUiController` should own cached lobby UI controls and idempotency state. `LobbyPatches` delegates Harmony events to it.
- Listener policy: clear/replace listeners only on controls owned by MultiplayerTools; for native controls, keep vanilla listeners and track bound instance IDs to avoid duplicate mod listeners.
- `ui-dump-full.json` is a dev-only inspection reference. The committed source of native UI path names should be `SleddingUiPaths`.
- Settings parent resolution should support main menu, pause menu, active game, and lobby states.
- The Sledding UI Adapter should be an internal mod subsystem, reusable by future MultiplayerTools features but not designed as a public API for other mods.
