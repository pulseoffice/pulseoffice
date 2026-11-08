# Round 122: Ledger sort leftovers

Follows round 120 (docs/deep-audit/round-120-ledger-filter-ui.md), which left five sort items open. The branch was
fast-forwarded to `work` before starting. Each defect got a test first; tests that only needed existing API were run
red before the fix (`A_large_sort_is_one_compact_undo_step`, and the locale and memory tests against empty stubs).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetSort.cs | 243/243 | 2 fixed (undo per cell, thread culture) |
| src/PulseOffice.UI.Controls/Sorting/SortLevelsView.cs | 281/281 | clean of defects, minor notes |
| src/PulseOffice.UI.Controls/Sorting/SortLevelsModel.cs | 115/115 | clean |
| src/PulseOffice.Sheets/DatabaseRanges.cs | 79/79 | sort descriptor added |
| src/PulseOffice.Sheets/Filtering.cs (AutoFilterState) | 38-91 | sort descriptor added |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | 82/82 | sort follows column edits |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 324/324 | `table:sort` written and read |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxRangeFilters.cs | 114/114 | clean |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | 45/45 | `sortState` read |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableWriter.cs | 167/167 | table `sortState` written |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTables.cs | 1-80 | table `sortState` read |
| src/PulseOffice.Sheets.UI/LedgerToolCommands.cs (SortAsync) | 1-130 | 1 fixed (locale), dialog prefilled |
| src/PulseOffice.Sheets.UI/LedgerEditDataCommands.cs | 1-130 | 1 fixed (quick sort) |
| src/PulseOffice.Sheets.UI/SheetSortGate.cs | 38/38 | clean |
| src/PulseOffice.Sheets.UI/DataDialogs/CustomSortOrders.cs | 33/33 | locale wrapper added |
| src/PulseOffice.Sheets.UI/DataDialogs/LedgerSortServices.cs | 39/39 | extended |
| src/PulseOffice.Sheets.UI/LedgerFilterCommands.cs | 1-60, 120-200 | clean |

## Defects fixed

1. **Sort settings were saved in neither format** (medium; model had no descriptor). New `SortSettings` / `SortField`
   (`PulseOffice.Sheets/SortSettings.cs`) on `AutoFilterState.Sort` (also in `AutoFilterMemento`) and
   `DatabaseRange.Sort`. `ISortSettingsMemory` (engine) records them after a sort, in the same undo step, on the
   AutoFilter or database range that owns the sorted block, and recalls them to prefill the sort dialog. Offsets follow
   column insertion and deletion (`FiltersPart`). ODS: `table:sort` / `table:sort-by` (field-number, order,
   `table:case-sensitive`) inside `table:database-range`, the anonymous one for the sheet AutoFilter; custom orders in
   our attribute `pulseoffice-sort:custom-order` (`urn:pulseoffice:sort`, items separated by line feeds).
   XLSX: `sortState` inside the sheet `autoFilter`, `sortState` in the table part for tables; read from the
   AutoFilter, the worksheet or the table; `customList` carries custom orders. Loss declared: `XLSX236` (a custom
   order item containing a comma is split by Excel's comma-separated list). Tests: `Round122SortStateTests`.
2. **Custom sort orders used the thread culture** (`LedgerToolCommands.cs:76`, minor, decision 17): now
   `ILocaleSortOrders` over `ISpreadsheetLocale`. **Text comparison in `SheetSorter`** also used
   `CurrentCulture`; it now takes `ISpreadsheetLocale` (DI) and compares with its `CompareInfo`.
   Tests: `Text_is_compared_in_the_spreadsheet_locale_not_the_thread_culture`, `Custom_sort_orders_come_from_the_spreadsheet_locale`.
3. **Quick ascending/descending sort** (`LedgerEditDataCommands.cs:84`, medium): a single cell sorted nothing (fewer
   than two rows), the key was always the first column, the header was sorted with the data and the AutoFilter was not
   reapplied. `IQuickSortPlanner` now uses the AutoFilter range (with its header) or the database range under the
   cursor, else extends a single cell to the current region with header detection; the key is the cursor's column;
   after sorting an AutoFilter range the filter is reapplied. Tests: `Round122QuickSortTests`.
4. **One undo action per cell** (`SheetSort.cs:97`, medium, memory): now one action holding the before and after row
   arrays (plus the comments action when there are comments). Test: `A_large_sort_is_one_compact_undo_step`.

## SortLevelsView notes (no defect fixed)

- Alt+Insert adds a level: Mac keyboards have no Insert key, so on macOS adding has only the button.
- A duplicate column only warns; confirming is still allowed (the duplicate key is harmless).
- A remembered custom order that matches none of the offered orders is shown as "no custom order".
- Fixed widths (72, 160, 180, 620/760) instead of metrics.

## Open

- A worksheet-level XLSX `sortState` on a plain range (no AutoFilter, no table) and left-to-right sorts (`columnSort`)
  are not read; a sort on a plain range is not remembered (no owner to keep it on).
- `SortLists.ContainingAll` (AutoFill lists) still compares with the current culture.

## Tests run

- All green: Sheets.Engine.Tests 1848+, Sheets.UI.Tests 488, Formats.OpenXml.Tests 2484, Formats.Odf.Tests 845,
  Localization.Tests 32, Architecture.Tests 43.

## To verify live

- Sort dialog on an AutoFilter range, save as ODS and XLSX, reopen: the dialog opens with the same levels.
- Quick sort with the cursor in one cell of a table with a header and an active filter: header stays, filter reapplied.
- Undo of a large sort; open a saved XLSX with a sorted AutoFilter in Excel (Data, Sort shows the levels).
