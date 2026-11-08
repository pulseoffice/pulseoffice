# Deep audit round 17: Ledger's function providers not re-read in round 16

Round 16 (`ledger-functions-round16.md`) covered lookup, text, financial solvers, dates infrastructure and array
functions. This round read statistics, distributions and their inverses, date and time, mathematics and trigonometry,
engineering, information and database providers, plus the XLOOKUP/XMATCH minors left open by round 16. Edge cases
were compared with Excel; where the project deliberately follows the reference suite (and a test pins it) the
divergence is recorded, not changed.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Providers/ModernLookupFunctions.cs | 114/114 | 2-D lookup array, short return array, binary modes, wildcard + binary (minor/medium) — fixed |
| Providers/RoundingFunctions.cs | 121/121 | huge `digits` overflowed `Math.Abs(int.MinValue)` / gave NaN (medium) — fixed; CEILING/FLOOR(.XCL/.MATH/.PRECISE) signs checked by hand: correct |
| Providers/CombinatoricsFunctions.cs | 40/40 | loops proportional to the argument (blocking) — fixed in `SpreadsheetMath` |
| Providers/DateTimeFunctions.cs | 304/304 | no new defects; divergences below |
| Numerics/DateSystem.cs | 50/50 | no defects; no 1904 system in the engine (the XLSX reader shifts serials, `Xlsx1904DateShift`) |
| Numerics/SpreadsheetMath.cs | 392/392 | `Factorial`, `DoubleFactorial`, `Combinations` ran to the end after overflowing (blocking) — fixed |
| Numerics/HolidayCalendar.cs | 18/18 | no defects (hash set, linear in the holidays) |
| Providers/DistributionFunctions.cs | 407/407 | HYPGEOM.DIST cumulative loop and overflow, NEGBINOMDIST overflow, FISHERINV overflow — fixed |
| Numerics/DistributionInverse.cs | 74/74 | bracketing and bisection bounded (≤ 1100 expansions, 2000 halvings); no defects |
| Numerics/BinomialDistribution.cs | 41/41 | no defects (log form and normal approximation already there) |
| Providers/InverseDistributionFunctions.cs | 245/245 | BETA.INV with upper ≤ lower, F.TEST with a constant first sample (minor) — fixed |
| Providers/StatisticsFunctions.cs | 100/100 | LARGE/SMALL over an array of ranks re-sorted the sample per rank (medium) — fixed |
| Providers/EngineeringFunctions.cs | 258/258 | MULTINOMIAL inherits the combinatorics fix; base conversions checked against Excel's 10-digit limits: correct |
| Providers/NumberSystemFunctions.cs | 77/77 | BITLSHIFT wrapped past 64 bits, bit functions accepted fractions, BASE/DECIMAL past 2^53 (minor/medium) — fixed |
| Numerics/RadixConverter.cs | 33/33 | no defects of its own |
| Numerics/BesselFunctions.cs | 193/193 | iterations bounded (9e6 cap, recurrences stop on overflow); no defects |
| Numerics/ComplexNotation.cs | 77/77 | no defects; mixed `i`/`j` suffixes accepted (minor, open) |
| Providers/DatabaseFunctions.cs | 159/159 | headers compared with the current culture (minor, open); calculated criteria not supported (open) |
| Providers/InformationFunctions.cs | 69/69 | no defects |
| Providers/ArithmeticFunctions.cs | 72/72 | no new defects; divergences below |
| Providers/TrigonometryFunctions.cs | 43/43 | COT/CSC/COTH/CSCH at zero gave `#NUM!`, Excel and the reference suite `#DIV/0!` (minor) — fixed |
| Providers/CompatibilityFunctions.cs | 257/257 | PERCENTILE.EXC/QUARTILE.EXC rejected the boundary ranks (medium), NEGBINOM.DIST overflow — fixed |
| Numerics/PercentRanking.cs | 23/23 | no defects |
| Providers/SummaryFunctions.cs | 169/169 | no defects (ExclusiveQuantile boundaries already inclusive) |
| Providers/ConditionalFunctions.cs | 147/147 | COUNTIF over more than 2^31 cells overflowed `int` (minor) — fixed |
| Functions/FunctionBuilder.cs | 125/125 | gained `Lifted<TState>` (prepare once, lift per element) |
| Functions/ArrayLifting.cs | 72/72 | re-read for the LARGE/SMALL fix; no defects |
| Functions/ArgumentReader.cs | 1–80 re-read | no defects |

Not read this round: `IntegrationFunctions.cs` and `MiscellaneousFunctions.cs` (web and cube functions),
`RegressionFunctions.cs`, `ForecastFunctions.cs`, `UnitConversion.cs`, `ExponentialSmoothing.cs`, `LinearRegression.cs`,
`LeastSquares.cs`.

## Defects

- **Blocking — SpreadsheetMath.cs `Factorial` (line 10), `DoubleFactorial` (line 17), `Combinations` (line 25): the
  loops kept multiplying after the result was already infinite.** `=COMBIN(1E+15;5E+14)` looped 5·10^14 times and froze
  recalculation; the same for COMBINA, PERMUT (also through `Factorial` up to `int.MaxValue`), FACTDOUBLE, MULTINOMIAL,
  HYPGEOM.DIST and NEGBINOMDIST. Excel answers `#NUM!` at once. The loops now stop at the first infinite partial
  result, so they run at most a few hundred steps before overflowing.
- **Serious — DistributionFunctions.cs HYPGEOM.DIST cumulative (line 274): summed every mass from 0 to x**, each one a
  product loop over the population. With a population of 2·10^9 it never finished. Now
  `HypergeometricDistribution` (new): exact products up to a population of 1,000, log-gamma beyond; the cumulative sums
  only the tail that decreases away from the mode, stopping when terms become negligible, and takes the complement when
  x is past the mode.
- **Medium — HYPGEOM.DIST, NEGBINOMDIST and NEGBINOM.DIST overflowed on ordinary inputs**:
  `HYPGEOM.DIST(500;1000;1000;2000;FALSE)` and `NEGBINOM.DIST(2000;2000;0.5;FALSE)` were `#NUM!` (Excel ≈ 0.0357 and
  ≈ 0.0063) because C(2000;1000) is infinite. Both now fall back to log-gamma (`NegativeBinomialDistribution`, new,
  shared by the two functions that had duplicated the formula).
- **Medium — CompatibilityFunctions.cs `Quantile` (line 235): PERCENTILE.EXC and QUARTILE.EXC rejected k = 1/(n+1) and
  k = n/(n+1).** `PERCENTILE.EXC({1;2;3};0.25)` was `#NUM!`, Excel gives 1. The bounds are now inclusive and the
  position is clamped against rounding.
- **Medium — RoundingFunctions.cs ROUND/ROUNDUP/ROUNDDOWN/TRUNC (lines 14–17): `(int)digits` saturated** to
  `int.MinValue`; ROUND then threw `OverflowException` in `Math.Abs` and TRUNC returned NaN, so `=ROUND(5;-1E+10)` was an
  error instead of 0. Digits are now clamped to ±400 before use.
- **Medium — NumberSystemFunctions.cs `Shift` (line 73): BITLSHIFT shifted a `long` and wrapped past 64 bits**, so
  `BITLSHIFT(2^47;17)` was 0 instead of `#NUM!`. The shift is now computed exactly in floating point and checked against
  2^48.
- **Medium — StatisticsFunctions.cs LARGE/SMALL (line 52): each rank of an array argument re-read and re-sorted the
  sample.** `SUM(LARGE(SEQUENCE(30000);SEQUENCE(30000)))` took more than 10 s. `FunctionBuilder.Lifted<TState>` now
  prepares the sorted sample once and lifts only the rank.
- **Minor — ModernLookupFunctions.cs (open from round 16): a 2-D lookup array was accepted**, XLOOKUP with a return
  array shorter than the lookup array gave `#REF!`, binary search modes ran linearly, and wildcard matching with a
  binary mode was accepted. Excel: `#VALUE!`, `#VALUE!`, a real binary search, `#VALUE!`. Now so: binary modes use a
  lower-bound search on the total order numbers < text < logicals < blanks.
- **Minor — bit functions accepted fractional operands** (`BITAND(1.5;1)`); Excel gives `#NUM!`. Fixed.
- **Minor — BASE and DECIMAL past 2^53** returned imprecise values; Excel gives `#NUM!`. Fixed.
- **Minor — FISHERINV overflowed** for |y| > 355 (`exp(2y)` infinite); now `tanh`.
- **Minor — COT/CSC/COTH/CSCH at zero** gave `#NUM!`; now `#DIV/0!`.
- **Minor — BETA.INV with upper ≤ lower** returned a value; **F.TEST with a constant first sample** returned 0. Both
  `#NUM!` / `#DIV/0!` as in Excel.
- **Minor — ConditionalFunctions.cs `MatchingPositions`: the cell count was an `int`**, so `COUNTIF(E:XFD;"")`
  (1.7·10^10 cells) overflowed and answered 0. Now a `long`.

## Divergences from Excel kept on purpose

- DATE maps years 0–29 to 2000–2029 (two-digit year rule of the reference suite); Excel maps every year below 1900 to
  1900 + year.
- Serial 1 is 1899-12-31 (epoch 1899-12-30, as in the reference suite), so serials below 61 differ by one day from
  Excel's fictitious 1900-02-29; negative serials are dates before the epoch instead of `#NUM!`.
- `ATAN2(0;0)` is 0 and `0^0`/`POWER(0;0)` is 1 (pinned by `NumberToTextAndRuleTests`); Excel gives `#DIV/0!` and `#NUM!`.
- SIN/COS/TAN of arguments ≥ 2^27 return a value; Excel gives `#NUM!`.
- BASE accepts a minimum length up to 65,534 (pinned by `DeepAuditSheetsEngineTests`); Excel stops at 255.

## Still open

- WORKDAY/NETWORKDAYS walk day by day (bounded by the date range, about 3.6 million steps at worst).
- Database functions compare headers with the current culture and do not support calculated criteria.
- IMSUM/IMPRODUCT accept mixed `i` and `j` suffixes (Excel: `#VALUE!`); IMDIV by zero is `#DIV/0!` (Excel: `#NUM!`).
- MINVERSE/MDETERM O(n³) and user regular expressions on the backtracking engine (from round 16).

## Tests

`tests/PulseOffice.Sheets.Engine.Tests/DeepAuditLedgerFunctionsRound17Tests.cs` (54 cases, each formula on a thread
with a time limit). Run against the unmodified sources (extracted with `git archive HEAD` into the scratchpad) 37 failed,
six of them by timeout. After the fixes the whole Sheets.Engine test project passes (1,323) and the Architecture tests
pass (38).

## Suggested live tests

- `=COMBIN(1E+15;5E+14)` gives `#NUM!` at once instead of freezing.
- `=HYPGEOM.DIST(5E+8;1E+9;1E+9;2E+9;TRUE)` answers about 0.5 immediately.
- `=PERCENTILE.EXC({1;2;3};0.25)` gives 1; `=XLOOKUP(3;A1:A3;B1:B2)` gives `#VALUE!`.
- `=SUM(LARGE(SEQUENCE(30000);SEQUENCE(30000)))` is immediate.
