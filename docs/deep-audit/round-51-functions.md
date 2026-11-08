# Deep audit round 51: Ledger lookup, aggregation, text, math, engineering, information

Continues [round 49](round-49-functions.md) and [round 50](round-50-calc-error-1900.md). Same method: about 120 formulas
with the documented Excel / OpenFormula result run through the real engine (`DeepAuditLedgerFunctionsRound51Tests`, with
a small data block in A1:D6: numbers, text, a blank, dates and an error cell), then reading of the code behind each mismatch.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/RoundingFunctions.cs | whole | correct (FLOOR/CEILING are the OpenFormula forms, `.XCL` the Excel ones, mapped in `OoxmlFunctionNames`) |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/NumberSystemFunctions.cs | 1-60 | DECIMAL and ROMAN error codes, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/TextFunctions.cs | 1-40 | UNICHAR error codes, UNICODE crash on a lone surrogate, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Numerics/UnitConversion.cs | 1-140 | correct (decimal and binary byte prefixes, case-sensitive as in Excel) |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ArrayFunctions.cs | MDETERM, MINVERSE | non-square error code, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/FunctionBuilder.cs | error helpers | correct |

Families checked by value with no defect: MATCH with wildcards and match types 1/-1, INDEX/MATCH, VLOOKUP with wildcard,
HLOOKUP approximate, OFFSET (and negative offset → `#REF!`), INDIRECT in R1C1 and with a sheet name (and an unknown sheet →
`#REF!`), INDIRECT(ADDRESS(…, sheet)), XLOOKUP binary search ascending/descending, SUMIFS/COUNTIFS/AVERAGEIFS with
operators, `""`, `"<>"`, `"="`, `"*"`, `"~*"` and date criteria, SUBTOTAL 2/3/9/109 and invalid code, AGGREGATE 4/9/12/14
with and without ignoring errors, SUMPRODUCT with booleans and `--`, NUMBERVALUE with separators and repeated `%`, UNICODE
and UNICHAR outside the BMP, TRIM, CLEAN, MROUND, FLOOR.XCL/CEILING.XCL/FLOOR.MATH/CEILING.PRECISE/FLOOR.PRECISE/ISO.CEILING,
BASE, DECIMAL base 36, ROMAN forms, ARABIC, GCD/LCM (zero, negative, above 2^53), MDETERM (singular gives 0), MINVERSE
singular → `#NUM!`, BIN2DEC/HEX2DEC/OCT2DEC negatives and invalid digits, DEC2BIN/DEC2HEX negatives and padding, COMPLEX
with `j`, IMSUM, IMPRODUCT, IMABS, IMCONJUGATE, CONVERT (mass, temperature, length, information prefixes, time,
incompatible families → `#N/A`), SHEET, SHEETS, CELL row/col/address/type, ISFORMULA, ISBLANK.

Performance: ten whole-column SUMIFS/COUNTIFS plus a whole-column FILTER over 20,000 rows evaluate well under the 10 s
bound (`WholeColumnConditionalSumsOverManyRowsStayLinear`): whole-column references are clipped to the used area.

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | TextFunctions.cs, UNICODE | A text starting with a lone surrogate (possible from a file or cell value) made `char.ConvertToUtf32` throw | Fixed: `Rune.TryGetRuneAt`, the code unit otherwise |
| Minor | TextFunctions.cs, UNICHAR | `UNICHAR(0)` gave an empty text and a surrogate code point `#VALUE!`; Excel documents `#VALUE!` for 0 and `#N/A` for partial surrogates | Fixed |
| Minor | NumberSystemFunctions.cs, DECIMAL | Invalid digit for the radix gave `#VALUE!`; Excel gives `#NUM!` | Fixed |
| Minor | NumberSystemFunctions.cs, ROMAN | Number above 3999 or negative gave `#NUM!`; Excel documents `#VALUE!` | Fixed |
| Minor | ArrayFunctions.cs, MDETERM/MINVERSE | Non-square matrix gave `#NUM!`; Excel documents `#VALUE!` | Fixed |

Two older expectations changed accordingly (`FunctionRegressionTests.CharEUnicharFuoriIntervalloDannoValore`,
`DeterministicEvaluationTests.CleanTogieAncheINonCaratteriUnicode`).

## Declared differences (not changed)

- `FLOOR`/`CEILING` typed in Ledger are the OpenFormula functions (`FLOOR(-2.5,-2)` = -4, `CEILING(-2.5,2)` = `#NUM!`); the
  Excel semantics are `FLOOR.XCL`/`CEILING.XCL`, and XLSX import/export maps the names.
- `ADDRESS` with a sheet name writes `'Sheet'.$C$2` (ODF separator); INDIRECT accepts both `.` and `!`.
- `LEN` counts code points (`LEN(UNICHAR(128512))` = 1), Excel counts UTF-16 units (2).
- `ARABIC` rejects a leading minus sign.
- `VALUE`, `NUMBERVALUE` without separators and `DOLLAR` follow the current culture, as in Excel.
