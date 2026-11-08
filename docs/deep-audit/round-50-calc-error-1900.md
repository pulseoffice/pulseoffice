# Deep audit round 50: #CALC! error and the 1900 leap-year bug in XLSX

Follow-up of the two declared limits of [round 49](round-49-functions.md).

## 1. The #CALC! error

`CellError.Calc` (text `#CALC!`) joins the model. Where Excel returns it, the engine now returns it:

| Case | Before | Now |
|---|---|---|
| `FILTER` with no kept row and no `if_empty` | `#NULL!` | `#CALC!` |
| `UNIQUE` with nothing left (`exactly_once`) | `#NULL!` | `#CALC!` |
| `TAKE(a;0)`, `DROP` of all rows or columns (either sign) | `#N/A` / `#NUM!` | `#CALC!` |
| `SEQUENCE` / `RANDARRAY` with 0 rows or columns | `#VALUE!` | `#CALC!` (negative stays `#VALUE!`) |
| `TOCOL` / `TOROW` that ignore every value | empty array | `#CALC!` |
| A lambda left uncalled (`=LAMBDA(x;x)`, `=LET(f;LAMBDA(..);f)`) | empty | `#CALC!` (scalar coercion of a `LambdaValue`) |
| `MAP`, `SCAN`, `BYROW`, `BYCOL`, `MAKEARRAY` whose lambda returns a multi-cell array or range (nested arrays) | first element | `#CALC!` for that element (`HigherOrderForm.Element`) |

- `ERROR.TYPE` gives 14 for `#CALC!` and 9 for `#SPILL!` (the latter was `#N/A`); `ERRORTYPE` (ODF) keeps `#N/A` for
  both, as ODF has no code for them. `ISERROR`, `ISERR`, `IFERROR` treat it as any non-`#N/A` error; `ISNA`/`IFNA` do not.
- The formula lexer and parser accept the literals `#CALC!` and `#SPILL!`.
- XLSX writes `t="e"` with `#CALC!` and reads it back; `#SPILL!` was missing from the XLSX error table (read back as
  `#VALUE!`) and is added too.
- ODS: ODF defines neither error. The cell is written as the reference suite writes errors (`calcext:value-type="error"`,
  text `#CALC!`, constant errors as `of:=#CALC!`); our reader maps the text back. Other ODF readers show a generic error:
  declared as **ODF271** (info) in `OdsSpreadsheetWriter.CheckFidelity`, five languages.
- Error names are not localized in the interface (display is always the Excel text), and the function wizard has no per-error
  texts, so no further UI strings were needed.
- Tests that encoded the old results were updated: `ErrorTypeCodeTests` (`DROP` beyond the size, `TAKE(..;0)`) and
  `LambdaTests.UnaLambdaSenzaChiamataDaCalc`.

## 2. The 1900 leap-year bug in XLSX

Our serials follow the 1899-12-30 epoch (as ODF); Excel's 1900 system counts a fictitious 29 February 1900 (serial 60), so
its serials 1-59 (1 January - 28 February 1900) are one less than ours. Restructured:

- `XlsxCalendarCells` (new): finds the constant numeric cells whose number format has a calendar part (the logic that
  lived inside `Xlsx1904DateShift`, now shared and per workbook).
- `Xlsx1904DateShift` uses it.
- `XlsxLeapYearSerials` (new): `FromExcel` (Excel `[1;60)` → +1), `ToExcel` (ours `[2;61)` → −1), `ShiftToStandardSystem`
  run by `XlsxReader` after the 1904 shift, and `PrecedesExcelCalendar`.
- `XlsxWriteContext.ExcelValue` converts constant date cells on write.
- **Choice for Excel serial 60** (the fictitious 29 February 1900): read as 28 February 1900 (ours 60), declared on read as
  **XLSX191** (info). Written back it becomes Excel 59; the fictitious day cannot survive.
- Dates before 1 January 1900 (ours `< 0` or `[1;2)`) have no Excel serial: declared as **XLSX192** (warning) in
  `XlsxSpreadsheetWriter.CheckFidelity`. Values in `[0;1)` are left alone both ways: they are times, also when the format
  shows a date.
- Not converted, by design: formula results (both programs recalculate with their own calendar, so `DATE(1900;1;15)` is the
  same day in each) and numeric literals in formulas (`=A1+15` is arithmetic, not a date). Time-only formats and General
  are never shifted. ODS is unaffected.

## Tests

- `PulseOffice.Sheets.Engine.Tests/CalcErrorTests.cs` (new): every case above, `ERROR.TYPE`, `ISERROR`/`ISERR`/`ISNA`/`IFERROR`/`IFNA`.
- `PulseOffice.Formats.OpenXml.Tests/XlsxCalcErrorAndLeapYearTests.cs` (new): `#CALC!`/`#SPILL!` XLSX round trip and raw
  `t="e"`, XLSX ↔ ODS ↔ XLSX chain, ODF271; serials written (2→1, 16→15, 60→59, 61→61) and read (1→2, 15.25→16.25, 59→60,
  61, 0.5), fictitious day with XLSX191, numbers and times not shifted, early date through XLSX → ODS → XLSX, XLSX192.
- `PulseOffice.Formats.Odf.Tests/OdsCalcErrorTests.cs` (new): ODS cell markup and read back, ODF271.

## Files

- `src/PulseOffice.Sheets/Values/CellValue.cs`
- `src/PulseOffice.Sheets.Engine/Parsing/Lexer.cs`, `Parsing/Parser.cs`
- `src/PulseOffice.Sheets.Engine/Evaluation/Functions/ValueCoercion.cs`
- `src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ArrayFunctions.cs`, `InformationFunctions.cs`
- `src/PulseOffice.Sheets.Engine/Evaluation/Functions/SpecialForms/HigherOrderForms.cs`
- `src/PulseOffice.Formats.OpenXml/Spreadsheet/SheetSections.cs`, `Xlsx1904DateShift.cs`, `XlsxCalendarCells.cs` (new),
  `XlsxLeapYearSerials.cs` (new); `XlsxReader.cs`, `XlsxFormat.cs`
- `src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs`, `OdsFormat.cs`
- `src/PulseOffice.Localization/Resources/{it,en,de,es,fr}.json` (ODF271, XLSX191, XLSX192)
