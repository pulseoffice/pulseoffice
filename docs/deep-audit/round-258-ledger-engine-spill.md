# Round 258: Ledger calculation engine, spills and volatility (2026-11-19)

Area: dynamic array spill, `@` and `#`, iterative calculation, volatile functions and dirty tracking, 1904 dates,
XLSX dynamic array metadata. No build or test was run in the round (audit cycle rule).

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | A dynamic array spilled over merged cells; Excel returns #SPILL! | `SpillArea` blocks a target that overlaps a merge |
| Medium | A dynamic array spilled into a table; Excel returns #SPILL! (spills are not allowed in tables) | `SpillArea` blocks a target that overlaps a database range; table changes recalculate spill anchors, so removing the table lets the spill resume |
| Medium | RANDARRAY was not volatile, so it never changed on recalculation as Excel's does | added to the volatile set of `FormulaDependencies` |
| Minor | `A1#` read the spill range without computing a pending anchor first, so on-demand evaluation could see a stale range | `WorkbookEvaluationContext.GetSpillRange` computes the anchor first |

Tests: `tests/PulseOffice.Sheets.Engine.Tests/Round258SpillAndVolatileTests.cs`.

## Checked and found sound

- `@` (`UnaryOperator.Single`): intersects multi-cell ranges, takes the first element of arrays, #VALUE! for unions.
- `#` references: #SPILL! when the anchor is blocked, #REF! when there is no spill.
- Iterative calculation: max iterations clamped to 1..32767, stops at max change, cycle inputs are computed on demand.
- Volatile set: NOW, TODAY, RAND, RANDBETWEEN, INDIRECT, OFFSET, INFO, CELL, followed through defined names.
- XLSX: `cm` written for dynamic anchors and read back to clear implicit intersection; `_xlfn.ANCHORARRAY` and
  `_xlfn.SINGLE` map `#` and `@`; `_xlfn._xlws.` for FILTER and SORT; `_xlpm.` for LAMBDA parameters.
- 1904 XLSX workbooks: date-formatted constants are shifted at load with a diagnostic.

## Open (queued in docs/audit-queue.md)

Unmerge does not recalculate blocked anchors; ODS `table:null-date` ignored; 1904 shift limited to date-formatted
constants; not covered in depth: spill and volatile recalculation after structural edits, XLSX writer metadata for
blocked anchors, ODS spill round trip, iteration with spilling cells.
