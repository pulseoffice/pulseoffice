# Round 117: Ledger filters, second pass

Follows round 114 (filters part one). Every file below was read in full.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | 576/576 | clean |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | 82/82 | clean (re-read for the shift question) |
| src/PulseOffice.Sheets.Engine/Operations/StandardFilter.cs | 128/128 | minor notes |
| src/PulseOffice.Sheets.UI/Dialogs/StandardFilterField.cs | 95/95 | clean |
| src/PulseOffice.Sheets.Engine/Operations/SheetFilter.cs | 332/332 | minor notes |
| src/PulseOffice.Sheets.Engine/Operations/RangeFilterEvaluator.cs | 86/86 | clean |
| src/PulseOffice.Sheets.Engine/Operations/RangeMoveFilters.cs | 99/99 | clean |
| src/PulseOffice.Sheets.Engine/Operations/FilterValueText.cs | 44/44 | clean |
| src/PulseOffice.Sheets/FilterTreeColumns.cs | 68/68 | clean |
| src/PulseOffice.Sheets.UI/DataDialogs/AdvancedFilterView.cs | 207/207 | 1 minor fixed |
| src/PulseOffice.Sheets.UI/DataDialogs/FilterMenuModel.cs | 288/288 | minor notes |
| src/PulseOffice.Sheets.UI/FilterMenuView.cs | 383/383 | minor notes |
| src/PulseOffice.Sheets.UI/Scripting/FilterFacade.cs | 117/117 | clean |
| src/PulseOffice.Sheets.UI/LedgerDatabaseRangeCommands.cs | 132/132 | 1 medium fixed |
| src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs | 319/319 | 1 medium fixed |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxAutoFilterCodec.cs | 149/149 | 1 major, 1 medium fixed |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/XlsxRangeFilters.cs | 114/114 | clean |
| src/PulseOffice.Formats.OpenXml/Tables/XlsxTableFilters.cs | 45/45 | clean (wired to the fix) |
| src/PulseOffice.Sheets/ExternalData.cs (Shift) | 119-138 | clean (moves database ranges after FiltersPart) |

## Open item from round 114: cell shifts and filter areas

Not a defect. `FiltersPart` is not `WholeLinesOnly`, so `SheetStructure.ShiftBand` runs it for insert/delete with shift
down or right; `FiltersPart.Blocks` lists the autofilter, the anonymous range filter and the database ranges, so
`SheetCellShift.KeepsBlocksWhole` refuses a shift whose band would split one of them. A band that covers the whole
filter width moves the area, the column filters and the database range (moved by `ExternalDataPart` after
`FiltersPart` re-indexes its fields). Pinned by `Round117FilterShiftTests` (three tests).

## Defects

### Major
1. **XLSX date grouping read as an empty value list** (`XlsxAutoFilterCodec.cs:86-91`): spreadsheet applications store a
   date value filter as `<filters><dateGroupItem year month day dateTimeGrouping/></filters>`. Only `<filter val>` was
   read, so the column got an empty allowed set and every dated row was hidden on open. Fixed: the new
   `XlsxFilterDateGroups` resolves each `dateGroupItem` (year to second grouping) against the numeric cells of the
   column and adds the matching values; the sheet is passed to the codec for sheet and table autofilters.
   Test: `Round117FilterTests.Date_group_items_written_by_spreadsheet_applications_are_read_as_the_matching_values`.

### Medium
2. **Value filters became case-sensitive after a reload** (`XlsxAutoFilterCodec.cs:89`, `OdfDatabaseRangeMapper.cs:289,305,306`):
   the app builds allowed sets with `CurrentCultureIgnoreCase` (the menu also merges "Roma" and "ROMA" into one item),
   the readers used `Ordinal`, so after save and reopen the rows spelled differently were hidden. Fixed: readers use the
   same comparer. Test: `Value_filters_stay_case_insensitive_through_both_formats` (XLSX and ODS).
3. **Redefining a database range dropped its state** (`LedgerDatabaseRangeCommands.cs:81-87`): defining an existing
   name built a fresh `DatabaseRange`, losing filter buttons, column and range filters, the query binding, style and
   totals row. Fixed: the previous range is kept and only sheet, area and header change; filters are kept when the
   columns and header stay the same. Test: `LedgerRound6bAuditTests.Redefining_a_database_range_keeps_its_filters_and_binding`.

### Minor
4. **Advanced filter dialog ignored the initial output** (`AdvancedFilterView.cs:44,49`): reopened with a copy target, it
   showed "in place" and an empty output. Fixed.

### Minor, not fixed (noted)
- `FilterMenuView.cs:326`: non-date numbers are listed in invariant form ("0.5" with an Italian interface), not in the
  cell's format.
- `FilterMenuModel.ShownRows` drops blanks while searching but `Choice` still passes `Blanks`; the count can disagree
  with the result.
- `StandardFilter.Decompose` cannot show "equal to empty" (`=`) or "contains empty" rows in the dialog.
- `RangeFilterEvaluator.Duplicates` and `SheetFilterService` unique keys compare `CellValue.ToString()`, so number 1 and
  text "1" count as duplicates.
- `AdvancedFilterView` preview uses fixed white and black (a sheet look, also in dark theme), and the probe has no
  formula evaluator, so computed criteria count as rejecting in the preview only.
- XLSX writer: allowed date values are written as serial `<filter val>`; other applications compare `val` with the
  displayed text, so a reapply there loses the selection (the saved hidden rows still show the right view). ODS
  `filter-set-item` values have the same raw form.

## Round trip checked

Values, conditions (one and two, and/or), blanks exclusion, top/bottom N and percent, background and text colour,
standard (range filter tree), advanced with criteria range (`_xlnm.Criteria`), database ranges with buttons and totals:
covered by `XlsxOdsParityTests` and the existing ODS round-trip tests, all green; date grouping now read from XLSX.

## Tests run

- PulseOffice.Sheets.Engine.Tests, filter `Round117`: 3/3.
- PulseOffice.Formats.OpenXml.Tests: 2473/2473.
- PulseOffice.Sheets.UI.Tests: 482/482.
- PulseOffice.Formats.Odf.Tests: 834/834.

## Live checks

- Open an XLSX saved by another application with a date autofilter (tree of years and months): only the chosen dates
  visible.
- Filter "Roma" in a column holding "Roma" and "ROMA", save as ODS and XLSX, reopen: both rows visible.
- Data > Ranges > Define on an existing name with filter buttons: buttons and filters stay.
