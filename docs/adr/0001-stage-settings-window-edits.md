# 0001 Stage Settings Window Edits

## Status

Accepted

## Context

The current settings window writes preferences immediately when controls change. The native create-lobby controls also write preferences immediately as part of the built-in lobby creation flow.

The rewrite separates the mod-owned settings window from native create-lobby adaptation. The settings window is an explicit editor for several settings at once, while the create-lobby controls are part of the game's existing lobby flow.

## Decision

The settings window will edit a `SettingsDraft` instead of writing preferences immediately.

Pressing `Apply` commits the full settings draft and keeps the window open. Pressing `Apply & Close` commits the full settings draft and closes it.

After a successful `Apply`, the draft becomes the new clean baseline. Dirty detection compares against the last clean draft baseline, not continuously against live preferences while the window is open.

Closing with `X` or Escape closes immediately only when there are no unapplied changes. If there are unapplied changes, the settings window shows a confirmation dialog with `Apply & Close`, `Discard & Close`, and `Cancel` choices.

`Apply` and `Apply & Close` save preferences/defaults only. Updating an already-running lobby is deferred to a future feature.

Native create-lobby controls remain immediate-save and do not use `SettingsDraft`.

## Consequences

Settings window users can review multiple changes before committing them.

The settings feature needs explicit draft state, dirty checking, apply behavior, and close confirmation behavior.

The code must preserve two different setting surfaces: staged settings in the mod settings window and immediate-save controls in the native create-lobby UI.
