# Deep audit round 100: range move and sheet copy, second pass

The «Remaining» of round 97 (docs/deep-audit/round-97-move-copy.md).

## Coverage (files read)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetRangeMove.cs | 268/268 | defects D2, D4 |
| src/PulseOffice.Sheets.Engine/Operations/RangeMoveParts.cs | 184/184 | defects D3, D5 |
| src/PulseOffice.Sheets.Engine/Operations/SheetDuplicator.cs | 171/171 | defect D1 |
| src/PulseOffice.Sheets.Engine/Operations/FormControlRelinker.cs | 77/77 | qualification added (D5) |
| src/PulseOffice.Sheets/TrackedChanges.cs | 185/185 | `ChangeLog` had no cross-sheet relocation (D4) |
| src/PulseOffice.Sheets/DatabaseRanges.cs, RangeFilters.cs, Filtering.cs (AutoFilterState) | partial | model shapes for D1, D2 |
| src/PulseOffice.Sheets/Workbook.cs (InsertSheet/RemoveSheet) | 95-150 | detached database ranges come back on reinsertion: reused for the copy's redo |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | full | blocks reused |
| src/PulseOffice.Sheets.Engine/Operations/WorkbookCheckpoint.cs | full | the change log and database ranges are in the move's checkpoint: one undo step |
| src/PulseOffice.Sheets.Engine/Recalculation/CalculationEngine.cs (Track, database names) | 380-410, 1110-1130 | the engine already records each cleared and written cell while recording |

## Defects and fixes

- **D1 (medium)**: a copied sheet did not duplicate its database ranges (tables). `SheetTableCopies` (owned by
  `SheetDuplicator`) copies each table on the source sheet with a unique name (`Table1_2`, `_3`…, free among tables and
  defined names), its column filters, totals row and advanced filter (criteria on the source sheet are rebased to the
  copy, other criteria follow the sheet insertion); the query binding is not carried (the copy is a plain table). Formulas
  on the copy that name the table (`TableNameRewriter` over `NameNode`) use the new name. `SheetCopy.Tables`; the copy's
  undo removes the sheet, whose tables are detached with it and come back on redo. One undo step.
- **D2 (medium)**: a move was refused when an AutoFilter, a sheet range filter or a database range lay wholly inside the
  moved range. Now only blocks that the move cuts refuse (`SheetRangeMove.SplitBlocks` removes the carried filters from the
  blocks); `MovedFiltersPart` carries them whole, also to another sheet (column filters kept, advanced-filter criteria
  wholly inside the moved range follow), and drops filters wholly inside the overwritten target area. `MovableFilters.LandFree`
  refuses a move that would give the target sheet a second AutoFilter or range filter, or land a table on another table.
  Query-bound tables, query bindings, scenarios and area links still refuse.
- **D3 (medium)**: conditional formats and validations partly inside the moved range were left in place whole.
  `RangeMoveSplitter` splits them first into the moved part and the remaining rectangles (`RangeCutter`), re-anchoring the
  relative references of each piece's formulas to its new top-left cell (`OffsetRewriter`), then the existing parts move
  the inside piece.
- **D4 (medium)**: pending tracked changes stayed at the source addresses after a move. `ChangeLog.Relocate` carries
  the records of moved cells to the target sheet and address (`MovedChangesPart`, first part of the move); the move itself
  is recorded by the engine as the clearing of the source cells and the writing of the target cells, so rejecting the
  records restores the values.
- **D5 (minor)**: form controls carried to another sheet kept unqualified links, which then read the target sheet.
  `FormControlRequalifier` qualifies them with the source sheet (and drops the qualification of links to the target sheet).

## Remaining

- Moving a filtered AutoFilter keeps the filtered-row hiding on the source rows; the filter is reapplied by the next
  filter command.
- Query-bound tables still refuse a move.

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round100MoveCopyTests.cs (6 tests)
- tests/PulseOffice.Sheets.Tests/ChangeLogSheetIndexTests.cs `RelocatingMovedCellsCarriesTheirChangesToTheTargetSheetAndLeavesTheOthers`

Runs: Sheets.Engine.Tests 1827/1828 (the failing one is the timing test
`InserireUnaRigaInCimaAMoltiDatiNonRicalcolaPerOgniCella`, row insertion, untouched code, under machine load),
Sheets.UI.Tests 469/469, Sheets.Tests 357/357, Architecture.Tests 43/43. No saved-format change: tables, filters,
rules, validations and changes use the existing ODS/XLSX mappings.
