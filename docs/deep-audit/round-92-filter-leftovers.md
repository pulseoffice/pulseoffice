# Deep audit round 92 — filter leftovers of round 90

Scope: the two "Remaining" items of [round-90-filter-model.md](round-90-filter-model.md).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/round-90-filter-model.md | full | scope |
| src/PulseOffice.Sheets/RangeFilters.cs | full | G2: no way to remap fields |
| src/PulseOffice.Sheets/DatabaseRanges.cs | full | G2 |
| src/PulseOffice.Sheets/Filtering.cs | full | G2: AutoFilter area never moved |
| src/PulseOffice.Sheets/Worksheet.cs | 60-130 | G2 |
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | full | G2: no filter part |
| src/PulseOffice.Sheets.Engine/Operations/SheetSnapshot.cs | full | G2: undo did not hold filters |
| src/PulseOffice.Sheets/ExternalData.cs | 100-140 | database range areas already moved, filters inside them not |
| src/PulseOffice.Sheets/Workbook.cs | 140-180 | criteria sheets follow moves/removal by index; rename needs nothing |
| src/PulseOffice.Sheets.UI/LedgerStructureCommands.cs | full | G2: no filter refresh after the edit |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | full | G1 |
| src/PulseOffice.Sheets.UI/Dialogs/StandardFilterField.cs | full | G1 |
| src/PulseOffice.Sheets.UI/LedgerPartialCommands.cs | 185-240 | G1 |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs | 100-185 | G1 |
| src/PulseOffice.Sheets.UI/DataDialogs/AdvancedFilterView.cs | 1-124 | G1 |
| src/PulseOffice.Sheets.UI/SheetRangeFilters.cs | full | G1 |
| src/PulseOffice.Sheets.Engine/Operations/SheetRangeReference.cs | 1-60 | `Qualify` reused for the criteria text |

## Defects and fixes

- **G1 (medium)**: the standard filter dialog and the advanced filter dialog always opened empty, so editing a stored
  filter meant retyping it. `ISheetRangeFilters.Stored` finds the stored filter whose area covers the selection (named
  database range first, then the sheet's anonymous range); the standard dialog opens on that area with the rows
  rebuilt by `IStandardFilter.Decompose` (inverse of `Compose`: left-to-right trees, at most
  `StandardFilterEditor.MaximumConditions` rows, every operator the dialog offers). A tree it cannot show (or-of-and
  groups not in reading order, more than eight terms, blank conditions) or an advanced filter on the area is not
  discarded: the dialog says so (`filter.tooComplex`, `filter.replacesAdvanced`) and confirming replaces it. Unique
  records of a stored standard filter are kept. The advanced dialog reopens with the stored area, the sheet-qualified
  criteria range and the unique flag; over a standard filter it says that confirming replaces it
  (`advancedFilter.replacesStandard`). Texts in five languages.
- **G2 (major, silent corruption)**: the AutoFilter area, the sheet's standard/advanced filter area, the filters of
  named database ranges (column filters and condition trees) and criteria ranges did not follow row/column insertion and
  deletion: after deleting a column the conditions applied to the wrong columns. New `FiltersPart` in `SheetStructure`
  (file `FilterStructurePart.cs`): areas move with `StructureShift`, a column deleted inside an area drops its
  conditions and the later fields are renumbered (`FilterNode.Remapped`, `RangeFilter.Moved`; a group left with one
  child collapses, an empty filter disappears); criteria ranges on the edited sheet move for filters of every sheet and
  database range, and disappear when their cells are deleted; an area deleted entirely deactivates the AutoFilter /
  removes the filter. `SheetSnapshot` now holds the AutoFilter memento and every sheet's range filter, so the structural
  command stays one undo step; `LedgerStructureCommands` refreshes the filtered rows after the edit when the sheet has
  a filter (inside the same step). Filtered-row marks already shifted (round 90, F5). Sheet rename needs nothing
  (criteria are by sheet index); sheet moves and removal were already handled by `Workbook.ReindexSheetFilters`.

## Remaining

- Insert/delete cells with shift (`ShiftBand`) does not move filter areas (as before; the reference suite refuses most
  such shifts through a filtered area).

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round92FilterLeftoversTests.cs (AutoFilter area and columns, rows removing the
  area, tree renumbering, named ranges and criteria on another sheet, snapshot undo, decompose and its limits)
- tests/PulseOffice.Sheets.UI.Tests/ChartSeriesSourcesCommandTests.cs `Deleting_a_filtered_column_renumbers_the_autofilter_in_one_undo_step`
- tests/PulseOffice.Formats.OpenXml.Tests/Round90FilterParityTests.cs `A_filter_renumbered_after_a_column_deletion_crosses_both_formats`

Runs: Sheets.Engine.Tests 1808/1808, Sheets.UI.Tests 467/467, Formats.OpenXml.Tests (Round90) 8/8, Localization.Tests 32/32,
Architecture.Tests 43/43.

Live checks: reopen the standard filter on a filtered range (rows reloaded; a mixed ODS tree shows the notice); reopen
the advanced filter (criteria and unique reloaded); delete a filtered column inside an AutoFilter, undo/redo; insert rows
above a criteria range on another sheet and reapply.
