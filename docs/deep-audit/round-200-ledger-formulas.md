# Round 200: Ledger formula engine

Scope: dynamic arrays, the lambda helpers, the modern lookup and array functions, and their crossing of XLSX and ODS.

## Defects fixed

- **Medium: REDUCE stopped at the first error.** The fold returned as soon as the accumulator became an error, so a
  lambda that absorbs it (for example with IFERROR on the accumulator) never got the chance; the other spreadsheet
  applications keep folding. The early return is gone.
- **Minor: SEQUENCE accumulated rounding drift.** Terms were built by repeated addition of the step, so
  `SEQUENCE(4;1;0;0.1)` ended with 0.30000000000000004; each term is now start + step × index.

Tests: `Round200FormulaEngineTests` in `PulseOffice.Sheets.Engine.Tests`.

## Reviewed without defects

MAP, SCAN, BYROW, BYCOL, MAKEARRAY argument and shape checks; XLOOKUP and XMATCH match and search modes; UNIQUE; the
XLSX writer's choice between `t="array"` and dynamic-array spill formulas.

## Open items

Listed under «Ledger» in docs/audit-queue.md.
