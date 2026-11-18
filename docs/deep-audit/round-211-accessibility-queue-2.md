# Round 211: third key tip level, chart mark ink, shape style dialog walk

## Scope
The Accessibility items queued by round 208: key tips beyond the second level and on the shell's own surfaces,
non-text chart marks in fixed colours, and the keyboard walk of the shared shape style dialog.

## Changes
- **Third key tip level** (Medium, missing feature): `KeyTipLevel.Items`. `RibbonBuildContext.Popover` now tracks the
  open ribbon popovers (`OpenPopover`, `PopoverOpened`, `PopoverClosed`); when a command tip opens one (menu, gallery,
  split-button list, collapsed or overflow group), `RibbonKeyTips` shows tips on its items, collected by
  `KeyTipCollector` (ribbon slots by their own tips, other buttons by accessible name, text or tooltip). A letter runs
  the item; an item that opens a nested popover shows the next level; an item that runs a command closes every open
  popover and the tips. Escape closes the top popover and steps back to its parent's items or to the commands; a
  popover closed by other means closes the tips. While a popover lives in its own popup window, `RibbonView` listens to
  that window's keys too. Split buttons (`SplitGallerySlot`, `BorderGallerySlot`, `ColorSplitSlot`, the gallery arrow
  of `ChoiceSlot`) declare their arrow as `RibbonSlot.Opener`, which gets a second tip, "<title> options"
  (`ribbon.keyTip.options`, five languages), so the list is reachable while the main part still runs the command.
- **Shell key tip regions**: `IKeyTipRegion` targets join the tab level. `ShellWindow` registers the title-bar menus
  (`MenuKeyTipRegion`: opens the menu and focuses the top item), the document tabs (`DocumentTabsView`, from two open
  documents: activates the document) and the status bar (`ButtonKeyTipRegion`: command segments, zoom, notification
  history). Badges whose anchor lies outside the ribbon are placed in the anchor's adorner layer, centred on its bottom
  edge, instead of the ribbon's canvas, which would have clamped them into the ribbon.
- **Chart mark ink** (Low): `ChartInk` gains `Stroke` (axis lines, minor ticks, crossings, group brackets, box-plot
  whiskers, quantile reference line, the empty-chart frame), `Grid` (value grid, radar and parallel webs), `Strong`
  (bullet target, box-plot median), `Track` (gauge and bullet backgrounds) and `Separator` (pie, doughnut, treemap,
  funnel and sunburst slice separators and the doughnut hole, which take the paper). All derive from the text and paper
  brushes, so `ChartInk.Print` keeps the printed look. `ChartFrame.Marks` carries the ink into the plots. The Ledger grid
  now paints charts with `IGridTheme.ChartInk()` (text primary on surface) and a themed border; printing, export and
  rasterising keep `ChartInk.Print`.
- **Shape style dialog walk**: `tests/Shared/TestFontPickers.cs` builds working font picker services (fixed catalog,
  in-memory recents, real runtime fonts and flyout dismissal, idle activation) over the walk's `DialogHost`, which
  `DialogKeyboardWalk.Dialogs` now exposes. `Round211ShapeStyleKeyboardTests` walks each of the four sections, checks
  that the font picker is a Tab stop and that Enter on an effect check box confirms.

Tests: `Round211RibbonKeyTipsTests`, `Round211ChartInkTests`, `Round211ShapeStyleKeyboardTests`.

## Verification
The code and tests were written without building or running them in this round; the coordinator builds and runs
`tools/run-tests.py --changed` after joining the branches. The shape style walk has therefore not yet reported its
findings; whatever it finds is a follow-up.

## Open items
Listed in `docs/audit-queue.md`, section Accessibility.
