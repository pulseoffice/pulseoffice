# Deep audit round 82: Ledger spreadsheet formats

Scope: XLSX and ODS readers and writers after the recent additions (#CALC!, 1900 serials, external pivots, table
totals, range filters, shape text lists, number schemes, REGEX* functions, locale changes).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.OpenXml/XlsxReader.cs | 240/240 | 1904 shift runs before the leap-year shift (correct order); no defect |
| src/PulseOffice.Formats.OpenXml/XlsxWriter.cs | 193/193 | worksheet and workbook child order match CT_Worksheet / CT_Workbook; no defect |
| src/PulseOffice.Formats.OpenXml/Spreadsheet/SheetSections.cs | 801/801 | defect D1 (below), fixed |
| src/PulseOffice.Formats.OpenXml/XlsxLeapYearSerials.cs | 45/45 | read/write inverse; fictitious 29 February declared (XLSX191), pre-calendar dates declared (XLSX192) |
| src/PulseOffice.Formats.OpenXml/XlsxFormat.cs | 351/351 (CheckFidelity and writer) | declarations checked against the writers: XLSX024 (crop/filter of sheet pictures) still true, the sheet image mapper writes no srcRect; no stale declaration found |
| tests/PulseOffice.Formats.OpenXml.Tests/XlsxCalcErrorAndLeapYearTests.cs | 175/175 | #CALC!/#SPILL! and early dates covered in both formats and XLSX→ODS→XLSX |

Not read in full in this round (left for the next one): OdsReader, OdsWriter, SheetPartReaders, SheetWriting, the pivot,
table and filter mappers.

## Defects

- **D1 (medium) — `SheetSections.cs`, `SheetDataSection.Cell`**: a constant error cell was always written with a
  formula equal to its error text. For `#CALC!` and `#SPILL!` this produced `<f>#CALC!</f>` / `<f>#SPILL!</f>`, which are
  not error literals of Excel's formula grammar (only `#NULL!`, `#DIV/0!`, `#VALUE!`, `#REF!`, `#NAME?`, `#NUM!`, `#N/A`):
  Excel reports the file as needing repair. Fixed at the root: `XlsxCellCodec.IsFormulaLiteral` says which errors may be
  written as a formula; the others are written as a plain `t="e"` value, which reads back to the same constant error.
  Test: `XlsxCalcErrorAndLeapYearTests.Constant_errors_outside_the_excel_formula_grammar_carry_no_formula` (failed
  before the fix).

## Performance (1,000,000 cells, 50,000 x 20, one quarter text)

`LargeWorkbookRoundTripTests` on this Mac (Debug build): XLSX write 2.5 s, read 4.0 s (4.2 MB); ODS write 2.5 s, read
1.8 s (1.9 MB); managed heap about 440 MB, working set about 565 MB with three copies of the workbook alive. The test
guards the whole round trip under two minutes and checks every cell arrives.
