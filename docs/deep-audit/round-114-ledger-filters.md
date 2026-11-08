# Deep audit round 114 — Ledger filters and anchored objects

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/SheetObjectAnchoring.cs | full | A1 fixed |
| src/PulseOffice.Sheets/SheetPlacement.cs (ISheetPlacement) | full | context for A1 |
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | 1-140 of 576 | filters already a structure part |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | full | no defect |
| src/PulseOffice.Sheets.Engine/Operations/ReferenceStructureParts.cs | full | no defect |
| src/PulseOffice.Sheets/Filtering.cs | full | no defect |
| src/PulseOffice.Sheets/RangeFilters.cs | full | no defect |
| src/PulseOffice.Sheets/DatabaseRanges.cs | full | no defect |
| src/PulseOffice.Sheets.UI/SheetRangeFilters.cs | full | no defect (stored filter lookup) |
| src/PulseOffice.Sheets.UI/LedgerFilterCommands.cs | full | no defect; protection checked after the dialog |
| docs/deep-audit/round-92-filter-leftovers.md | full | scope |

Not read in this round: StandardFilter.cs, StandardFilterField.cs, SheetFilter.cs, RangeFilterEvaluator.cs,
RangeMoveFilters.cs, FilterValueText.cs, FilterTreeColumns.cs, AdvancedFilterView.cs, FilterMenuModel.cs,
FilterMenuView.cs, FilterFacade.cs, LedgerDatabaseRangeCommands.cs, OdfDatabaseRangeMapper.cs and the XLSX filter
mapper. They remain for a later round.

## Leftovers of round 90

Both were already closed in round 92: `ISheetRangeFilters.Stored` reloads the stored filter into the standard and
advanced dialogs, and `FiltersPart` in `SheetStructure` moves AutoFilter, sheet range filter, database-range filters
and criteria ranges on row/column insertion and deletion (tests in Round92FilterLeftoversTests). Rerun here: green.
Still open as in round 92: insert/delete cells with shift does not move filter areas.

## Defects

- **A1 (medium, data loss)**: `SheetObjectFollower` settled a move-and-size object from the model it had itself clamped:
  hiding the row (or column) holding the object's start or end clamped the offset inside that row to zero, and showing
  it again could not recover it, so the object jumped to the top/left of the cell or lost part of its size. The same
  happened through filters (`ApplyFilteredRows`). Fix: the follower remembers, per settled object instance
  (`ConditionalWeakTable`), the cell markers it was settled from, and captures from those markers while the instance is
  unchanged; any edit creates a new instance and falls back to a fresh capture. Move and Fixed objects were not
  affected (verified by test).
  Remaining: a row/column structure edit made while the rows are hidden captures from the clamped model.

## Tests

- tests/PulseOffice.Sheets.Tests/ObjectAnchoringRound114Tests.cs: 4 failed before the fix, all 6 pass after.
- Sheets.Tests 371/371; Sheets.Engine.Tests (filter, round 92, round 112 filters) 89/89.

## Live checks

Insert a shape with "move and size with cells" a few points inside a cell, hide and show that row (and its column),
and filter that row out and back in: the shape returns to the same place and size.
