# Round 270: date epoch reach and pivot dialog leftovers

Area: the round 266 (pivot dialog) and round 267 (date epoch) queue items. Not built or run here (the coordinator
builds and tests at the join).

## Defects fixed

1. **Medium (data): XLSX date filters of 1904 workbooks named the wrong days.** `FilterColumnDates`,
   `XlsxFilterDateGroups` and `XlsxDynamicFilterCodec` converted serials with the 1900 system, so a `dateGroupItem`
   written for a 1904 workbook pointed four years earlier in Excel, and dynamic filter bounds (`val`, `maxVal`) were
   1900 serials. They now use the workbook's epoch (`CalculationSettings.TryDateOf`, `DateOf`, `SerialOf`), and dynamic
   bounds are written in the file's system (the 1900 system for epochs Excel does not know).
2. **Medium (data): the XLSX reader set the epoch only after the sheets**, so autofilter date groups and month
   dynamic filters were matched against the 1900 system while reading. The epoch is now set before the sheet loop.
3. **Medium: relative date filters ignored the epoch** (`RelativeDateBounds.Contains` now takes the calculation
   settings; `SheetFilter` passes the sheet's workbook); ODF relative period bounds are written in the workbook's
   system; `FilterValueText` displays filter dates with the ambient epoch.
4. **Medium: series fill by months and weekdays assumed 1899-12-30** (month steps converted with OADate, weekdays
   took serial 0 for a Saturday). Both now count from the sheet's workbook epoch; the series preview too.
5. **Medium (data): query results and external pivot caches** wrote dates as 1900 serials into 1904 or other-epoch
   workbooks (`QueryValueConverter` now converts with the workbook's calculation settings; `QueryRefresher` and
   `PivotSourceRefresher` pass them; XLSX cached `DateTimeItem`s of external sources read with the workbook's epoch).
6. **Medium (data): XLSX pivot caches of local sources** wrote `DateTimeItem`, `minDate`, `maxDate` and the date
   group `startDate`/`endDate` with the 1900 system, and read date shared item keys back the same way: in 1904
   workbooks Excel saw dates four years off. `XlsxPivotMapper` and `XlsxPivotGroupWriter` use the workbook's epoch.
7. **Minor: duplicate removal without an injected presenter and CSV export** formatted dates with the fixed epoch.
8. **Medium (UX): the pivot field settings panel and the dialog preview showed invariant keys** (`2024-T1`,
   `2024-01`, `TRUE`). `IPivotPreviewer.ItemLabel` renders items through the public `PivotItemCells`, with the labels
   language; the preview renders row and column items the same way.
9. **Medium: calculated value fields were not editable in the dialog and lost their position** (always appended
   after the placed value fields). `PivotLayoutModel` gives each calculated value field a slot id after the source
   columns, kept in the Value zone at its original order; the settings panel edits its name, formula, show-as mode and
   number format; its chip offers only removal (it cannot move to rows, columns or filters).

Tests: `tests/PulseOffice.Formats.OpenXml.Tests/Round270DateEpochTests.cs`,
`tests/PulseOffice.Sheets.Tests/Round270DateEpochTests.cs`, `tests/PulseOffice.Sheets.Queries.Tests/Round270QueryEpochTests.cs`,
`tests/PulseOffice.Sheets.UI.Tests/Round270PivotDialogTests.cs` (the dialog built over a real source, settings panel
inspected through the logical tree).

## Not covered (queued)

- CSV import into a workbook with another epoch (`CsvValues` keeps the 1900 system); pivot date grouping and display
  outside evaluation still follow the active workbook's epoch.
- Calculated field names and formulas are not validated in the settings panel (an invalid formula shows error values
  in the preview); the dialog still offers no base item (next, named) or the round 267 show-as modes beyond the list.
- Nothing opened in Excel or the reference suite.
