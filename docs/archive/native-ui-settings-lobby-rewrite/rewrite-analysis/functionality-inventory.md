# Functionality Inventory

## Native UI Adapter

Preserve these public capabilities:

- Wrap existing objects/components/transforms with a `UiElement` handle.
- Create bare UI objects with `RectTransform`.
- Find named scene objects, including inactive objects via `Resources.FindObjectsOfTypeAll`.
- Capture default references from arbitrary roots and from the active scene.
- Clone native templates for text, inputs, toggles, sliders, panels, backdrops, and scrollbars.
- Create panels, backgrounds, screen backdrops, scroll viewports, horizontal rows, flex rows, and grid-track rows.
- Apply text, toggle, image, shadow, and layout styles.
- Remove localization components before setting replacement text.
- Normalize cloned UI scales.
- Stabilize cloned controls by disabling content fitters, select animations, and `AnimationSelectableUi` behavior.
- Set canvas group visibility/interactivity.
- Activate paths and children in native UI trees.

## Settings Command/Menu

Preserve these behaviors:

- Host command `!settings` opens the MultiplayerTools settings UI.
- Opening settings closes any existing settings root.
- Opening settings captures scene defaults.
- Opening settings aborts with a clear warning if required native templates are missing.
- Opening settings closes chat and closes other game menus.
- Settings root is parented under the native UI parent when possible.
- Settings modal includes a screen backdrop and centered panel.
- Panel size is `900 x 640`.
- Layout padding is left/right `43`, top/bottom `26`.
- Scroll viewport exists with sensitivity `35`.
- Header text is `MultiplayerTools Settings`.
- Close button is labelled `X`.
- Escape closes the menu.
- Closing destroys the root, clears the first selectable, closes chat, and restores locked/hidden cursor.
- Menu is opened through `UiReferenceController.OpenMenu` when available.
- Cursor is unlocked/visible if no `UiReferenceController` is available.
- Settings controls are cloned from native templates; the settings window does not create plain fallback controls.

Preserve displayed settings:

- Server Name input
- Server Capacity slider using the native slider template
- Public Lobby toggle
- Password Protected toggle plus password input placeholder `enter password...`
- Peaceful Mode toggle
- Text Chat Only toggle
- Divider
- `Mod Settings` section header
- Enable Guest `!Bang` Commands toggle
- Message of the Day input
- Join Messages toggle plus Size slider
- Leave Messages toggle plus Size slider

Change settings window binding semantics:

- Values are read from `MultiplayerToolsCore`.
- UI changes edit staged settings in the window.
- Pressing `Apply` commits the full settings draft through `MultiplayerToolsCore` setters and keeps the window open.
- Pressing `Apply & Close` commits the full settings draft through `MultiplayerToolsCore` setters and closes the window.
- `Apply` and `Apply & Close` save preferences/defaults only; they do not attempt to update an already-running lobby in this rewrite.
- Pressing `X` or Escape with no unapplied changes closes the window.
- Pressing `X` or Escape with unapplied changes opens a confirmation dialog with `Apply & Close`, `Discard & Close`, and `Cancel` choices.
- Native create-lobby controls remain immediate-save and do not use `SettingsDraft`.
- Server capacity is clamped to 1-64.
- Join/leave message size sliders are clamped to 50-100.

## Lobby Patch Features

Preserve these behaviors:

- Custom lobby name input is cloned from the native password input and inserted at the native lobby-name label sibling index.
- Native lobby-name text is hidden while the input is shown.
- Lobby name input character limit is 64 and single-line standard text.
- Lobby name placeholder is `Lobby name...`.
- Lobby name default is saved server name, else `<PlayerName>'s Lobby`, else `Sledding Lobby`.
- Lobby name changes update hidden native label text and saved server name.
- Max players slider max value is changed to 64.
- Max players slider initializes from saved server capacity.
- Slider changes update saved server capacity.
- Create lobby prefixes use custom lobby input name if present, else saved server name, else player-name fallback.
- `LobbyManager.CreateLobby` max players is clamped 1-64 and saved.
- `EOSLobbyManager.CreateLobby` lobby properties `MaxNumLobbyMembers` is clamped 1-64.
- Public/private, password protected, password, peaceful mode, and text-chat-only native controls initialize from preferences and update preferences.
- Guest bang commands toggle is cloned after text-chat-only toggle and bound to `EnableGuestBangCommands`.
- Create-lobby close buttons are hidden and made non-interactable.
- Main menu host button is hidden.
- Join button moves to sibling index 0 and expands.
- Quit button gets red image/shadow colors.
- Main menu layout is scaled to `0.78` and moved right by `295`.
- Embedded create-lobby root is reparented, moved left by `295`, scaled to `0.9`, and brought to last sibling.
- Embedded create-lobby activation runs for 10 frames because the vanilla UI disables it shortly after `OnEnable`.
