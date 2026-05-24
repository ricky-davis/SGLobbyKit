# MultiplayerTools Rewrite Handoff

## Purpose

Continue the Sledding Game MultiplayerTools UI rewrite. The user asked to start overhauling `UILib`, `SettingsCommand`, and `LobbyPatchFeatures` according to the documented rewrite plan.

## Repository

Workspace:

`/home/rdavis/CodingProjects/Modding/SleddingGame/MultiplayerTools`

Project:

`MultiplayerTools/MultiplayerTools.csproj`

Important constraint:

The project uses explicit `<Compile Include="...">` items. Any new `.cs` file must be added to the project file.

## Key Existing Artifacts

Do not duplicate these; read them directly:

- Rewrite TODO: `REWRITE-TODO.md`
- Repo/system/functionality docs: `docs/rewrite-analysis/`
- ADR for staged settings: `docs/adr/0001-stage-settings-window-edits.md`
- ADR for Sledding UI Adapter: `docs/adr/0002-use-sledding-ui-adapter.md`
- Glossary/context: `CONTEXT.md`

## Current Observable Worktree State

At handoff creation, `git status --short --untracked-files=all` showed:

```text
 M TODO.md
?? ui-dump-full.json
```

`git diff --stat` showed only `TODO.md` changed.

This means the code rewrite changes discussed earlier may not be present in the current workspace snapshot. Before continuing, verify whether these files exist:

- `MultiplayerTools/UI/SleddingUiPaths.cs`
- `MultiplayerTools/UI/SleddingUiAdapter.cs`
- `MultiplayerTools/Features/Settings/SettingsDraft.cs`
- `MultiplayerTools/Features/Settings/SettingsSchema.cs`
- `MultiplayerTools/Features/Settings/SettingsMenuController.cs`
- `MultiplayerTools/Features/Settings/SettingsMenuView.cs`
- `MultiplayerTools/Features/Settings/SettingsMenuHandle.cs`
- `MultiplayerTools/Features/Lobby/LobbyUiController.cs`

If they are absent, recreate them from the plan rather than assuming the extraction already happened.

## Decisions Already Made

The UI layer is a Sledding Game UI adapter, not a generic Unity UI toolkit. It should clone and modify native Sledding Game UI controls to preserve the built-in style.

`UILib` should become a temporary compatibility facade. The long-term canonical entry point is `SleddingUiAdapter`.

Native object names and transform paths should be centralized in `SleddingUiPaths`.

Settings window behavior:

- Uses `SettingsDraft`.
- Controls stage changes.
- `Apply` saves the full draft and keeps the window open.
- `Apply & Close` saves the full draft and closes.
- `X`/Escape prompt if the draft is dirty.
- Dirty confirmation offers `Apply & Close`, `Discard & Close`, and `Cancel`.
- Apply only saves preferences/defaults for now; updating an active lobby is deferred.

Native create-lobby controls remain immediate-save and do not use `SettingsDraft`.

Missing settings templates:

- No plain fallback UI controls.
- Required native templates missing should abort opening settings and log clearly.

Lobby rewrite direction:

- Replace `LobbyPatchFeatures` with thin `LobbyPatches`.
- Route behavior through stateful `LobbyUiController`.
- Extract `MainMenuAdapter`, `LobbyMenuAdapter`, and `LobbyCreateSubmission`.
- Native control listeners should be tracked by instance ID; do not clear vanilla listeners on native controls.

## Verification Notes

Earlier attempts to build could not run because `dotnet` was not available in the shell. The next agent should run a build if the local environment has .NET/MSBuild:

```bash
dotnet build MultiplayerTools/MultiplayerTools.csproj --no-restore
```

Expect possible Windows `GamePath` reference issues from this Linux shell. `MultiplayerTools/Directory.Build.props` points to a Windows Steam install path.

## Suggested Next Steps

1. Inspect the current filesystem and verify whether the rewrite files listed above exist.
2. If missing, start with the low-risk foundation:
   - Add `SleddingUiPaths`.
   - Add `SleddingUiAdapter`.
   - Add `SettingsDraft`.
   - Add `SettingsSchema`.
   - Add `LobbyUiController` shell.
   - Update `.csproj` compile includes.
3. Extract settings lifecycle:
   - `SettingsMenuController`.
   - `SettingsMenuView`.
   - `SettingsMenuHandle`.
   - Reduce `SettingsCommand` to command/Harmony glue.
4. Implement staged settings behavior if not already present.
5. Move to lobby split:
   - `LobbyPatches`.
   - `MainMenuAdapter`.
   - `LobbyMenuAdapter`.
   - `LobbyCreateSubmission`.

Use `REWRITE-TODO.md` as the checklist source of truth.

## Suggested Skills

- `rewrite-planner`: Use if revisiting architecture, parity, or migration sequencing.
- `grill-with-docs`: Use only for new high-level decisions that are not already settled in ADRs/docs.
- `handoff`: Use before ending another long rewrite session.

## Cautions

- Do not overwrite user changes in `TODO.md`.
- Treat `ui-dump-full.json` as dev-only inspection data, not committed source of truth.
- Keep changes incremental and reviewable.
- Since there are no tests, preserve behavior carefully and update `docs/rewrite-analysis/parity-checklist.md` when in-game checks are completed.
- Do not try to build locally, we don't have dotnet. The user has to build on a different system and test externally.
