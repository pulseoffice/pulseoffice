# Deep audit round 97: range move (cut and paste) and sheet copy

Leftover of round 95 (docs/deep-audit/round-95-ledger-references.md, «Remaining»): cut-and-paste of cell ranges and
sheet copy were not checked against the range-bearing objects.

## Coverage (files read)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | 488/488 | parts, blocks and rigid blocks reused for the move refusal |
| src/PulseOffice.Sheets.Engine/Operations/SheetDuplicator.cs | 96/96 | defects C1-C3 |
| src/PulseOffice.Sheets.UI/Exchange/LedgerExchange.cs | 350/350 | defect M1 |
| src/PulseOffice.Sheets.Engine/Operations/FormulaRewriter.cs | 101/101 | base for the move rewriter |
| src/PulseOffice.Sheets.Engine/Operations/SheetRenaming.cs | 160/160 | `SheetNameRewriter` reused by the sheet copy |
| src/PulseOffice.Sheets.Engine/Operations/StructureRewriter.cs | 93/93 | ok |
| src/PulseOffice.Sheets.Engine/Operations/ChartReferenceUpdates.cs | 157/157 | `ChartReferenceRewrite` reused |
| src/PulseOffice.Sheets.Engine/Operations/FormControlRelinker.cs | 61/61 | restructured over a reference map |
| src/PulseOffice.Sheets.Engine/Operations/SheetCellShift.cs | 103/103 | ok |
| src/PulseOffice.Sheets.UI/LedgerSheetCommands.cs (duplicate), LedgerPartialCommands.cs (move/copy dialog) | partial | both copy paths |
| src/PulseOffice.Sheets.Engine/Operations/ReferenceStructureParts.cs (sparklines, scenarios) | partial | ok |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs (blocks) | partial | ok |
| src/PulseOffice.Sheets/Workbook.cs (sheet insert/remove, detached names) | 110-145 | local names of a removed sheet are kept and come back on reinsertion: reused for the copy's undo |
| src/PulseOffice.Sheets/DatabaseRanges.cs, Sparklines.cs, Pivot/PivotDefinition.cs, Formatting/ConditionalRule.cs | partial | model shapes |

## Object × operation matrix

Move = cut then paste inside the same workbook (`ISheetRangeMove`, `SheetRangeMove`, parts in `RangeMoveParts.cs`).
Copy = sheet duplicate and the move/copy dialog's copy (`ISheetDuplicator` returning a `SheetCopy`).

| Object | Before: move | After: move | Before: copy | After: copy |
|---|---|---|---|---|
| Cell values, formulas, styles, arrays | cut cleared the source at once; paste re-offset relative references | moved as Excel: formula text kept, references into the moved range follow, moved formulas going to another sheet qualify their references to the source sheet | copied | unchanged (explicit references to the source sheet keep pointing at it, as Excel) |
| Formulas elsewhere pointing into the moved range | not touched (pointed at the emptied cells) | follow, also across sheets; ranges only partly inside stay; references to overwritten target cells become `#REF!` | n/a | n/a |
| Defined names | not touched | follow (scope sheet for unqualified references) | sheet-scoped names lost | local names copied with the copy's scope and rebased; global names referring to the source sheet become local names of the copy (Excel) |
| Charts and series sources | not touched | follow (`ChartMoveRewrite` over `ChartReferenceRewrite`) | copy kept pointing at the source sheet | copy's charts pointing at the source sheet point at the copy; original untouched |
| Conditional formats | not touched | rules wholly inside move (to the target sheet when crossing), formulas rewritten; rules wholly inside the overwritten area dropped | copied | unchanged |
| Validations | not touched | as conditional formats | copied | unchanged |
| Filters, criteria, database ranges, query bindings, scenarios, area links | not touched | moving cells that intersect them is refused (`status.cells.wouldSplit`) | AutoFilter and range filter copied | unchanged; database ranges (tables) are not duplicated (declared below) |
| Merges, array formulas | half moved | moves that cut them at the source or at the target are refused; wholly inside move; overwritten ones dropped | copied | unchanged |
| Pivot source | not touched | follows when wholly inside | pivots placed on the source sheet lost | duplicated with a fresh id and unique name, destination (and source, when on the same sheet) on the copy |
| Pivot output | — | moving cells holding a pivot destination is refused | | |
| Sparklines | not touched | cell and source follow; across sheets they move when both cell and source move | copied | unchanged |
| Form-control links | not touched | follow (`FormControlRelinker` now works over an `IReferenceMap`, shared by structure edits and moves) | copied | unchanged |
| Comments | not touched | follow; comments in the overwritten area dropped | copied | unchanged |
| Shape anchors | not touched | objects anchored in the moved range move (to the target sheet when crossing) | copied with fresh ids | unchanged |
| Print ranges | | not touched (Excel does not move them either) | copied | unchanged |
| Undo | cut and paste were two steps | one step (`IWorkbookCheckpoints`) | one step, names and pivots included | one step |

## Defects and fixes

- **M1 (major, silent corruption)**: cut cleared the source cells at once and paste copied with relative re-offsetting;
  nothing that referred to the cut cells (formulas elsewhere, names, charts, rules, validations, pivots, sparklines,
  form links, comments, objects) followed, and merges/arrays could be cut in half. Restructured: cut marks the range
  (`LedgerCut` in `LedgerCellsNative`); pasting it in the same workbook runs `ISheetRangeMove`, whose refusal reuses the
  blocks and rigid blocks of the round-95 parts (`ISheetStructure.Blocks`/`RigidBlocks`). One undo step. A cut is consumed
  by its first paste; pasting it into another application or workbook is a copy (as Excel).
- **C1 (medium)**: sheet copy dropped the source's local names. Fixed (`SheetCopy.Names`).
- **C2 (medium)**: charts on the copy kept reading the original sheet. Fixed.
- **C3 (medium)**: pivots placed on the copied sheet were not duplicated (their output cells became dead values). Fixed.

## Remaining

- Database ranges (tables) on a copied sheet are not duplicated (unique names would need renaming in formulas).
- Moving cells that intersect a filter, database range, query binding, scenario or area link is refused, even when the
  whole object lies inside the moved range (Excel moves a whole table).
- Rules and validations partly inside the moved range are not split; tracked changes do not follow a move.
- Form controls carried to another sheet keep unqualified links relative to the sheet they now live on.

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round97MoveCopyTests.cs (5 tests)
- tests/PulseOffice.Sheets.UI.Tests/ChartSeriesSourcesCommandTests.cs `Duplicating_a_sheet_copies_local_names_and_rebased_charts_in_one_undo_step`

Runs: Sheets.Engine.Tests 1822/1822, Sheets.UI.Tests 469/469, Sheets.Tests 356/356, Architecture.Tests 43/43.
Desktop.Tests (filter ContextRefusal|Clipboard|Exchange) 124/124.
