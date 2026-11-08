# Deep audit round 49: Ledger calculation functions

Method: a table of 113 formulas with documented Excel / OpenFormula results (function reference examples) evaluated
through the real engine (`DeepAuditLedgerFunctionsRound49Tests`), then reading of the code behind each mismatch.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/ArrayFunctions.cs | UNIQUE, SEQUENCE, RANDARRAY, TEXTSPLIT, helpers | TEXTSPLIT defect, fixed |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Providers/DateTimeFunctions.cs | EDATE, EOMONTH, helpers | correct (overflow already gives an error) |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/Numerics/DateSystem.cs | 1-31 | epoch 1899-12-30, see limits |
| src/PulseOffice.Sheets.Engine/Evaluation/ArrayParameterClasses.cs | whole | correct |
| src/PulseOffice.Sheets.Engine/Evaluation/Functions/ArgumentReader.cs | public surface | correct |

Families checked by value with no defect: DATEDIF (Y, M, YM, YD, MD), EDATE/EOMONTH at month ends, NETWORKDAYS.INTL and
WORKDAY.INTL (weekend strings and codes, reversed range), XNPV/XIRR (and XIRR without sign change → #NUM!), HYPGEOM.DIST,
NEGBINOM.DIST, BETA.INV, GAMMA.INV, T.INV.2T, F.DIST, CHISQ.INV.RT, BINOM.INV, PRICE, YIELD, ACCRINT, ODDFPRICE,
ODDLPRICE, ODDLYIELD, COUPDAYS, COUPPCD, DAYS360, YEARFRAC bases 0/1, TAKE/DROP/CHOOSECOLS/VSTACK padding/WRAPROWS
padding, LET, LAMBDA with MAP/REDUCE/SCAN/BYROW/BYCOL/MAKEARRAY, XMATCH/XLOOKUP match and search modes, UNIQUE,
SORT/SORTBY, FILTER, SEQUENCE, FIXED, TEXT, TEXTBEFORE/TEXTAFTER, coercion of booleans and text in SUM/AVERAGE/COUNT/+,
ROUND half away from zero (including 1.005), MOD/QUOTIENT signs, ISOWEEKNUM/WEEKNUM 21, MROUND, CEILING.MATH.

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | ArrayFunctions.cs, TEXTSPLIT | Only 4 arguments accepted and `ignore_empty` ignored; array delimiters (`{",",", "}`) not supported (wrong split); no `match_mode`; jagged rows not padded with `#N/A` / `pad_with` | Fixed: 2-6 arguments, `DelimitedTextSplitter` (leftmost, longest delimiter), case-insensitive mode, padding |

## Declared limits (not changed)

- Serial dates follow the 1899-12-30 epoch without the 1900 leap-year bug (as the ODF reference suite): serials 1-60
  differ by one day from Excel (`DATE(1900,2,29)` is 61, `WEEKDAY(DATE(1900,1,1))` is 2). XLSX 1904 workbooks are shifted
  on import (`Xlsx1904DateShift`).
- There is no `#CALC!` error: empty results of FILTER/UNIQUE give `#NULL!`, `TAKE(...,0)` `#N/A`, `SEQUENCE(0)` `#VALUE!`.
  Adding the error type touches the model, both formats and the UI; left for a dedicated round.

Both declared limits above were closed in [round 50](round-50-calc-error-1900.md).
