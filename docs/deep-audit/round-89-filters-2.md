# Deep audit round 89: Ledger filtering, second pass

Continues [round 87](round-87-filters.md): closes its remaining items R1 and R2 and audits what it did not reach.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/Filtering.cs | full | R2: second condition and join |
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs | full | R1, R2 |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | full | R1 |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/CriteriaMatcher.cs | lines 1-120 | ok |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/NumericText.cs | full | ok (used for canonical values) |
| src/PulseOffice.Sheets.UI/DataDialogs/FilterMenuModel.cs | full | R1 |
| src/PulseOffice.Sheets.UI/FilterMenuView.cs | full | R2 popup |
| src/PulseOffice.Sheets.UI/LedgerFilterCommands.cs | full | R2 |
| src/PulseOffice.Sheets.UI/Scripting/FilterFacade.cs | full | ok |
| src/PulseOffice.Sheets.UI/Dialogs/StandardFilterField.cs | lines 1-80 | ok (already several rows with and/or) |
| src/PulseOffice.Sheets.UI/LedgerPartialCommands.cs | lines 185-273 | standard filter: see remaining |
| src/PulseOffice.Sheets.UI/LedgerDataDialogCommands.cs | lines 95-230 | D3 |
| src/PulseOffice.Sheets.UI/DataDialogs/AdvancedFilterView.cs | lines 160-200 | ok |
| src/PulseOffice.Sheets.UI/GridParts/GridViewport.cs | lines 110-150 | D4 |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/SummaryFunctions.cs | lines 40-110 | ok |
| src/PulseOffice.Sheets.Engine/Recalculation/CalculationEngine.cs | lines 1155-1180 | D4 |
| src/PulseOffice.Sheets.Engine/Operations/SheetSort.cs | lines 1-140 | ok |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs | full | R2 |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | lines 1-48, 105-260 | R2, D2 |

## Defects and fixes

- **R1 (medium) — canonical conditions**: values typed in the filter popup and in the standard filter dialog were stored
  as typed (interface decimal separator, local date) and read with the interface locale, so files carried
  locale-dependent numbers and `>1.5` from a file was not a number under an Italian interface. New
  `IFilterValueText`/`FilterValueText` (Sheets.Engine, interface locale by DI): comparison values are stored as
  invariant numbers, dates as serials; the popup shows them back in the interface locale (dates as dates on date
  columns). Text patterns (contains, begins, ends) stay as typed. AutoFilter conditions are evaluated with invariant
  numbers.
- **R2 (medium, import loss) — two conditions per column**: `ColumnFilter.SecondCriteria` and `Join` (`CriteriaJoin`).
  Engine combines both with and/or. XLSX writes two `customFilter` with `and` when joined by and, reads both; a lone
  non-blank or an `and`-joined non-blank still means "exclude blanks". ODS writes the pair inside `table:filter-or`
  when joined by or (plain conditions of the outer `filter-and` otherwise) and reads the parent back. The popup has a
  second condition row with an and/or selector (existing `filter.and`/`filter.or`/`filter.join` texts, five
  languages). Declared loss **XLSX231**: two conditions plus exclusion of blanks (Excel holds two conditions per column).
- **D2 (medium, interoperability) — ODS pattern conditions**: "contains", "does not contain", "begins with", "ends with"
  were written as operator `=` with literal asterisks, which other readers treat as plain text. They are now written
  with the ODF operators `contains`, `!contains`, `begins-with`, `ends-with` (and `!begins-with`, `!ends-with` read).
- **D3 (major, protection) — advanced filter in place** ignored the sheet's "use filter" protection; it now refuses with
  the same status message and remedy as the AutoFilter.
- **D4 (medium) — SUBTOTAL with standard and advanced filters**: rows hidden by those filters counted as manually hidden,
  so `SUBTOTAL(9;…)` still summed them. `SheetLayout.MarkFilteredRows`/`IsRowFiltered` record filter-hidden rows and
  `RowVisibilityAt` reports them as filtered.

## Remaining

- The standard filter dialog's result is transient (not an undo step, not stored in the model, not saved); the
  advanced filter in place likewise. Storing them needs a database-range filter model that holds arbitrary condition
  trees, as ODS does.
- Filters on named database ranges (round B6) round-trip but are not applied to rows on open.
- Advanced-filter computed criteria (a formula under a blank or non-matching header) are not supported.
- Sorting inside a filtered range sorts hidden rows too, then reapplies the filter (same as the reference suite).
- `filter.ByCondition` in macros takes one canonical condition; no macro entry point for the second condition yet.
- The filtered-row marks do not follow row insertion and deletion (same limit as the grid's filter bookkeeping).

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round89FilterTests.cs (canonical decimals, and/or, canonical text, dates,
  standard filter, SUBTOTAL)
- tests/PulseOffice.Sheets.UI.Tests/Round89FilterCodecTests.cs
- tests/PulseOffice.Formats.OpenXml.Tests/XlsxOdsParityTests.cs `A_filter_with_two_conditions_crosses_both_formats`,
  `Two_conditions_with_blank_exclusion_are_declared_in_xlsx`
- tests/PulseOffice.Desktop.Tests/LedgerDataDialogsTests.cs `The_filter_popup_composes_and_reloads_two_joined_conditions`

Runs: Sheets.Engine.Tests 1793/1793, Sheets.UI.Tests 463/463, Formats.OpenXml.Tests (parity, filter) 91/91,
Formats.Odf.Tests (filters) 3/3, Desktop.Tests (data dialogs) 23/23, Architecture.Tests 43/43.

Live checks: Italian interface, filter "greater than 1,5" and reopen the popup; save ODS and XLSX and reopen; two
conditions with "or"; date column condition; SUBTOTAL(9) under a standard filter; advanced filter on a protected sheet.
