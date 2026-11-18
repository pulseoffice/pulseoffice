# Round 169: Ledger growth and relative timing tests

Branch `audit-169`. Nothing was built or run in this round: the coordinator compiles and tests after the join.

## Defects fixed

1. **Medium: whole-column merges were scanned linearly** (`MergedRegions`). Merges spanning 64 or more row bands
   lived in one flat set that every `At` and `Overlapping` call scanned, and `Overlapping` fell back to scanning
   every region whenever the band dictionary was empty, so adding or reading n whole-column merges cost n squared
   (`MergedRegionsIndexTests.MilleUnioniSuColonneIntereNonOccupanoUnaVocePerBlocco`: 110x for 10x). The region list
   also used `List.Remove`, linear per removal.
2. **Medium: dense side-by-side merges in a few rows were scanned linearly** (same class). Regions short in rows
   shared one 64-row band, so thousands of 1x2 merges along a header row put every region in one list. The index
   was restructured into tile levels: each region goes to the level whose tile height and width (powers of four)
   are the smallest that cover it within two tiles per axis, so every tile holds a bounded number of regions of
   any shape; `At` probes one tile per populated level, `Overlapping` enumerates the range's tiles or, when that
   is more than the level holds, the level's tiles. Insertion order is kept in a sequence map and `All` is
   materialised lazily; `Overlapping` returns regions in insertion order. Test: `Round169MergedRegionsTests`.
3. **Minor: row and column edits removed conditional rules one by one** (`RulesPart` in `SheetStructure`):
   `RemoveAt` from the middle of the list shifted every later rule. The list is rebuilt in one pass. Fill now asks
   `Merges.Overlapping(source)` instead of filtering every merge. Test: `Round169StructureGrowthTests` (delete the
   first half of n rows, each with a rule and a merge).

## Timing tests turned into growth checks

Absolute bounds under 5 s became `GrowthCheck` comparisons of a small and a 10x run in the same test (`Linear`
for work proportional to the size, `Flat` for work that must not depend on the extent, `Bounded` for per-frame or
per-key costs measured at two sizes): `SheetLayoutScaleTests` (offsets), `GridGeometryTests` (hidden rows),
`SheetFormattingTests` (whole-row formatting), `GridAndDataAuditTests` (summary of a whole column),
`LedgerGridRound19Tests` (both whole-row and whole-column fills), `LedgerRenderCostTests` (scroll, typing and
bottom-versus-top frames, plain and frozen), `ReviewPanelCostTests` and `NavigatorPanelCostTests`,
`FolioReviewTests` (typing, render and review source over many revisions), `SubtotalVisibilityRecalculationTests`,
`Round126SubtotalsTests`, `WholeColumnFillPerformanceTests` (unrelated edits), `DocxManyRunsTests`,
`DocxParagraphMarkChainPerformanceTests` (read, write, conditional fields), `AuditSpellingTests`,
`CompactTableTests` (the VDMX tail now grows with the group count, so a per-group copy would show as quadratic),
Typography `RobustnessTests` (overlapping cmap segments).

Left as they are, as hang guards of 5 s or more: the 6 s bound of `SheetLayoutScaleTests`, the whole-column fill and
million-row bounds, `DeepAuditLedgerFunctionsRound16/51/52Tests`, `PivotLimitsTests`, `RoundSixtyProbeTests`,
`RoundFourteenLayoutTests` (8 s), `DeepAuditRoundTwentyTwoPasteTests`, `TextEditSpliceTests`.
`DeepAuditRound27ChartTests` has no timing bound.

## Inspected without defects

Sheet search and replace (one pass over non-empty cells), filters (hash sets of hidden rows, rows hidden in
increasing order append to the run lists), structure edits of merges (clear and re-add, now linear).

## Open

- Nothing compiled or run here; the rewritten tests must be checked at the join, especially the UI cost tests,
  whose small runs set the reference (floor 10 ms).
- `RunLengthMap` keeps runs in a `List`, so inserting runs out of order costs a memory move per insertion; not
  measured as a problem at sheet sizes, not changed.
