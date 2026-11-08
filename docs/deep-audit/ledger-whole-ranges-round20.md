# Deep audit round 20: whole rows and columns in Ledger

Scope: operations that accept whole rows or columns, in `src/PulseOffice.Sheets` and `src/PulseOffice.Sheets.UI`
(`src/PulseOffice.Sheets.Engine` excluded: another agent owns it).

## Coverage

| File | Read | Outcome |
|---|---|---|
| `Sheets.UI/LedgerViewParts/LedgerCellOperations.cs` | fill, clear contents (lines 1-170) | defect 1 fixed |
| `Sheets.Engine/Operations/SheetFill.cs` | lines 1-80 (read only) | lanes iterated over the whole request; bounded by the caller |
| `Sheets/Addressing/SelectionWorkArea.cs` | whole | correct, reused |
| `Sheets/SheetRandomFill.cs` | whole | defect 2 fixed |
| `Sheets.UI/Exchange/SheetRangeCapture.cs` | lines 1-95 | copy bounded by `Occupied`; see open point |
| `Sheets.UI/LedgerSheetExtraCommands.cs` | lines 235-300 | random fill and shuffle entry points |
| `Sheets.UI/ActiveSheetContext.cs` | cell format path | already bounded by `ISelectionWorkArea.Plan` |
| `Sheets/Worksheet.cs` | `UsedRange`, `SetArrayFormula` | array range is user-bounded |
| `Sheets.UI/LedgerAnalysisCommands.cs` | references reader | capped by `MaximumReferencedCells` |

Not reached in this session (still to audit for whole ranges): sort, find and replace, validation,
conditional formatting, insert/delete, hide, group, text to columns, remove duplicates, undo snapshots, CSV export.

## Defects

1. **Serious** — `LedgerCellOperations.ApplyFill` (was line 128): filling whole columns sideways (A:A to A:C) walked
   every one of the 1,048,576 lanes in `SheetFill.Extend` and enumerated every target cell for the lock check, ~3.6 s
   for five used rows; whole rows downwards did the same on 16,384 columns. Fix: `LanesInUse` bounds the lanes
   (rows for a horizontal fill, columns for a vertical one) to the sheet's used range, and the lock check runs only on
   the produced cells. Fill copies values and formulas only, so formatted-but-empty rows need no lane. Test:
   `LedgerGridRound19Tests` caps lowered from 10 s to 500 ms; verified failing before, passing after (~tens of ms).
2. **Medium** — `SheetRandomFill.Shuffle` (line 62): shuffling a whole column built a 1,048,576-entry plan, scattered
   the used values across the whole column and wrote a million cells into the history. Fix: `SheetRandomFill` takes
   `ISelectionWorkArea` by injection and bounds whole rows and columns to the used range. Tests:
   `WholeRangeRound20Tests` (two, verified failing before).

## Open points

- `SheetRangeCapture.Occupied` clamps to `LastUsedRow`/`LastUsedColumn`: empty cells with an explicit style beyond the
  last value are not copied with a whole-column copy (minor, not changed).
- `SheetRandomFill.Plan` on a whole column still produces one value per row: that is the user's explicit request.
- `LedgerRound6bAuditTests.LaRicercaObiettivoNonScriveUnaCellaBloccataDiUnAltroFoglioProtetto` failed once in a full
  project run and passed alone and on the next full run: intermittent, unrelated to this round.

## Live checks

Select A:A with a few values, drag the fill handle to C: instant. Select A:A and run Data, Shuffle: only the used rows
change order, undo restores them.
