# Round 201: accessibility and keyboard use

## Scope
Accessible names on icon-only buttons in the shared controls, hard-coded colours, keyboard operation of the shell.

## Defects
- **Medium**: icon-only buttons without an accessible name. The drop arrows of split, colour, border, number-box and
  choice gallery slots on the ribbon, the band gallery's row scroll arrows and "more" button, and the remote
  browser's "up" button had only an icon (some a tooltip, which is not the automation name), so screen readers
  announced an unnamed button. Each now sets `AutomationProperties.Name` (the item title, or the new texts
  `ribbon.gallery.previousRow` and `ribbon.gallery.nextRow` in the five languages). Guarded by
  `Round201IconButtonNameTests`, a source scan of buttons built from `RibbonBuildContext.Flat` with only an icon or
  chevron, and of `DialogParts.Quiet` with an empty label.

## Open items
Listed in `docs/audit-queue.md`, section Accessibility.
