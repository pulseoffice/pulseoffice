# Round 256: Ledger calculation engine (dynamic arrays, LAMBDA helpers)

Area: dynamic arrays and spill, LET/LAMBDA and helpers, IFERROR/IFNA, recalculation, 1904 dates, XLSX/ODS prefixes.

## Findings

- Medium, fixed: MAP refused arrays of different sizes with #VALUE!. Excel broadcasts single-row, single-column
  or single-value arrays and pads positions a shorter array does not reach with #N/A, as array operators do.
- Minor, fixed: MAKEARRAY turned an error in its row or column count into #VALUE! instead of propagating it.
- Checked, no defect: IFERROR and IFNA lift over arrays through the builder's liftable positions; `_xlfn.` names
  of LET, LAMBDA and the helpers and `_xlpm.` parameters are mapped.

## Open (queued)

- Spill, `@`, `#` references, iterative calculation, volatile dirty tracking, 1904 dates and the `cm` dynamic array
  metadata were not re-audited in depth this round.

Test: `Round256LambdaHelperTests`.
