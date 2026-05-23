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

- [ ] Build with a local .NET SDK/MSBuild environment.
- [ ] Smoke-test `!settings` in-game.
- [ ] Verify `Apply` keeps the settings window open and saves preferences.
- [ ] Verify `Apply & Close` saves preferences and closes.
- [ ] Verify `X`/Escape prompts only when there are unapplied changes.
- [ ] Verify confirmation actions: `Apply & Close`, `Discard & Close`, `Cancel`.
- [ ] Verify settings parent resolution from main menu, pause menu, active game, and lobby.

## Lobby Rewrite

- [ ] Replace `LobbyPatchFeatures` with thin `LobbyPatches` Harmony shell.
- [ ] Route lobby patch methods through `LobbyUiController`.
- [ ] Extract `MainMenuAdapter`.
- [ ] Extract `LobbyMenuAdapter`.
- [ ] Extract `LobbyCreateSubmission`.
- [ ] Move cached lobby UI state into `LobbyUiController`.
- [ ] Track native control listener bindings by instance ID.
- [ ] Clear listeners only on controls owned/cloned by MultiplayerTools.
- [ ] Keep native create-lobby controls immediate-save.
- [ ] Preserve embedded create-lobby activation retry behavior.

## Settings Rewrite

- [ ] Use `SettingsSchema` for simple one-label/one-control rows.
- [ ] Keep password and join/leave message rows as explicit compound builders.
- [ ] Add required-template validation before opening settings.
- [ ] Remove plain fallback settings controls.
- [ ] Ensure Apply commits the full `SettingsDraft`.
- [ ] Keep active-lobby update out of scope for this rewrite.

## Sledding UI Adapter

- [ ] Split `UILib.Element` into `UiElement`.
- [ ] Extract template capture into `NativeUiTemplates`.
- [ ] Extract style copying into `UiStyles`.
- [ ] Extract rect/layout helpers into `UiLayout`.
- [ ] Extract clone/create helpers into `NativeUiFactory`.
- [ ] Extract common form composition into `NativeUiBuilder`.
- [ ] Keep `UILib` as a temporary compatibility facade.
- [ ] Migrate call sites from `UILib` to `SleddingUiAdapter`/native UI helpers.
- [ ] Remove or obsolete unused `UILib` methods after migration.

## Documentation

- [ ] Update `docs/rewrite-analysis/functionality-inventory.md` after each behavior-changing slice.
- [ ] Update `docs/rewrite-analysis/parity-checklist.md` as parity checks are completed.
- [ ] Add ADR only for hard-to-reverse, surprising trade-off decisions.

