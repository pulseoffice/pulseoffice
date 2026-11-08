# Deep audit round 102: Ledger conditional formatting and data validation

## Coverage (read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/ConditionalFormattingEvaluator.cs | 495/495 | defect 1 |
| src/PulseOffice.Sheets.Engine/Operations/DataValidator.cs | 156/156 | defect 2 |
| src/PulseOffice.Sheets.Engine/Operations/ConditionalRangeSnapshots.cs | 71/71 | defect 1 (no deviation) |
| src/PulseOffice.Sheets/Formatting/ConditionalRule.cs | 200/200 | defect 1 (no field) |
| src/PulseOffice.Formats.OpenXml/Conditional/RangeRuleCodecs.cs | 128/128 | defect 1 (`stdDev` dropped) |
| src/PulseOffice.Formats.Odf/OdsFormat.cs | 100-209 | fidelity checks reviewed |

Not reached in this round: the XLSX x14 data bar and icon codecs, the ODS condition forms beyond the keyword table,
the rules manager and validation dialogs, the validation codecs, the grid painter.

## Defects

1. **Medium** - above/below average rules with standard deviations: XLSX `cfRule@stdDev` was ignored on read and
   never written, so "1 std dev above average" silently became "above average" (a silent loss, rule 7).
   Fixed: `ConditionalRule.StandardDeviations`, sample standard deviation in `ConditionalRangeSnapshot`, the bound
   `average +/- n * deviation` in `AverageTest`, `stdDev` read (clamped 0..3) and written in `XlsxAverageCodec`.
   ODF has no such option: declared as `ODF102` in `OdsSpreadsheetWriter.CheckFidelity`.
2. **Medium** - text length validation of a number counted zero characters (`CellValue.AsText()` is empty for
   numbers), so 12345 passed "length <= 3". Fixed in `DataValidator` by measuring the shown value.

## Remaining

- The rule editor (`ConditionalRuleEditorView`) and the macro facade (`FormatRuleFacade`) do not yet offer the number
  of standard deviations; rules read from XLSX keep and evaluate it.

## Tests

- `RangeConditionEvaluationTests.LaMediaConDeviazioniStandardSpostaLaSoglia`
- `DataValidationTests`: text length case extended with numbers
- `RangeConditionRoundTripTests.LeDeviazioniStandardFannoIlGiroInXlsxESonoDichiarateInOds`

Sheets.Engine.Tests (filtered, 15) and Formats.OpenXml.Tests (conditional, parity and round trip filter, 1123) pass.
