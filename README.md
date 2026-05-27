# LobbyKit

Host and lobby quality-of-life tools for **Sledding Game**.

The internal project is named `LobbyKit`, matching the mod package name.

## Features

- Host lobbies with up to 64 players.
- Add a custom lobby name and saved create-lobby defaults.
- Save public/private, password, peaceful mode, and text-chat-only lobby preferences.
- Improve lobby search with saved filters, inclusive max-player filtering, region preference, modded/locked lobby toggles, and current-player sorting.
- Show live session timers beside player names in the player list.
- Send join and leave messages with configurable size.
- Send an optional message of the day to players who join your hosted lobby.
- Use chat commands for settings and teleport workflows.

## Chat Commands

| Command | Host only | Description |
| --- | --- | --- |
| `!settings` | Yes | Opens the in-game LobbyKit settings menu. |
| `!motd [message]` | Set: yes | Shows the message of the day. Hosts can pass a message to update it. |
| `!bc <on\|off>` | Yes | Enables or disables guest bang commands. |
| `!tp <name>` | No | Teleport to a player by name. |
| `!tpme <name>` | No | Ask another player to teleport to you. |
| `!tpa` | No | Accept a pending teleport request. |
| `!tpf <name>` | Yes | Force a player to teleport to you. |

## Installation

### Gale

Install with Gale. MelonLoader is declared as a dependency.

### Manual

1. Install MelonLoader for Sledding Game.
2. Launch the game once so MelonLoader generates its folders and IL2CPP assemblies.
3. Copy `LobbyKit.dll` into the game's `Mods/` folder.
4. Start the game through MelonLoader.

## Configuration

Most options are available in-game through `!settings` while hosting. The mod also stores preferences through MelonLoader under the `LobbyKit` category.

