# Deep audit round 21: whole rows and columns in Ledger (continued)

Continues [round 20](ledger-whole-ranges-round20.md). Each operation gets a test with a whole column or row and a few
values, capped at a few hundred milliseconds (`tests/PulseOffice.Sheets.Engine.Tests/WholeRangeRound21Tests.cs`).

## Coverage (updated every five files)

| Operation | Code | Outcome |
|---|---|---|
| Sort | `Sheets.Engine/Operations/SheetSort.cs` (whole) | already bounded by `Occupied`; test passes |
| Remove duplicates | `SheetDataTools.cs` `DuplicateRemover` (whole), `Sheets.UI/LedgerDataCommands.cs` | defect 1 fixed |
| Text to columns | `SheetDataTools.cs` `TextToColumns`, `LedgerToolCommands.cs` 172-224 | 1M cheap lookups, under the cap; guard is run-length |
| Outline detect, group, collapse | `Sheets.Engine/Operations/SheetOutline.cs` (whole) | defect 2 fixed |
| Insert/delete cells and whole rows/columns | `SheetCellShift.cs` (whole), `SheetStructure.cs` (head) | defect 4 fixed |
| Hide/show | `Sheets/SheetLayout.cs` 120-330 | run-length; test passes |
| Undo snapshots | `SheetSnapshot.cs` (whole) | sparse; test passes |
| Find and replace | `LedgerFindTarget.cs` (whole), `WorkbookSearch.cs` 20-80 | sparse; test passes |
| Copy, paste, paste link, clipboard text | `SheetClipboard.cs` 60-470, `Sheets.UI/Exchange/LedgerExchange.cs` 80-200, 340-349 | defects 3 and 5 fixed |
| Edit guard | `Sheets/SheetEditGuard.cs` (whole) | run-length; correct |
| Clear contents / clear formats | `LedgerCellOperations.cs` 25-60, `LedgerFillCommands.cs` 105-200 | sparse / bounded by `ISelectionWorkArea.Plan` (round 20) |
| Validation, conditional formatting | `ConditionalRangeSnapshots.cs`, `LedgerAnalysisCommands.cs` 181-192 | rules are ranges, evaluation reads stored cells only |
| CSV export | `Formats.Interchange/CsvFormat.cs` 148-260 | bounded by `UsedRange` |

## Defects

1. **Medium** — `DuplicateRemover.Plan` (`SheetDataTools.cs` line 31): on a whole column every empty row past the data
   is a duplicate of the first, so the plan listed about a million rows (629 ms, wrong result). The command bounded it,
   the service and macros did not. Fix: `DuplicateRemover` takes `ISelectionWorkArea` and bounds the range to the used area.
2. **Serious** — `SheetOutline.Collapse/Expand`: grouping whole rows and collapsing wrote one row height and one dictionary
   entry per row (over 1 s, a million-entry `OutlineGroup.HiddenSizes`). Fix: collapse and expand use the layout's run-length
   `HideRows/ShowRows` (`HideColumns/ShowColumns`), which already remember manual sizes; `HiddenSizes` removed from the model.
3. **Blocking** — `SheetClipboard.PasteLinks`: paste special with link of a whole column wrote a formula into every row
   (12 s, a million formulas in the history). Fix: `ClipboardExtent` bounds a whole-axis snapshot to its occupied cells.
4. **Minor** — `SheetCellShift.Move` blanked every address of a whole-column insert range; now only stored cells.
5. **Serious** — `TabularText.Write`: copying whole columns allocated a `string[1048576, n]` and put a million lines on the
   clipboard. Fix: the same `ClipboardExtent`; bounded ranges keep their trailing blank rows.
6. **Minor** (open point of round 20) — `SheetRangeCapture.Occupied` and `SheetClipboard.Occupied` clamped to the last
   stored cell, so formatted empty cells held as layout style blocks (how ODF and XLSX files load them) below the last value
   were dropped by a whole-column copy. Fix: `Worksheet.LastFormattedRow/LastFormattedColumn` add the style blocks that do not
   reach the sheet edge (an edge-reaching block is a band and would re-expand the copy to a million rows).

## Tests

`WholeRangeRound21Tests` (15, Sheets.Engine.Tests), `CsvWholeColumnRound21Tests` (Formats.Interchange.Tests). Verified
failing before the fix: paste link (12 s), duplicates (wrong plan), collapse (over 1 s). Clipboard text and style-block copy
were written with the fix and fail by construction on the old code (a million lines; rows clamped to 3).
Runs: Sheets.Engine.Tests 1408/1408, Sheets.UI.Tests 396/396, Sheets.Tests 351/351, Desktop builds with no warnings.

## Open points

- `TextToColumns.Plan` still walks every row of a whole-column request (cheap lookups, within the cap); the command does not bound it.
- `SheetOutline.Detect` on whole rows walks every column of each row up to the request; within the cap with few rows.
- Expanding an outer outline group now shows every row inside it, nested collapsed groups included (as before in effect).

## Live checks

Select A:B with a few values: copy and paste into a text editor (few lines); Paste special, link, into E1 (few formulas,
instant). Select column A, Data, Group rows, then collapse and expand: instant. Remove duplicates on column A: only real
duplicates go. Copy a column of an ODS whose formatted cells extend below the data: the format comes along.
