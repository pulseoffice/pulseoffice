# Round 208: ribbon key tips, show announcements, chart ink, keyboard walk of dialogs

## Scope
The Accessibility items queued by round 203: key tips on the ribbon, announcement of the slide show's screens, chart
text drawn in fixed black, and a headless keyboard walk of the chart editor, table look options, PDF export, page setup,
find and replace and media options dialogs.

## Changes
- **Ribbon key tips** (Medium, missing feature): pressing and releasing Alt alone, or F10, shows letter badges on the
  ribbon tabs; a letter opens that tab and shows badges on its visible, enabled commands (and on collapsed or overflow
  group buttons); a letter there runs the command (or focuses its field, or opens its drop-down) and closes the tips;
  Escape clears a partly typed tip, then steps back from commands to tabs, then closes. Any other key, a click or the
  window losing activation closes them; Alt used with another key never shows them, and they stay hidden while a modal
  is open or the interface is locked. Letters come from `KeyTipAssigner` (`IKeyTipAssigner`, in `UI.Commands`): the
  localised title is folded to unaccented capitals, word initials are tried first, then the title's other letters, then
  any free letter or digit; past 35 entries the tips take a `0` prefix, which no single tip uses, so no tip is the
  prefix of another. The typed key is read from the key symbol first (so non-QWERTY layouts work) and from the key code
  otherwise; the text input that follows a consumed letter is swallowed so it never reaches the document. The badges
  are drawn by `RibbonKeyTips` on a canvas over `RibbonView`, the ribbon control shared by Ledger, Deck and Folio, in the
  inverted text and surface tokens. `ShellWindow` closes the in-window menu bar when the tips open, because Avalonia's
  access-key handler opens it on the same Alt release. Tests: `Round208KeyTipAssignerTests` (tab titles in the five
  languages), `Round208RibbonKeyTipsTests`.
- **Show announcements** (Low): `ShowAnnouncer` keeps an assertive live region on the presentation window; black and
  white screens, the end screen, the slide number being typed on the keypad and every slide change are announced, in
  the five languages (`deck.present.announce.*`). `Round208ShowAnnouncementTests`.
- **Chart ink** (Low): `ChartRenderer` takes a `ChartInk` (text and paper brushes). Titles, axis titles, legend, data
  labels and the empty-chart notice use the ink, axis and plot labels use it at half opacity, and data-label backdrops
  use the paper. `ChartInk.Print` (black on white) stays the default for printing, export and rasterising;
  `ChartPreview` passes the `PulsePageInk` and `PulsePage` tokens, so dark and high-contrast themes read on screen.
  `Round208ChartInkTests`.
- **Dialog keyboard walk**: `tests/Shared/DialogKeyboardWalk.cs` opens a dialog in a real `DialogHost`, walks every Tab
  stop until the focus comes back, checks that each stop is inside the dialog, visible, enabled and visited once, that
  Shift+Tab walks the same stops and starts from the last one, that Escape cancels and gives the focus back, and that
  Enter runs the default action. It found:
  - **Medium**: Enter on a check box or radio button in a dialog toggled it (the button's own Enter handling) instead of
    running the default action, so dialogs made of options (table look, media options) could not be confirmed from the
    keyboard without tabbing to the footer. `DialogHost` now runs the default button on Enter from a check box or radio
    button; Space still toggles.
  - **Low**: find and replace with Find next had no default button, so Enter in the query field did nothing. Find next is
    now the default, and the result summary is a polite live region.
  Tests: `Round208DialogKeyboardWalkTests` (chart editor, table look, PDF export, find and replace, media options),
  `Round208PageSetupKeyboardTests`.

## Verification
The code and tests were written without building or running them in this round; the coordinator builds and runs
`tools/run-tests.py --changed` after joining the branches.

## Open items
Listed in `docs/audit-queue.md`, section Accessibility: key tips beyond the second level and on the menu bar, document
tabs and status bar, the live check of Alt against the in-window menu, non-text chart marks in fixed colours, the shape
style dialog walk, and a screen reader pass.
