# Parity Checklist

Use this checklist during the rewrite. Any unchecked item is a possible regression.

## Build

- [x] Project builds with the existing `MultiplayerTools.csproj`.
- [x] No new external packages are required unless explicitly approved.
- [ ] `PostBuild` copy behavior remains unchanged.

## Settings Menu Visual Parity

- [x] `!settings` opens a modal over the native UI.
- [ ] Settings window can resolve a native parent from main menu, pause menu, active game, and lobby states.
- [x] Backdrop covers the screen and blocks raycasts.
- [x] Panel remains centered and `900 x 640`.
- [x] Header, close button, labels, inputs, toggles, sliders, divider, and scroll viewport match current appearance.
- [x] Existing game templates are used when available.
- [x] Missing native templates are logged clearly instead of silently creating plain fallback controls.
- [x] Settings window aborts opening if required native templates are missing.
- [x] Settings window requires panel/background, text/label, button, input field, toggle, slider, and scrollbar/scroll viewport native templates.
- [x] Text sizes match current constants.
- [x] Long server name and labels continue to use current autosize/ellipsis behavior.
- [x] Scroll wheel sensitivity remains `35`.
- [x] Escape and close button both close the menu.

## Settings Menu Behavior

- [ ] Opening settings closes chat.
- [ ] Opening settings closes other open game menus.
- [ ] Closing restores cursor locked/hidden.
- [ ] First selectable is the close button.
- [x] Control changes in the settings window are staged until Apply.
- [x] Apply commits the full settings draft through the existing `MultiplayerToolsCore` setters and keeps the window open.
- [x] Apply/Close commits the full settings draft through the existing `MultiplayerToolsCore` setters and closes the window.
- [x] Closing without Apply does not commit staged settings.
- [x] `X` and Escape with unapplied changes show confirmation.
- [x] Confirmation can Apply/Close or Discard/Close.
- [x] Confirmation modal is built from cloned native Sledding UI templates.
- [x] Native create-lobby controls remain immediate-save.
- [x] Server capacity clamps 1-64.
- [x] Message size clamps 50-100.

## Embedded Lobby UI Visual Parity

- [x] Main menu host button is hidden.
- [x] Join and quit buttons keep expected placement and styling.
- [x] Embedded create-lobby panel remains visible after vanilla disables it.
- [x] Embedded create-lobby root position/scale match current layout.
- [x] Custom lobby name input uses native input styling.
- [x] Guest bang command toggle uses native toggle styling.
- [x] Close buttons in embedded create-lobby are hidden.

## Embedded Lobby Behavior

- [ ] Lobby name fallback order remains custom input, saved server name, player name, `Sledding Lobby`.
- [x] Lobby name input updates preferences.
- [x] Max players slider supports 64.
- [x] Lobby creation uses selected max player value.
- [x] EOS lobby creation uses selected max player value.
- [x] Native public/password/peaceful/text-chat controls initialize from preferences.
- [x] Native controls update preferences.
- [ ] Repeated `UIMainMenu.OnEnable` calls do not duplicate listeners or guest toggles.

## Safety

- [ ] No unrelated changes to chat commands or player tracking.
- [x] Staged Apply behavior is implemented only for the settings window and does not accidentally change native create-lobby control behavior unless separately approved.
- [x] No loss of inactive-object lookup needed for Il2Cpp/Unity UI templates.
- [x] No dependency on editor-only Unity APIs.
