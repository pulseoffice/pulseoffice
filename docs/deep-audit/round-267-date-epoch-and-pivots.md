# Round 267: workbook date epoch and pivot leftovers

Area: the queued date epoch item (ODS null date, XLSX 1904) and the round 264 pivot items. Not built or run here (the
coordinator builds and tests at the join). The pivot dialog was not touched (round 266 works on it in parallel).

## Defects fixed

1. **Serious (data): an ODS `table:null-date` other than 1899-12-30 was ignored**, so every date of such a file was
   read four years (or two days) off and written back shifted. `CalculationSettings.DateEpoch` now holds the epoch;
   `OdfCalculationSettingsMapper` reads and writes `table:null-date`, and the ODS reader takes it from the
   calculation settings before the first table row is streamed, so `office:date-value` becomes a serial in the
   workbook's own system; the writer turns serials back into dates with the same epoch.
2. **Serious (data): XLSX 1904 workbooks shifted only date-formatted constants** to the 1900 system on load, so plain
   numbers used as dates, cached formula results and formulas like `=A1-1462` gave other results, and the file was
   saved in the 1900 system. The load shift is gone (`Xlsx1904DateShift` became `XlsxDateEpochs`, and the XLSX057 info
   with it): `workbookPr/@date1904` sets the epoch, is written back, and the 1900 leap-day correction applies only to
   1900 workbooks.
3. **Medium: an epoch Excel does not know** (ODS null dates other than 1899-12-30 and 1904-01-01) is written to XLSX in
   the 1900 system: date-formatted constants are moved, and the loss is declared (XLSX245, five languages). The early
   dates warning (XLSX192) is judged in the epoch the file will use.
4. **Medium: evaluation and display ignored the epoch.** An injectable ambient `IDateEpoch` (`AmbientDateEpoch`,
   AsyncLocal inside the instance) is entered by the `Evaluator` with the workbook's epoch for every evaluation;
   `StandardDateSystem` (all date functions, TEXT through `FormatCodeRenderer`, conditional date periods), `NumericText`
   (text to date coercion, DATEVALUE), `CellInputRecognizer`, `CellEditText`, the Ledger number formatter, pivot date
   grouping and chart text read it. Outside evaluation the default is the active workbook's epoch
   (`ActiveWorkbookDateEpoch`, Sheets.UI). Insert Current Date counts from the workbook's epoch.
5. **Minor: a text item "TRUE", "FALSE" or "5" shared the key of the boolean or number** and rendered as one. Text that
   reads as another kind gets a word-joiner prefix in its key (`PivotValues.TextKeyOf`, `NameOf`); rendering, ODS
   hidden members and base items strip it; XLSX string shared items are keyed the same way. Headers are not keyed.
6. **Minor: subtotal and column labels of boolean and number items showed the invariant key.** `PivotItemCells.Display`
   renders booleans with the labels language's names and numbers in its culture.
7. **Minor: GETPIVOTDATA did not accept the rendered labels.** Items are matched to keys per field: exact key first,
   then the item name without the text marker, then the rendered label (localized quarters, months, days, booleans).
8. **Medium: base items other than "previous" were not modelled.** `PivotField.BaseItem` (`PivotBaseItem`: previous,
   next, named key) drives difference and % difference in the engine (a row that is the named base item shows empty);
   XLSX `baseItem` (1048828 previous, 1048829 next, otherwise the item index) and ODS
   `data-pilot-field-reference/@member-type` and `@member-name` are written and read.
9. **Medium: Excel's percentOfParentCol read as % of parent row.** `ValueDisplayMode.PercentOfParentColumn` divides by
   the parent column total (the row's total for the outermost column level); XLSX x14 `percentOfParentCol`, ODS our
   `pulsepivot:display="parent-column-percentage"`.

Tests: `tests/PulseOffice.Sheets.Engine.Tests/Round267DateEpochTests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/Round267DateEpochTests.cs`, `Xlsx1904DateSystemTests.cs` (rewritten),
`tests/PulseOffice.Formats.Odf.Tests/Round267NullDateTests.cs`, `tests/PulseOffice.Sheets.Pivot.Tests/Round267PivotTests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/Round267XlsxPivotTests.cs`, `tests/PulseOffice.Formats.Odf.Tests/Round267OdsPivotTests.cs`.

## Research

ODF 1.3 part 3, 9.4.2 `table:null-date` (child of `table:calculation-settings`, default 1899-12-30) and ECMA-376
part 1, 18.2.28 `workbookPr/@date1904`; Excel has only the two systems, so other epochs cannot be written natively.
`pivotShowAs` values of the x14 data field extension (MS-XLSX 2.6.x) include `percentOfParentCol`; `dataField/@baseItem`
uses 1048828 and 1048829 for previous and next.

## Not covered (queued)

- Other date consumers still on the fixed epoch: autofilter date groups and dynamic filters, series fill by months,
  sheet data tools, query values, cached dates of external pivot sources, ODF relative periods.
- Display outside evaluation follows the active workbook only.
- ODS item names cannot tell a text "5" from the number 5; the pivot dialog does not expose the new modes and base
  items; nothing opened in Excel or the reference suite.
