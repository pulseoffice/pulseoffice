# Deep audit round 87: Ledger filtering

Area: AutoFilter, standard and advanced filters, value lists, conditions, top N, colour filters, the filter popup,
ODS and XLSX filter mapping.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs | full | D1, D2, D4 fixed |
| src/PulseOffice.Sheets/Filtering.cs | full | D3 fixed |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | full | inherits D2 fix |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/CriteriaMatcher.cs | full | ok (blank and wildcard rules) |
| src/PulseOffice.Sheets.UI/DataDialogs/FilterMenuModel.cs | full | D5 fixed |
| src/PulseOffice.Sheets.UI/FilterMenuView.cs | lines 1-110, 205-331 of 331 | D5 fixed |
| src/PulseOffice.Sheets.UI/Scripting/FilterFacade.cs | full | ok (protection checked, undo recorded) |
| src/PulseOffice.Sheets.UI/LedgerFilterCommands.cs | lines 60-180 | ok (protection, undo memento) |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs | lines 20-110 | D3 format side fixed |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | lines 100-260 | D3 format side fixed |

Not reached in this round: advanced-filter dialog UI, SUBTOTAL interaction beyond the existing tests, sorting inside
filtered ranges, dynamic arrays under a filter, database ranges of round B6.

## Defects

- **D1 (major) — SheetFilter.cs `Passes`**: an empty (non-null) allowed-values set was treated as "no filter", so a
  filter admitting no value, or only blanks, showed every non-empty row (round 85 finding).
- **D2 (medium) — SheetFilter.cs `Passes`**: blank cells returned `IncludeBlanks` before any condition was evaluated, so
  "contains", "<5" or "<>" (non-empty) filters, including standard-filter conditions, still showed blank rows. Blanks
  are now judged by the condition; value lists keep the blanks checkbox.
- **D3 (medium, loss) — Filtering.cs `IsEmpty`**: "all values but not blanks" (popup with only the blanks box unticked)
  was considered empty and dropped by `AutoFilterState.Set`, so the filter vanished. It is now a filter; ODS writes it
  as `!empty` and reads a lone `!empty` back as such; XLSX writes `customFilter notEqual " "` (alone or `and` with the
  condition) and reads it back. Both directions cross.
- **D4 (minor, performance) — SheetFilter.cs `RowsToHide`**: the condition was parsed once per row; it is now parsed
  once per filter. Duplicate filters no longer risk a duplicate-key exception.
- **D5 (major, responsiveness) — FilterMenuView/FilterMenuModel**: the popup created one check box per distinct value
  and recomputed (and re-folded) the visible list several times per rebuild; a million distinct values froze the UI.
  The list now shows at most `FilterMenuModel.ListedLimit` (1,000) items with a translated note
  (`filter.moreValues`, five languages) asking to narrow the search; the visible list is cached per search and the
  search text is folded once; date groups expand on search only within the limit.

## Remaining

- **R1 (medium)**: a condition typed in the popup is stored as typed, with the interface's decimal separator, and parsed
  with the locale's `NumericText`; files therefore carry locale-dependent numbers (decision 17 wants the canonical form).
- **R2 (medium, import loss)**: XLSX `customFilters` with two conditions (e.g. "between") keep only the first; the model
  has a single condition per column. Needs a second condition and join in `ColumnFilter`, ODS `filter-or`/`filter-and`
  nesting and UI.

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round87FilterTests.cs (7 of 9 failed before the fix)
- tests/PulseOffice.Sheets.UI.Tests/Round87FilterMenuTests.cs
- tests/PulseOffice.Formats.OpenXml.Tests/XlsxOdsParityTests.cs `A_filter_that_excludes_blanks_crosses_both_formats`

Runs: Sheets.Engine.Tests 1777/1777, Sheets.UI.Tests 460/460, Formats.OpenXml.Tests (parity, filter) 1062/1062,
Formats.Odf.Tests (filters) 13/13, Desktop.Tests (filter dialogs) 22/22.

Live checks: open the filter popup on a column with many distinct values; untick only "(blanks)"; filter "contains" on a
column with blanks; save in ODS and XLSX and reopen.
