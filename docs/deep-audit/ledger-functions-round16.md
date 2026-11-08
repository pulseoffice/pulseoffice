# Deep audit round 16: Ledger's function layer (Sheets.Engine/Evaluation/Functions)

Round 15 (`ledger-core-round15.md`) covered the evaluator core. This round read the shared function infrastructure and
the riskiest families: lookup, text, iterative financial solvers, dates and array functions, plus the minors left open
by round 15.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Functions/CriteriaMatcher.cs | 111/111 | no defects of its own; uses `WildcardPattern` (see below) |
| Functions/WildcardPattern.cs | 46/46 | backtracking regex for wildcards (medium) — fixed |
| Functions/TextPattern.cs | 75/75 | per-match 1 s timeout only; now linear-time for wildcards |
| Functions/ValueCoercion.cs | 140/140 | no defects |
| Functions/ArgumentReader.cs | 103/103 | no defects (`Integer` saturates on huge doubles) |
| Functions/FunctionBuilder.cs | 120/120 | no text-length guard on results (serious) — fixed |
| Functions/ArrayLifting.cs | 67/67 | lifting materialised any range without a cap (serious) — fixed |
| Functions/ISpecialForm.cs | 51/51 | Italian exception message (minor, open from round 15) — fixed |
| Providers/TextFunctions.cs | 243/243 | SUBSTITUTE/CONCAT/CONCATENATE/TEXTJOIN unbounded (serious) — fixed |
| Providers/ExtendedFunctions.cs | 227/227 | REGEX replace unbounded (serious) — fixed; WORKDAY.INTL with huge counts ends in `#NUM!` via the date overflow (acceptable) |
| Providers/LookupFunctions.cs | 387/387 | `SortedVectorSearch` checked by hand on edge cases: correct; approximate MATCH/VLOOKUP linear (fine on sorted data) |
| Providers/ModernLookupFunctions.cs | 114/114 | binary search modes (±2) run linearly: same results on sorted data; 2-D haystack not rejected (minor, open) |
| Providers/ArrayFunctions.cs | 740/740 | MMULT unbounded (serious), UNIQUE quadratic (serious), SORT accepted any order (minor), TRUE equal to 1 in UNIQUE/SORT (medium), FREQUENCY O(n·bins) (minor) — all fixed |
| Numerics/MatrixAlgebra.cs | 112/112 | no defects; MINVERSE is O(n³) on large inputs (minor, open) |
| Numerics/FinancialMath.cs | 238/238 | IRR/RATE ignored the guess and missed roots (medium) — fixed |
| Providers/FinancialFunctions.cs | 359/359 | XIRR ignored guess, accepted dates before the start and one-signed flows (medium) — fixed |
| Numerics/DateSystem.cs | 50/50 | extreme serials throw `ArgumentOutOfRangeException`, caught by the evaluator as `#NUM!` |
| Providers/RandomFunctions.cs | 49/49 | no defects |
| Evaluator.cs | 1–430 re-read | `&` unbounded (serious), tall × wide broadcast uncapped (minor from round 15) — fixed |

## Defects

- **Serious — TextFunctions.cs SUBSTITUTE (≈line 95), CONCAT/CONCATENATE/TEXTJOIN, ExtendedFunctions.cs REGEX
  replace (≈line 75), Evaluator.cs `&` (≈line 282): text results had no length cap.** Nested
  `SUBSTITUTE(REPT("a";32767);"a";REPT("b";32767))` builds a billion characters; a chain of `=A1&A1` cells doubles each
  step until memory runs out. Excel's cell limit is 32,767 characters and longer results are `#VALUE!`. Fixed at the
  root: `ResultLimits` (new) holds the text and array caps; SUBSTITUTE predicts the length before replacing, the joins
  check while appending, REGEX replaces through a bounded loop, `&` checks before concatenating, and `FunctionBuilder`
  turns any longer scalar text result into `#VALUE!`.
- **Serious — ArrayLifting.cs `Apply`: lifting a scalar function over a range materialised every cell.** `=ABS(A1:XFD1100)`
  built 18 million values, a whole-sheet range exhausted memory. Now a lifted range or a broadcast result above
  16,777,216 cells is `#NUM!`.
- **Serious — ArrayFunctions.cs MMULT (≈line 290): result size uncapped.** `MMULT(SEQUENCE(5000);SEQUENCE(1;5000))`
  allocated 25 million cells. Now checked with `Allocatable`, like SEQUENCE and the generators.
- **Serious — ArrayFunctions.cs UNIQUE (≈line 175): quadratic.** 60,000 distinct rows took 48 s. Now a dictionary with
  a line comparer consistent with the ordering (case-insensitive text).
- **Medium — ArrayFunctions.cs `Compare`: TRUE equal to 1.** UNIQUE merged TRUE with 1 and SORT mixed logicals with
  numbers; Excel orders numbers, text, logicals, then blanks and errors. Fixed in the shared ranking.
- **Medium — FinancialMath.cs `InternalRateOfReturn`/`Rate`, FinancialFunctions.cs XIRR: pure bisection from
  −0.999999.** `IRR({-100;230;-132})` was `#NUM!` (Excel: 10 %), the guess never mattered, and XIRR ignored its guess.
  Now one solver, `FinancialMath.SolveRate`: Newton from the guess first, then the previous bracketing as fallback; the
  three duplicated bisections are gone.
- **Medium — XIRR: dates before the first date and one-signed flows were accepted.** Excel gives `#NUM!`; now so.
- **Medium — WildcardPattern.cs: wildcard criteria ran on the backtracking engine.** `"*a*a*a*a*a*a*a*a*a*b"` over
  80-character cells hit the 1 s timeout per cell (and silently answered "no match"). Wildcards translate to patterns
  without back-references, so they now use `RegexOptions.NonBacktracking` (linear time).
- **Minor — SORT accepted any sort order**; Excel accepts only 1 and −1 (`#VALUE!` otherwise). Fixed.
- **Minor — FREQUENCY was O(data × bins)**; now sorted bounds and binary search.
- **Minor — Evaluator.cs broadcast of a tall column by a wide row** was not capped by the product of the extents
  (open from round 15). Fixed.
- **Minor — ISpecialForm.cs registry exception in Italian** (open from round 15). Now English.

## Still open

- MAP/SCAN/BYROW over whole columns walk every row (as in Excel; the result shape requires it).
- XLOOKUP/XMATCH binary modes run linearly; a 2-D lookup array is not rejected with `#VALUE!`.
- MINVERSE/MDETERM are O(n³) with no work cap beyond the input size.
- User regular expressions (REGEX, regex criteria) keep the backtracking engine with a 1 s timeout per call, because
  they may use back-references and look-arounds.

## Tests

`tests/PulseOffice.Sheets.Engine.Tests/DeepAuditLedgerFunctionsRound16Tests.cs` (28 cases). Before the fixes 17 failed
(UNIQUE took 48 s); after, the whole Sheets.Engine test project passes (1,269) and the Architecture tests pass (38).

## Suggested live tests

- `=SUBSTITUTE(REPT("a";1000);"a";REPT("b";1000))` gives `#VALUE!` at once.
- `=IRR({-100;230;-132})` gives 10 %, with guess 0.25 gives 20 %.
- `=ROWS(UNIQUE(SEQUENCE(60000)))` is immediate.
- `=COUNTIF(A1:A20;"*a*a*a*a*a*a*a*a*a*b")` over long texts answers immediately.
