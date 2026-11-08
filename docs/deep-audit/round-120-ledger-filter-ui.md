# Round 120: Ledger filter leftovers and sort audit

Follows round 117 (docs/deep-audit/round-117-ledger-filters-2.md). The branch was fast-forwarded to `work` (rounds 114-117)
before starting. Each defect got a test first; where the test only used existing API it was run red before the fix
(noted below), the others were red at compile time because the fix introduces the API they exercise.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.UI/FilterMenuView.cs | 383/383 | 2 minor fixed |
| src/PulseOffice.Sheets.UI/DataDialogs/FilterMenuModel.cs | 288/288 | 2 minor fixed |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | 128/128 | 1 minor fixed |
| src/PulseOffice.Sheets.UI/Dialogs/StandardFilterField.cs | 95/95 | wired to the fix |
| src/PulseOffice.Sheets.Engine/Operations/RangeFilterEvaluator.cs | 86/86 | 1 minor fixed |
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs | 1-170, 250-290 of 332 (rest read in round 117) | 1 minor fixed |
| src/PulseOffice.Sheets.Engine/Operations/FilterValueText.cs | 44/44 | reused |
| src/PulseOffice.Sheets.UI/DataDialogs/AdvancedFilterView.cs | 207/207 | 2 minor fixed |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs | 149/149 | 1 medium fixed |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxFilterDateGroups.cs | 51/51 | extended |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 319/319 | 1 medium fixed |
| src/PulseOffice.Sheets.Engine/Operations/SheetSort.cs | 228/228 | 1 medium fixed, notes |
| src/PulseOffice.Sheets.UI/DataDialogs/CustomSortOrders.cs | 33/33 | 1 minor noted |
| src/PulseOffice.Sheets.UI/DataDialogs/LedgerSortServices.cs | 39/39 | clean |
| src/PulseOffice.Sheets.UI/SheetSortGate.cs | 32/32 | wired to the fix |
| src/PulseOffice.UI.Controls/Sorting/SortLevelsModel.cs | 115/115 | clean |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs (SortAsync) | 64-107 | 1 minor noted |
| src/PulseOffice.Sheets.UI/LedgerEditDataCommands.cs (Sort) | 84-101 | 1 minor noted |
| src/PulseOffice.UI.Controls/Sorting/SortLevelsView.cs | not read this round | open |

Sort state persistence: neither the model nor the ODS/XLSX codecs carry a sort descriptor (`table:sort` inside a
database range, `sortState` in a worksheet or table). A search of both codec projects finds no reader or writer. The
sorted cells themselves cross both formats; only the remembered sort settings are not kept (see open items).

## Defects fixed

### Medium
1. **Allowed dates written as serial numbers** (`XlsxAutoFilterCodec.cs` Build, `OdfDatabaseRangeMapper.cs` Conditions):
   other applications compare a value filter with the shown text, so after reapplying the filter there the selection
   was lost. Now the shared `FilterColumnDates` (`PulseOffice.Sheets`) finds the date-formatted cells of the column:
   XLSX writes them as `dateGroupItem` (year, month, day, plus hour/minute/second with `dateTimeGrouping="second"` when
   the value has a time); ODS writes `filter-set-item` values as ISO 8601 text (`2026-03-01`, or
   `2026-03-01 13:45:00`), which is how the reference suite writes date entries. Readers map ISO items back to the
   matching cells (`FilterColumnDates.Resolve`), XLSX date groups were already resolved since round 117, so the saved
   view stays the same. Test: `Round120FilterDateTests` (two tests, red before the fix).
2. **Sorting split array formulas and spilled results** (`SheetSort.cs:54`): rows holding part of an array formula were
   reordered, leaving anchors and members with stale ranges. `ISheetSort.Splits` now refuses the sort (the engine
   returns without changes, so macros are covered too) and `SheetSortGate` reports it in the status bar
   (`status.sort.arrays`, five languages). Test: `Round120SortTests`.

### Minor
3. **Filter menu numbers in invariant form** (`FilterMenuView.cs:326`): `FilterValueCount.Shown` carries the number as
   the spreadsheet locale writes it (`IFilterValueText.Display`, decision 17); the key stays canonical, so filtering is
   unchanged; the search matches the shown text. `FilterColumnFactsReader` takes `IFilterValueText` and
   `IFilterColumnDates` (registered in `LedgerDataComposition`). Test: `Round120FilterMenuTests.Numbers_in_the_filter_list_follow_the_spreadsheet_locale`.
4. **Count while searching disagreed with the result** (`FilterMenuModel.ShownRows`, `FilterMenuView.Choice`): the new
   `FilterMenuModel.IncludesBlanks` (blanks only when not searching) feeds both. Test: `While_searching_the_shown_count_and_the_choice_agree_on_blanks`.
5. **Standard filter could not show "empty" conditions** (`StandardFilter.cs:114`): new `FilterCondition.IsEmpty` and
   `IsNotEmpty` ("=" and "<>" with no value), readable by `Decompose`, kept by `IStandardFilter.Applies` when the dialog
   is confirmed (rows with an empty value and another condition are still dropped); the value box is disabled for them.
   Labels in the five languages. Test: `Round120FilterTests.Equal_to_empty_and_not_empty_survive_decomposition_and_hide_the_right_rows`.
6. **Number 1 and text "1" deduplicated together** (`RangeFilterEvaluator.cs:79`, `SheetFilter.cs:72`): both now use
   the injectable `IFilterRowKey` (`FilterRowKey`, kind-tagged key; text stays case-insensitive). Test:
   `Number_one_and_text_one_are_not_duplicates` (red before the fix).
7. **Advanced filter preview white in dark theme** (`AdvancedFilterView.cs:67,144-163`): table, header, lines and text
   use theme tokens (Surface, SurfaceSunken, Border, TextPrimary). Test: `LedgerDataDialogsTests.Il_filtro_avanzato_segue_il_tema_scuro`.
8. **Preview count ignored computed criteria** (`AdvancedFilterView.cs:184`): `IAdvancedFilterProbes.For` takes the
   engine's `IShiftedFormulaEvaluator`, passed by the command. Test: `The_advanced_filter_preview_counts_rows_through_computed_criteria`.

## Open items (noted, not fixed)
- Sort settings are not persisted in ODS (`table:sort`) nor XLSX (`sortState`); the model has no sort descriptor. Not
  a cell-data loss; a future block should add the descriptor to `DatabaseRange`/autofilter and cross both formats.
- `LedgerToolCommands.SortAsync` builds custom orders from `CultureInfo.CurrentCulture`, not the spreadsheet locale.
- `LedgerEditDataCommands.Sort` (quick ascending/descending) does not extend a single cell to the current region and
  does not reapply an active autofilter afterwards.
- `SheetSorter` pushes one undo action per cell: memory heavy on very large ranges.
- Non-date numbers in XLSX `<filter val>` stay canonical rather than the shown text.

## Tests run
- PulseOffice.Formats.OpenXml.Tests: 2478/2478.
- PulseOffice.Sheets.Engine.Tests: 1844/1844.
- PulseOffice.Sheets.UI.Tests: 485/485.
- PulseOffice.Formats.Odf.Tests: 841/841.
- PulseOffice.Desktop.Tests, filter LedgerDataDialogsTests and LedgerAnalysisDialogsTests: 24/24.
- PulseOffice.Architecture.Tests: 43/43. PulseOffice.Localization.Tests: 32/32.

## Live checks
- Italian interface: filter menu on a column with 0.5 shows "0,5"; search "0,5" finds it.
- Search in the filter menu with blanks present: the count matches the rows shown after OK.
- Standard filter "is empty" / "is not empty": apply, reopen, the condition is shown.
- Dark theme: advanced filter preview readable; criteria with a formula count correctly.
- Save a date autofilter as XLSX and ODS, open in other applications, reapply the filter: same dates selected.
- Sort a range crossing an array formula: refused with a status message.
