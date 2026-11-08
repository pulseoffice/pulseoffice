# Deep audit round 18: Ledger integration, regression, forecasting, conversion and round 17 leftovers

Area: `src/PulseOffice.Sheets.Engine`. Method: every file read in full with the Read tool, a failing test before each fix
(checked against the unmodified sources extracted with `git archive HEAD` into the scratchpad), fix at the root, no
commit.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Evaluation/Functions/Providers/IntegrationFunctions.cs | 279/279 | defects (FILTERXML, WEBSERVICE) fixed |
| Evaluation/Functions/Providers/MiscellaneousFunctions.cs | 175/175 | defects (NUMBERVALUE, DOLLAR) fixed |
| Evaluation/Functions/Providers/RegressionFunctions.cs | 216/216 | defect (SUMPRODUCT shape) fixed |
| Evaluation/Functions/Numerics/LeastSquares.cs | 108/108 | defects (collinearity, absolute tolerance) fixed |
| Evaluation/Functions/Numerics/LinearRegression.cs | 87/87 | defect (correlation overflow) fixed |
| Evaluation/Functions/Providers/ArrayFunctions.cs | 375–534 (regression part) | read with LeastSquares, clean |
| Evaluation/Functions/Providers/ForecastFunctions.cs | 130/130 | defect (seasonality limit) fixed |
| Evaluation/Functions/Numerics/ExponentialSmoothing.cs | 473/473 | defects (step overflow, scenario memory) fixed |
| Evaluation/Functions/Numerics/UnitConversion.cs | 278/278 | clean (see notes) |
| Evaluation/Functions/Providers/DatabaseFunctions.cs | 159/159 | defects (culture, calculated criteria, per-row parse) fixed |
| Evaluation/Functions/Providers/DateTimeFunctions.cs | 260–304 (workdays) | defects fixed |
| Evaluation/Functions/Providers/ExtendedFunctions.cs | 140–259 (workdays, euro) | defects fixed |
| Evaluation/Functions/Numerics/HolidayCalendar.cs | 18/18 | clean |
| Evaluation/Functions/Numerics/ComplexNotation.cs | 77/77 | defect (suffix lost) fixed |
| Evaluation/Functions/Providers/EngineeringFunctions.cs | 125–258 (complex) | defects fixed |
| Evaluation/Functions/FunctionProviderRegistration.cs | 106/106 | registration of the new services |
| Recalculation/ExternalAccess.cs | 16/16 | clean |
| ../PulseOffice.Sheets.UI/WebResourceAccess.cs (outside the area, read only) | 82/82 | see notes |

## Defects

### Grave

- **NUMBERVALUE with an empty separator threw** (`MiscellaneousFunctions.cs:39`, `string.Replace("")` raised an
  `ArgumentException` that no layer catches): `=NUMBERVALUE("1.5";"")` aborted the recalculation. Now `#VALUE!`; the
  separators use their first character as in Excel, a clashing explicit group separator and a group separator after the
  decimal one are `#VALUE!`, the empty text is 0, any whitespace is ignored and only finite values are accepted.
- **NETWORKDAYS/NETWORKDAYS.INTL up to 31/12/9999 gave `#NUM!`** (`DateTimeFunctions.cs:284`, `ExtendedFunctions.cs:195`:
  the loop computed `end.AddDays(1)`, which overflows), and both functions, with WORKDAY/WORKDAY.INTL, walked one day at
  a time. Restructured: `WorkdayArithmetic` (`IWorkdayArithmetic`, pure arithmetic over `DateOnly.DayNumber`: whole
  weeks, then the remainder, holidays deduplicated, only those on working days, counted by binary search) and
  `WorkdayCalculator` (argument reading, shared by the two providers, which no longer duplicate the logic). `WorkWeek`
  holds the Monday-first mask.
- **LINEST/LOGEST/TREND/GROWTH with collinear regressors gave `#NUM!`** (`LeastSquares.cs:62`). Excel drops the
  redundant columns (coefficient 0, standard error 0, degrees of freedom counting only the kept ones). Now a
  Gram–Schmidt pass (twice, relative tolerance 1e-10) keeps the independent regressors, the intercept first; a constant
  regressor with an intercept gives slope 0.
- **D functions: headers compared with the current culture** (`DatabaseFunctions.cs:116,155`): in Turkish `"id"` did not
  match `"ID"` (`#VALUE!`). Now ordinal, case-insensitive.
- **D functions: calculated criteria unsupported**: a criterion under a header that is not a field (or blank) holding a
  formula (`=B2>AVERAGE($B$2:$B$5)`) never matched. Now the formula is evaluated for each record, moved by the record's
  offset from the first one (`IEvaluationContext.EvaluateOffset`, implemented in the engine with `OffsetRewriter`, nesting
  capped at 64 and guarded by `TryEnsureSufficientExecutionStack`). Criteria are compiled once instead of re-parsing
  every condition for every record.

### Medium

- **FILTERXML returned only the first node** (`IntegrationFunctions.cs:253`); Excel returns a column of all matches. Now
  an array; a non-finite XPath number is `#VALUE!` and node text like `NaN` or `Infinity` stays text.
- **LINEST with tiny values was singular** (`LeastSquares.cs:62`, the tolerance was absolute against the largest entry,
  which is 1 because of the intercept column): `LINEST({2E-20|4E-20|6E-20};{1E-20|2E-20|3E-20})` gave `#NUM!`.
- **CORREL/PEARSON/RSQ overflowed** (`LinearRegression.cs:37`, `Sqrt(sumXx * sumYy)`): values around 1E+100 gave 0.
- **Complex arithmetic lost the `j` suffix and accepted mixed suffixes** (`EngineeringFunctions.cs:229–257`):
  `IMSUM("2j";"3j")` gave `5i`, `IMSUM("1+i";"1+j")` gave a value. `ComplexNotation.Parse` now returns `ComplexText`
  (value and suffix); mixed suffixes are `#VALUE!` in IMSUM, IMPRODUCT, IMSUB and IMDIV, the result keeps the operands'
  suffix. **IMDIV by zero** is now `#NUM!` as in Excel.
- **FORECAST.ETS far targets**: `(int)` of the step count overflowed and `steps % period` could become negative (index
  out of range in `Ahead`); now `long` steps, a target more than 1E+15 steps away is `#NUM!`. The seasonal prediction
  interval kept 1,000 scenarios for every step (up to 80 MB per cell); now only the two steps it interpolates.

### Minor

- **SUMPRODUCT accepted arrays of different shapes with the same size** (`RegressionFunctions.cs:110`): `{1|2}` against
  `{1;2}` gave 5; Excel gives `#VALUE!`.
- **WEBSERVICE**: URLs longer than 2,048 characters are refused before any request; answers longer than a cell are
  `#VALUE!` instead of being cut.
- **DOLLAR with more than 15 decimals** gave `#NUM!` (`Math.Round` throws past 15); now formatted, up to 127 decimals.
- **FORECAST.ETS seasonality above 8,760** gave `#VALUE!`; Excel gives `#NUM!`.
- The GETPIVOTDATA constraint regex was rebuilt at every call; now cached.

## Notes, no change

- FILTERXML parses with DTDs prohibited and no resolver: external entities are refused (pinned by a test). XPath itself
  has no time cap; the document is bounded by the cell text limit.
- `WebResourceAccess` (Sheets.UI, outside the area) is off by default, accepts only http/https, has a 10 s timeout and a
  2 MB cap; with the setting on it can reach local addresses. Declared, not changed.
- CONVERT: unit table and prefix handling read; no defect found. `FORECAST.ETS` seasonality detection is O(n·n/2).

## Still open

- Calculated criteria see the criteria cell's own substitutions only through the engine (inside MULTIPLE.OPERATIONS
  the offset evaluation ignores the substituted inputs).
- MINVERSE/MDETERM O(n³), user regular expressions on the backtracking engine, ETS seasonality detection O(n²).

## Tests

`tests/PulseOffice.Sheets.Engine.Tests/DeepAuditLedgerFunctionsRound18Tests.cs` (70 cases, each on a thread with a time
limit, exceptions reported). Against the unmodified sources 35 failed (one by an uncaught exception). After the fixes the
whole Sheets.Engine test project passes (1,393), Sheets.Tests (349) and Architecture (38) pass.
`DeepAuditLedgerCoreRound15Tests.cs` gained the new context member on its test context.

## Suggested live tests

- `=NUMBERVALUE("1.5";"")` gives `#VALUE!` without disturbing the sheet.
- `=NETWORKDAYS(1;2958465)` is immediate; `=WORKDAY(DATE(2024;1;1);10;DATE(2024;1;2))` is 16/01/2024.
- A DSUM with a criterion `=B2>AVERAGE($B$2:$B$5)` under a header that is not a field sums only the records above the
  average; in Turkish a field written `"id"` finds the `ID` column.
- `=FILTERXML("<a><b>1</b><b>2</b></a>";"//b")` spills two rows; `=IMSUM("2j";"3j")` is `5j`.
- `=TREND(...)` with a duplicated, proportional x column answers instead of `#NUM!`.
