# Round 245: Deck sections, filmstrip drag and outline identity; Ledger chart table warning

Date: 2026-11-19. Area: Deck sections, sorter and outline view; Ledger chart editor.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | A single empty section could be renamed or removed only through its header; the registry had only Remove Empty Sections for all of them. | `SlideSections.EmptyNear` finds the empty section right after the current slide (or right before it); new commands `deck.section.renameEmptyHere` and `deck.section.removeEmptyHere` act on it, in Slide › Section (prefix placement), with icons and titles in the five languages. Icons were also added to the other section commands. |
| Medium | The filmstrip had no slide drag except a drop onto an empty section header, and neither the filmstrip nor the sorter showed where a slide would land. | `SlideDragSession` (shared by filmstrip and sorter) resolves a drag to a move, a move into an empty section or a plain selection, and marks the target while dragging: an accent edge on the slide frame (before or after, horizontal in the sorter, vertical in the filmstrip) or an accent outline on the empty section header, through the adorner layer. |
| Medium | Moving an outline block with Alt+Shift+Up/Down past an identical block (same title and body) did nothing to the deck: the text was unchanged, so the outline was never applied. | The editor tracks the move: the two blocks' slides and entries are swapped in its tracked lists (`IOutlineBlockMove.Locate` gives the block index), and the outline is applied when the resulting slide order differs from the deck even with the same text. |
| Medium | A chart table already missing when the workbook opens (file reload) was drawn as a placeholder but not announced. | `ChartTableLossWatch.AnnounceStanding` raises the warning once when the Ledger view is built. |

## Open

- Identical outline blocks swapped by cut and paste (not by Alt+Shift+Up/Down) are still matched in order: the
  plain-text editor carries no hidden per-block identity, and invisible markers in the text were rejected because they
  break caret movement and leak into the clipboard.
- The drag indicator, the new section commands and the chart table warning on open have not been seen in the running app.
- Dragging has no threshold: releasing a press on another slide frame moves the slide, as the sorter did before.
