# LobbyKit

Host and lobby quality-of-life tools for **Sledding Game**.

## Host Requirement

LobbyKit is primarily a host/server-side mod. Most functionality (especially chat commands) is driven by the lobby host having LobbyKit installed. Clients generally do not need to install LobbyKit to join and use host-provided features.

## Features

- Host lobbies with up to 64 players.
- Add a custom lobby name and saved create-lobby defaults.
- Save public/private, password, peaceful mode, and text-chat-only lobby preferences.
- Improve lobby search with saved filters, inclusive max-player filtering, region preference, modded/locked lobby toggles, and current-player sorting.
- Show live session timers beside player names in the player list.
- Send join and leave messages with configurable size.
- Send an optional message of the day to players who join your hosted lobby.
- Grant trusted players Mod/Admin/Owner permissions that survive reconnects, with per-command level requirements.
- Moderate your lobby with kick, ban, and unban commands.
- Block common cheats: throwing spam, player-size manipulation, and flying sleds (each toggleable).
- Optionally auto-restart your hosted lobby.
- Use chat commands for settings, moderation, permissions, and teleport workflows.

## Chat Commands

Commands require a minimum permission level. The host is always Owner, and you can grant other players levels with `!op`. Default levels are shown below; each command's required level can be changed in `UserData/LobbyKit-permissions.json`.

| Command | Min level | Description |
| --- | --- | --- |
| `!!` | Everyone | Repeat your last command. |
| `!help [command\|page]` | Everyone | List commands (paged), or show details for one command. |
| `!motd [message]` | Everyone | Show the message of the day. Admins can pass text to set it. |
| `!tp [name]` | Everyone | Teleport to a player by name. |
| `!tpme [name]` | Everyone | Ask a player to teleport to you. |
| `!tpa` | Everyone | Accept a pending teleport request. |
| `!size [0.2-3.0]` | Everyone | Set your player size (`!size 1` resets to normal). |
| `!level [name]` | Everyone | Show your level, or another player's. |
| `!tpf [name\|@group]` | Mod | Force a player to teleport to you. Groups: `@all`, `@mods`, `@admins`, `@owners`. |
| `!kick [name] [reason]` | Mod | Kick a player. |
| `!ban [name\|puid] [reason]` | Admin | Ban a player (use `!unban` to reverse). |
| `!unban [name\|puid]` | Admin | Remove a ban by name or PUID. |
| `!settings [?\|key] [value]` | Owner | List settings with `!settings ?`, or change one, e.g. `!settings bc on`. |
| `!op [level] [name]` | Owner | Set a player's level (everyone/mod/admin/owner). Requires `!confirm`. |

### Changing or disabling commands per level

The required level for every command lives under `commandLevels` in `UserData/LobbyKit-permissions.json`. Levels are `Everyone = 0`, `Mod = 1`, `Admin = 2`, `Owner = 3`. Edit the file while the server is **stopped** (there is no hot-reload), then restart.

- **Lower the bar** so more players can use a command, e.g. let anyone tp:

  ```json
  "commandLevels": { "!tp": 0 }
  ```

- **Raise the bar** to restrict a command, e.g. make teleporting Mod-only:

  ```json
  "commandLevels": { "!tp": 1 }
  ```

- **Disable a command** entirely by setting a level above Owner (e.g. `4`):

  ```json
  "commandLevels": { "!size": 4 }
  ```

Every command is listed in the file on first run, so you can see and adjust each one. The host (the server itself) is always Owner and can use every enabled command.

## Installation

### Gale / r2modman

Install with Gale or r2modman. MelonLoader is declared as a dependency.

### Manual

1. Install MelonLoader for Sledding Game.
2. Launch the game once so MelonLoader generates its folders and IL2CPP assemblies.
3. Copy `LobbyKit.dll` into the game's `Mods/` folder.
4. Start the game through MelonLoader.

For hosted-lobby features, install LobbyKit on the host machine; clients typically do not need a local install.

## Configuration

Most options are available through `!settings` while hosting — open the in-game menu locally, or list and change settings from chat with `!settings ?` and `!settings <key> [value]`. The mod stores preferences through MelonLoader under the `LobbyKit` category.

Player permissions and bans are stored in `UserData/LobbyKit-permissions.json`, keyed on EOS PUID. Edit it while the server is stopped (no hot-reload), or manage levels and bans live with `!op`, `!ban`, and `!unban`.

## CI/CD (Thunderstore)

This repository includes GitHub Actions workflows for Thunderstore packaging and publishing:

- `.github/workflows/github-release.yml`
  - Manual workflow dispatch.
  - Creates a GitHub Release from a version in `CHANGELOG.md` (extracts that version's notes).
- `.github/workflows/thunderstore-build.yml`
  - Runs on push/PR/manual dispatch.
  - Builds `LobbyKit` and uploads a Thunderstore zip artifact.
- `.github/workflows/thunderstore-publish.yml`
  - Runs when a GitHub Release is published (or manually via workflow dispatch).
  - Builds and publishes with `tcli publish`.
  - All workflows accept `dryrun` on manual dispatch; `dryrun=true` echoes commands and skips execution-sensitive steps.

Required repository secret:

- `THUNDERSTORE_TOKEN`: Thunderstore service account token (used as `TCLI_AUTH_TOKEN`).
- `SGREFROOT_TOKEN`: GitHub token with read access to `ricky-davis/SGRefRoot` (used to fetch `Il2CppAssemblies` and `net6` refs).
- `RELEASE_WORKFLOW_TOKEN`: PAT used by `github-release.yml` to create GitHub Releases so `release`-triggered workflows run.

### Local Pre-Commit Hook

This repo includes a pre-commit hook that enforces version consistency across:

- `LobbyKit/Directory.Build.props` (`<Version>`)
- `thunderstore.toml` (`versionNumber`)
- `CHANGELOG.md` (latest `## [x.y.z]` heading)

Enable repo hooks once per clone:

```bash
git config core.hooksPath .githooks
```
