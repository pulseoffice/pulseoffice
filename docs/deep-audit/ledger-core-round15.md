# Deep audit round 15: Ledger's core (Sheets, Sheets.Engine, Sheets.Pivot)

Previous logs (`sheets-engine.md`, `sheets-model.md`, `sheets-round2.md`, `ledger-round7.md`) already cover every
provider, the parser, the dependency graph, `CalculationEngine` and most of `Operations`. This round compared the file
list against those logs and concentrated on the evaluation core and the files never listed.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| Evaluation/Evaluator.cs | 444/444 | eager IF/IFS/IFERROR/IFNA/SWITCH (serious) — fixed; nested `EvaluateRaw` reset the lambda call depth (serious) — fixed; elementwise operators over huge ranges allocated every cell (serious) — fixed |
| Evaluation/Functions/SpecialForms/LambdaForm.cs | 82/82 | no defects; `IsOmittedForm.Category` is an Italian literal like every other category key (minor, consistent with the registry) |
| Evaluation/Functions/SpecialForms/HigherOrderForms.cs | 225/225 | MAP/SCAN/BYROW over `A:A` invoke the lambda for every row of the sheet (1,048,576 calls) even beyond the used area (minor, open: the result shape requires it, as in Excel) |
| Evaluation/Functions/RangeMaterializer.cs | 120/120 | no defects (stores only the used area) |
| Evaluation/Functions/ISpecialForm.cs | 51/51 | registry exception message in Italian (minor, open: not a log) |
| Evaluation/ArrayModeContext.cs | 58/58 | no defects |
| Evaluation/Functions/Providers/LogicalFunctions.cs | 157/157 | see eager IF |
| Recalculation/CalculationEngine.cs | 200–660 re-read | on-demand `Compute` re-enters `EvaluateRaw` (source of the depth reset) |
| Operations/SheetStructure.cs | 300/440 | parts order verified (cells moved before formulas rewritten); rest read in round 7 |

## Defects

- **Serious — Evaluator.cs `EvaluateFunction` (≈line 388): functions that handle errors evaluated every argument
  eagerly.** IF, IFS, IFERROR, IFNA and SWITCH computed both branches, so a recursive LAMBDA (the standard
  `IF(n<2;n;f(f;n-1)+f(f;n-2))`) explored every branch down to the 64-level cap: `fib(fib;15)` was ~2^64 calls and
  froze the calculation forever. Fixed at the root: functions that declare `HandlesErrors` receive a
  `LazyArgumentList` (new, `Evaluation/LazyArgumentList.cs`) that evaluates each argument on first access and caches
  it; the other functions keep the eager left-to-right evaluation with error propagation.
- **Serious — Evaluator.cs `EvaluateRaw` (line 34): the lambda call depth was reset to 0 at every evaluation.**
  `CalculationEngine.ComputeOnDemand` evaluates another cell from inside a running formula; the reset, followed by the
  `finally` decrement of the enclosing `Invoke`s, drove the counter negative and doubled the allowed recursion each
  time, until the stack overflowed (process terminated). Now `EvaluateRaw` keeps the enclosing depth and restores it
  (and the last result) on exit, so nested evaluations share the same budget.
- **Serious — Evaluator.cs `ApplyOverArrays`/`EvaluateUnary`: an operator over a huge range materialized every cell.**
  `=SUM((A1:XFD1100)*1)` built 18 million values (5 s, ~0.5 GB); a whole-sheet range (1.7·10¹⁰ cells) exhausted
  memory. Now a range operand above 16,777,216 cells (the same cap as SEQUENCE and the other array generators) gives
  `#NUM!` at once.

## Still open

- Elementwise result of a tall range times a wide array is not capped by the product of the two extents (minor).
- MAP/SCAN/BYROW on whole columns walk all rows (minor).
- Remaining parts of `SheetStructure.cs` (lines 300–440) were read in round 7 and not re-read here.

## Tests

`tests/PulseOffice.Sheets.Engine.Tests/DeepAuditLedgerCoreRound15Tests.cs` (7 tests), run alone in the
Sheets.Engine test project; verified to fail (or hang) on the code before the fixes.

## Suggested live tests

- `=LET(fib;LAMBDA(f;n;IF(n<2;n;f(f;n-1)+f(f;n-2)));fib(fib;20))` gives 6765 immediately.
- `=SUM((1:1048576)*1)` gives `#NUM!` without freezing.
