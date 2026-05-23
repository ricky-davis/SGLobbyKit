# 0002 Use A Sledding UI Adapter

## Status

Accepted

## Context

MultiplayerTools needs mod-owned UI such as the settings window, and it also modifies built-in Sledding Game UI such as the create-lobby panel.

The game's UI has a distinct native style. A generic Unity UI helper library could create functional controls, but it would not reliably preserve the game's built-in visual language.

The current `UILib` already depends on native Sledding Game UI objects, including create-lobby controls, panel backgrounds, text styles, button styles, toggle styles, and sliders.

## Decision

The rewrite will treat the UI subsystem as a Sledding UI Adapter, not a game-agnostic Unity UI library.

The adapter will discover, capture, clone, style, and modify native Sledding Game UI objects. `UILib` will become a temporary compatibility facade while call sites migrate toward the canonical `SleddingUiAdapter` and supporting native UI helper types.

Known native UI object names and transform paths will be centralized in `SleddingUiPaths`.

The adapter is an internal MultiplayerTools subsystem. It should be reusable by future features in this mod, but it is not designed as a public API for other mods.

## Consequences

Mod UI should continue to look like native Sledding Game UI by default.

The UI subsystem will intentionally contain Sledding Game-specific concepts and paths, isolated behind adapter and path-catalog types.

The code should avoid generic UI abstractions that make native template cloning harder to reason about.

If Sledding Game changes its UI hierarchy, `SleddingUiPaths` and native template capture logic become the primary update points.

