# Changelog

All notable changes to this project will be documented in this file.

## [1.1.2] - 2026-06-13

### Fixed

- Fuzzy player-name matching now prefers names that actually contain what you typed (as a whole word, prefix, or substring) over names that are merely spelled similarly. Affects every command that takes a player name (`!tp`, `!tpf`, `!kick`, `!ban`, etc.).

## [1.1.1] - 2026-06-12

### Added

- `!tpf` group targets: `@all` (alias `@everyone`), `@mods`, `@admins`, and `@owners` force every matching connected player to teleport to you. The level groups match that permission level exactly, and the host's own player is always skipped.

### Fixed

- Removed unecessary CrashDetect log message that bloated log files.

## [1.1.0] - 2026-06-08

### Added

- Permission system backed by `UserData/LobbyKit-permissions.json`, keyed on EOS PUID so levels and bans survive reconnects. Levels: Everyone, Mod, Admin, Owner (the host is always Owner).
- `!op [level] [name]` to set a player's level and `!level [name]` to view one (Owner; `!op` requires `!confirm`).
- Per-command required levels, editable in the permissions file (set a level above Owner to disable a command).
- Moderation commands: `!kick [name] [reason]` (Mod), `!ban [name|puid] [reason]` and `!unban [name|puid]` (Admin). Names resolve exact-first then fuzzy, with a PUID guard; bans accept an offline PUID.
- `!size [0.2-3.0]` to set your own player size (`!size 1` resets).
- Anticheat: throwing-spam rate limiting, player-size cheat packet clamping (also enforced on join), and flying-sled blocking, each individually toggleable.
- Chat-driven settings: `!settings ?` lists all settings, `!settings <key> [value]` views or changes one.
- Configurable chat level prefixes for Mod, Admin, and Owner.
- Paged `!help`, with `!help <page>` and `!help <command>` for details.
- Auto-restart support for the hosted lobby.
- Crash protection and silent-crash detection, plus despawning of hacked trinkets.

### Changed

- Settings are now adjustable from chat; standalone `!bc` was removed in favour of `!settings bc <on|off>`.
- Settings menu gained Anticheat and Chat Prefixes sections.
- Commands and features split into per-area files under `Features/`.

### Fixed

- Cannons intermittently launching no one after a player rolled a snowball.
- Player-name resolution for commands.
- Player size not applying when a player joined.
- Message of the day not showing for the host's lobby.
- Pressing Escape with unapplied settings changes now prompts to confirm instead of silently discarding the menu state.

## [1.0.0] - 2026-05-28

### Added

- Extended host lobby capacity support up to 64 players.
- Custom lobby name input and saved create-lobby defaults.
- Saved lobby options for public/private, password protection, peaceful mode, and text-chat-only mode.
- Saved lobby search preferences, including max players, locked lobbies, modded lobbies, language filtering, peaceful-mode filtering, and region mode.
- Inclusive max-player lobby search filtering.
- Lobby search sorting by current player count.
- Player list session timers.
- Host settings menu opened with `!settings`.
- Message of the day support with `!motd`.
- Join and leave announcement messages with configurable size.
- Guest bang command toggle with `!bc`.
- Teleport chat commands: `!tp`, `!tpme`, `!tpa`, and host-only `!tpf`.
