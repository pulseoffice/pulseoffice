# Round 177: Ledger pivot tables, second pass

Area: what round 175 left out: the pivot dialog, pivot charts, undo of pivot edits, protected sheets and
structure-protected workbooks, sources with filtered or hidden rows, and the open item on mixed item ordering.

## Defects

1. **Medium: mixed numeric and text items sorted in a non-transitive order.** `PivotEngine.Compare` compared two
   numbers numerically, two group starts by start, and anything else as culture text, so "10" < "9" as text while
   "9" < "10" as numbers; with a mix of numbers and text the order depended on the input order and descending was not
   the reverse of ascending. Items now sort by a total order: grouped items by group start, then numbers by value,
   then text by culture comparison, with an ordinal tie-break; descending reverses it exactly.
2. **Medium: a pivot's output range did not follow structure edits or range moves.** Inserting or deleting rows or
   columns moved `Destination` but not `OutputRange`, and moving the pivot's cells moved neither; the next refresh then
   judged the shifted pivot cells as foreign data (PVT005) or cleared cells that were no longer the pivot's.
   `PivotsPart` now shifts the output range with the destination, and `MovedPivotsPart` moves destination sheet,
   destination and output range when the whole output is moved.
3. **Medium: applying a pivot added its destination sheet before the checks, and under structure protection.**
   A pivot that then failed (overlap, does not fit, destination occupied) left an empty new sheet behind outside the
   undo checkpoint (scripting path), and a refresh whose destination sheet had been deleted recreated it even in a
   workbook with protected structure. The sheet is now added only after every check passes, and a missing destination
   in a structure-protected workbook fails with the new diagnostic PVT007 (translated in the five languages).

Tests: `tests/PulseOffice.Sheets.Pivot.Tests/Round177PivotItemOrderTests.cs`,
`tests/PulseOffice.Sheets.Pivot.Tests/Round177PivotDestinationTests.cs`,
`tests/PulseOffice.Sheets.Engine.Tests/Round177PivotPlacementTests.cs`.

## Reviewed without defects

Dialog: the confirm button stays disabled without a value field, the destination name is checked against the tab
naming policy before applying. Create, refresh, calculated field and delete each run inside one workbook checkpoint
(one history step, rolled back on failure). Protected destination sheets block refresh; other protected-cell writes
are netted by `LedgerProtectionGuard`. Hidden and filtered source rows are included, as in the other spreadsheets.

## Open

- Pivot charts are not modelled: an XLSX chart with `c:pivotSource` is read as an ordinary chart over the drawn
  output (its extent trimmed to the cached point count) and the link to the pivot is lost; a refresh that grows the
  pivot does not grow the chart. Needs a pivot reference on `ChartDefinition`, both codecs, and a creation command.
- The dialog preview draws its cells with fixed light colours rather than theme tokens.
- Not built or run in this round (the coordinator builds and tests at the join).
