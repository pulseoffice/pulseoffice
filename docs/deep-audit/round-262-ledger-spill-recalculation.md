# Round 262: Ledger spill recalculation and spill round trips (2026-11-19)

Area: the leftovers of round 258 (unmerge and blocked anchors, ODS null date, XLSX 1904 plain numbers) and what it did
not cover in depth: spills and volatile cells after row and column insertion and deletion and range moves, XLSX
`cm`/`metadata.xml` for blocked anchors and cached spilled values, ODS round trip of spill anchors and spilled values,
iterative calculation with spilling cells in the cycle. No build or test was run in the round (audit cycle rule).

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | Unmerging cells left an anchor blocked by the merge at #SPILL! until the next edit in its range; merging over a live spill did not block it either | `Worksheet.MergesChanged` relayed by `Workbook.MergesChanged`; `CalculationEngine` recalculates the spill anchors of that sheet |
| Medium | A spilling formula inside an iterated cycle kept only its scalar: the spill was never laid out (or a previous one stayed stale) | `Iterate` keeps the last raw result of each cell and materializes multi-valued ones through `SpillArea` (releasing the spill otherwise); `IsMultiValued` moved to `SpillArea`, `RecalculationPass` to its own file |
| Medium | A blocked (#SPILL!) or single-cell dynamic anchor read from XLSX or ODS was written back as a plain formula when no recalculation ran in between (conversion, or calculation off): the dynamic array marker (`cm`, spill attribute) was lost | XLSX and ODS readers set `UsesArrays` on dynamic anchors |

Tests: `tests/PulseOffice.Sheets.Engine.Tests/Round262SpillRecalculationTests.cs` (unmerge, merge over a spill, rows
inserted above and inside a spill, deleting the blocking row, `A1#` reader after column insertion, moving an anchor,
RANDARRAY after a structure edit, iterated cycle through a spill) and
`tests/PulseOffice.Formats.OpenXml.Tests/Round262BlockedSpillRoundTripTests.cs` (blocked anchor `cm` and `#SPILL!`,
double round trips in XLSX and ODS, cached values of spilled cells in both formats).

## Checked and found sound

- Row and column insertion and deletion and range moves end in a full recalculation (`SheetStructure.Run`,
  `SheetRangeMove`), which resets the spill area, so anchors spill again from their new place and volatile cells are
  recomputed; spilled cells are not moved as content.
- XLSX writer: dynamic anchors get `t="array"`, `ref` and `cm`, one `metadata.xml` with the XLDAPR type; spilled cells
  are written as plain cached values; blocked anchors keep their error value.
- ODS writer and reader: dynamic anchors carry the matrix span and the spill mark; covered cells come back as spilled.

## Open (queued in docs/audit-queue.md)

- ODS `table:null-date` other than 1899-12-30 and XLSX 1904 plain numbers: both need a workbook date epoch in the model
  (`CalculationSettings`), honoured by `IDateSystem` consumers through the evaluation context and by the number format
  renderer, with the 1904 load shift removed; too wide for a round without builds. Not fixed.
