# Parity Checklist

Use this checklist during the rewrite. Any unchecked item is a possible regression.

## Build

- [ ] Project builds with the existing `MultiplayerTools.csproj`.
- [ ] No new external packages are required unless explicitly approved.
- [ ] `PostBuild` copy behavior remains unchanged.

## Settings Menu Visual Parity

- [ ] `!settings` opens a modal over the native UI.
- [ ] Settings window can resolve a native parent from main menu, pause menu, active game, and lobby states.
- [ ] Backdrop covers the screen and blocks raycasts.
- [ ] Panel remains centered and `900 x 640`.
- [ ] Header, close button, labels, inputs, toggles, sliders, divider, and scroll viewport match current appearance.
- [ ] Existing game templates are used when available.
- [ ] Missing native templates are logged clearly instead of silently creating plain fallback controls.
- [ ] Settings window aborts opening if required native templates are missing.
- [ ] Settings window requires panel/background, text/label, button, input field, toggle, slider, and scrollbar/scroll viewport native templates.
- [ ] Text sizes match current constants.
- [ ] Long server name and labels continue to use current autosize/ellipsis behavior.
- [ ] Scroll wheel sensitivity remains `35`.
- [ ] Escape and close button both close the menu.

## Settings Menu Behavior

- [ ] Opening settings closes chat.
- [ ] Opening settings closes other open game menus.
- [ ] Closing restores cursor locked/hidden.
- [ ] First selectable is the close button.
- [ ] Control changes in the settings window are staged until Apply.
- [ ] Apply commits the full settings draft through the existing `MultiplayerToolsCore` setters and keeps the window open.
- [ ] Apply/Close commits the full settings draft through the existing `MultiplayerToolsCore` setters and closes the window.
- [ ] Closing without Apply does not commit staged settings.
- [ ] `X` and Escape with unapplied changes show confirmation.
- [ ] Confirmation can Apply/Close or Discard/Close.
- [ ] Confirmation modal is built from cloned native Sledding UI templates.
- [ ] Native create-lobby controls remain immediate-save.
- [ ] Server capacity clamps 1-64.
- [ ] Message size clamps 50-100.

## Embedded Lobby UI Visual Parity

- [ ] Main menu host button is hidden.
- [ ] Join and quit buttons keep expected placement and styling.
- [ ] Embedded create-lobby panel remains visible after vanilla disables it.
- [ ] Embedded create-lobby root position/scale match current layout.
- [ ] Custom lobby name input uses native input styling.
- [ ] Guest bang command toggle uses native toggle styling.
- [ ] Close buttons in embedded create-lobby are hidden.

## Embedded Lobby Behavior

- [ ] Lobby name fallback order remains custom input, saved server name, player name, `Sledding Lobby`.
- [ ] Lobby name input updates preferences.
- [ ] Max players slider supports 64.
- [ ] Lobby creation uses selected max player value.
- [ ] EOS lobby creation uses selected max player value.
- [ ] Native public/password/peaceful/text-chat controls initialize from preferences.
- [ ] Native controls update preferences.
- [ ] Repeated `UIMainMenu.OnEnable` calls do not duplicate listeners or guest toggles.

## Safety

- [ ] No unrelated changes to chat commands or player tracking.
- [ ] Staged Apply behavior is implemented only for the settings window and does not accidentally change native create-lobby control behavior unless separately approved.
- [ ] No loss of inactive-object lookup needed for Il2Cpp/Unity UI templates.
- [ ] No dependency on editor-only Unity APIs.
