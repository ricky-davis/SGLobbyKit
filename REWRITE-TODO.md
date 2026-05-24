# Rewrite TODO

## Current State

- [x] Document rewrite analysis in `docs/rewrite-analysis/`.
- [x] Record ADR for staged settings window edits.
- [x] Record ADR for Sledding UI Adapter architecture.
- [x] Add `SleddingUiPaths`.
- [x] Add `SleddingUiAdapter` compatibility entry point over `UILib`.
- [x] Add `SettingsDraft`.
- [x] Add initial `SettingsSchema`.
- [x] Add `LobbyUiController` shell with native binding tracking.
- [x] Extract `SettingsMenuController`.
- [x] Extract `SettingsMenuView`.
- [x] Reduce `SettingsCommand` to command/Harmony glue.

## Verify Soon

- [ ] Smoke-test `!settings` in-game.
- [ ] Verify `Apply` keeps the settings window open and saves preferences.
- [ ] Verify `Apply & Close` saves preferences and closes.
- [ ] Verify `X`/Escape prompts only when there are unapplied changes.
- [ ] Verify confirmation actions: `Apply & Close`, `Discard & Close`, `Cancel`.
- [ ] Verify settings parent resolution from main menu, pause menu, active game, and lobby.

## Lobby Rewrite

- [x] Replace `LobbyPatchFeatures` with thin `LobbyPatches` Harmony shell.
- [x] Route lobby patch methods through `LobbyUiController`.
- [x] Extract `MainMenuAdapter`.
- [x] Extract `LobbyMenuAdapter`.
- [x] Extract `LobbyCreateSubmission`.
- [x] Move cached lobby UI state into `LobbyUiController`.
- [x] Track native control listener bindings by instance ID.
- [x] Clear listeners only on controls owned/cloned by MultiplayerTools.
- [x] Keep native create-lobby controls immediate-save.
- [x] Preserve embedded create-lobby activation retry behavior.

## Settings Rewrite

- [x] Use `SettingsSchema` for simple one-label/one-control rows.
- [x] Keep password and join/leave message rows as explicit compound builders.
- [x] Add required-template validation before opening settings.
- [x] Remove plain fallback settings controls.
- [x] Ensure Apply commits the full `SettingsDraft`.
- [x] Keep active-lobby update out of scope for this rewrite.

## Sledding UI Adapter

- [x] Split `UILib.Element` into `UiElement`.
- [x] Extract template capture into `NativeUiTemplates`.
- [x] Extract style copying into `UiStyles`.
- [x] Extract rect/layout helpers into `UiLayout`.
- [x] Extract clone/create helpers into `NativeUiFactory`.
- [x] Extract common form composition into `NativeUiBuilder`.
- [x] Keep `UILib` as a temporary compatibility facade.
- [ ] Migrate call sites from `UILib` to `SleddingUiAdapter`/native UI helpers.
- [ ] Remove or obsolete unused `UILib` methods after migration.

## Documentation

- [x] Update `docs/rewrite-analysis/functionality-inventory.md` after each behavior-changing slice.
- [ ] Update `docs/rewrite-analysis/parity-checklist.md` as parity checks are completed.
- [ ] Add ADR only for hard-to-reverse, surprising trade-off decisions.
