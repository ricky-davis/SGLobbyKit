# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- `ChatSystem.cs` split out as dedicated chat command system file.
- `Utils.cs` for consolidated player-finding helpers.

### Changed
- Reorganized patch/features structure in `MaxPlayersPatch.cs`.
- Moved chat broadcast and fake server player-reference logic into `ChatSystem`.
- Updated project compile includes to match new file layout.

### Fixed
- Corrected source include name mismatch (`ChatCommandSystem.cs` -> `ChatSystem.cs`).
