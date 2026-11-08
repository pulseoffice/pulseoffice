# Deep audit round 90 — database-range filter model

Scope: the remaining items of round 89 ([round-89-filters-2.md](round-89-filters-2.md)).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/SheetLayout.cs | 1-330 | F5: filtered-row marks did not shift |
| src/PulseOffice.Sheets/Filtering.cs | full | ok |
| src/PulseOffice.Sheets/DatabaseRanges.cs | full | F1: no filter model |
| src/PulseOffice.Sheets.UI/Scripting/FilterFacade.cs | full | F4 |
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs | full | F1, F3 |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | full | F1 |
| src/PulseOffice.Sheets.UI/GridParts/GridViewport.cs | 100-160 | F5: own per-sheet hidden set did not shift either |
| src/PulseOffice.Sheets.UI/LedgerPartialCommands.cs | 170-290 | F1 |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs | 80-240 | F1 |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | full | F1, F2 |
| src/PulseOffice.Sheets/Workbook.cs | 100-180 | sheet reindexing |
| src/PulseOffice.Sheets.Engine/Recalculation/CalculationEngine.cs | 170-195, 1175-1215 | EvaluateOffset reused for F3 |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs | full | F1 (XLSX side) |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | full | ok |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableWriter.cs | 30-140 | F1 |
| src/PulseOffice.Formats.OpenXml/XlsxMetadataMapper.cs | 95-230 | F1: `_xlnm` names other than print ones ignored |
| src/PulseOffice.Formats.Odf/OdsFormat.cs | 95-135 | ODF272 |
| src/PulseOffice.Sheets.UI/LedgerFilterCommands.cs | full | AutoFilter refresh dropped the other filters' rows |
| src/PulseOffice.Sheets.UI/Scripting/ScriptSheetEdits.cs | 1-55 | same |
| src/PulseOffice.Sheets.UI/LedgerDocumentFactory.cs | full | F2 hook |
| src/PulseOffice.Sheets.UI/SheetScriptApi.cs | 150-170 | F4 |
| src/PulseOffice.Sheets/Worksheet.cs, WorkbookCopier.cs, Engine/Operations/SheetDuplicator.cs | copy paths | RangeFilter copied |

## Design

- **Model (PulseOffice.Sheets, `RangeFilters.cs`)**: `FilterNode` = `FilterTerm(Field, Criteria)` (canonical criteria
  text, as the AutoFilter stores it) or `FilterGroup(Join, Children)` (and/or, any depth, any field mix).
  `RangeFilter` = `Condition` tree + `Criteria` (`AdvancedCriteria(SheetIndex, Range)`, the advanced filter's criteria
  range) + `UniqueOnly`. A named database range carries it in `DatabaseRange.RangeFilter`; the sheet's anonymous range in
  `Worksheet.RangeFilter` (`SheetRangeFilter(Range, HasHeader, Filter)`). Criteria sheet indexes follow sheet moves and
  removal (`Workbook.ReindexSheetFilters`, `DatabaseRangeCollection.Reindex`); a removed criteria sheet drops the
  criteria. `FilterTreeColumns` converts between trees and AutoFilter column filters (used by both formats).
- **Engine**: `ISheetFilter.RowsToHide(sheet, range, FilterNode)` evaluates trees (`IRowCondition` composite:
  matched column, computed, group). `IRangeFilterEvaluator` combines AutoFilter, named-range and anonymous filters of a
  sheet (`HiddenRows`) and restores them on open (`Restore`). `StandardFilter.Compose` turns the dialog's rows into a tree
  that keeps the dialog's left-to-right reading.
- **Computed criteria (F3)**: a criteria column whose header is blank or names no data column, holding a formula, is a
  computed criterion: the formula is evaluated for every data row with its relative references moved by the row's
  distance from the first data row (`IShiftedFormulaEvaluator`, `EngineShiftedFormulas` over
  `CalculationEngine.EvaluateOffset`). TRUE or a non-zero number passes; errors and anything else fail. Without an engine
  the criterion rejects every row (never silently passes).
- **UI**: `ISheetRangeFilters` (Sheets.UI) stores a filter as one undo step (`DelegateAction`, the model swap plus the
  row refresh) and refreshes a sheet's filtered rows from all its filters, so the AutoFilter, the standard filter, the
  advanced filter and macros no longer erase each other's hidden rows. The standard filter dialog and the in-place
  advanced filter go through it; the AutoFilter commands and `ScriptSheetEdits` refresh through it.
- **Row marks (F5)**: `SheetLayout` keeps the filter marks and the rows it hid itself as `RunLengthSet`s that shift with
  row insertion and deletion and are copied with the layout; `ApplyFilteredRows` replaces the grid's own per-sheet
  dictionary (which did not shift either). On open, rows the file already hides and a filter excludes are adopted as
  filter-hidden, so clearing the filter shows them again.
- **ODS**: the tree is written as nested `table:filter-and`/`table:filter-or`/`table:filter-condition` (same-join
  children flattened, as the schema alternates them); an advanced filter writes
  `table:condition-source="cell-range"` and `table:condition-source-range-address`, with the conditions derived from
  the constant criteria cells as children (the schema requires one); unique records write `table:display-duplicates="false"`.
  Reading: a filter with a condition source becomes `Criteria`; plain conditions that fit AutoFilter columns stay column
  filters on ranges with buttons, anything else becomes a tree. The anonymous range without buttons is now a standard
  filter (`Worksheet.RangeFilter`), not column filters on an inactive AutoFilter. A standard filter on the AutoFilter's
  range is written in the same anonymous range (and-joined).
- **XLSX**: a column-shaped tree on the sheet range is written as the sheet's `autoFilter` with `customFilters` (declared
  **XLSX232**, the range shows buttons when reopened); an advanced filter in place writes `_xlnm._FilterDatabase` (hidden)
  and `_xlnm.Criteria` local names, read back as the stored advanced filter (the hidden rows are written as hidden rows);
  a column-shaped tree on a named range joins its table's `autoFilter`. Everything else is declared **XLSX233**
  (mixed or-groups, unique records, advanced filters on named ranges, filters next to an AutoFilter).
- **ODF272**: a standard or advanced filter on a range other than the sheet's AutoFilter cannot share the single
  anonymous range; the AutoFilter wins and the loss is declared.
- **Macros (F4)**: `filter.ByConditions(column, criteria, secondCriteria, join)` with `join` "and"/"or" (anything else
  throws); titles, summary and parameter texts in five languages.

## Defects and fixes

- **F1 (major, data loss)**: standard filter and in-place advanced filter were transient (no undo, no model, not saved),
  and any AutoFilter change erased their hidden rows. Fixed by the model, `ISheetRangeFilters` and both format mappers above.
- **F2 (medium)**: filters of named database ranges were read but never applied. `IRangeFilterEvaluator.Restore` runs in
  `LedgerDocumentFactory` after the first recalculation of an opened workbook.
- **F3 (medium, missing feature)**: computed criteria in the advanced filter (also for "copy results to").
- **F4 (minor)**: macros could not set a second condition: `filter.ByConditions`.
- **F5 (medium)**: filtered-row marks (and the grid's own hidden-row bookkeeping) did not follow row insertion/deletion.

## Remaining

- Sorting inside a filtered range still sorts hidden rows too (same as the reference suite).
- The standard filter dialog does not reload a stored filter's rows when reopened (it starts empty, as before).
- The AutoFilter range itself and stored filter ranges do not move with row/column insertion (pre-existing, all filters alike).

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round90FilterModelTests.cs (trees, left-to-right composition, computed criteria,
  restore on open, unique records, row marks shift, criteria sheet reindexing)
- tests/PulseOffice.Formats.OpenXml.Tests/Round90FilterParityTests.cs (ODS trees, XLSX232/XLSX233/ODF272, advanced
  criteria through both formats and ODS→XLSX)
- tests/PulseOffice.Sheets.UI.Tests/LedgerCommandsRound6Tests.cs (advanced filter stored and undone, computed criteria in
  the store), LedgerFacadeContractTests.cs `filter.ByConditions`

Runs: Sheets.Engine.Tests 1801/1801, Sheets.UI.Tests 466/466, Formats.OpenXml.Tests 2410/2410, Formats.Odf.Tests 812/812,
Scripting.Tests 158/158, Localization.Tests 32/32, Architecture.Tests 43/43, Desktop.Tests (filter, dialogs) 12/12.

Live checks: standard filter with "North and >15 or East", undo/redo, save ODS and XLSX and reopen; advanced filter in
place with a computed criterion (blank header, `=B2>AVERAGE($B$2:$B$6)`); open an ODS with a filtered named range; insert
rows above filtered rows and clear the filter; AutoFilter change while a standard filter is active.
