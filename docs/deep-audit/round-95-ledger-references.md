# Deep audit round 95 — Ledger range-bearing objects against structural edits

Scope: every range-bearing object of a Ledger workbook against every structural edit (rows, columns, cells shifted
right/down/left/up, cut-paste, sheet insert/delete/move/rename/copy). Follows round 92's open item (cell shifts did not
move filter areas).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/round-92-filter-leftovers.md | full | scope |
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | full | parts pipeline; band shift runs only five parts |
| src/PulseOffice.Sheets.Engine/Operations/SheetCellShift.cs | full | duplicates cells/formulas/forms/charts outside the pipeline |
| src/PulseOffice.Sheets.Engine/Operations/FilterStructurePart.cs | full | band-aware, never run for cell shifts |
| src/PulseOffice.Sheets.Engine/Operations/StructureRewriter.cs | 1-80 | `StructureChange` band |
| src/PulseOffice.Sheets.Engine/Operations/SheetSnapshot.cs | full | no scenarios, query bindings, labels, frozen panes |
| src/PulseOffice.Sheets.Engine/Operations/WorkbookCheckpoint.cs | full | no defined names |
| src/PulseOffice.Sheets.Engine/Operations/ChartReferenceUpdates.cs | 51-85 | band-aware |
| src/PulseOffice.Sheets.Engine/Operations/FormControlRelinker.cs | 1-40 | band-aware |
| src/PulseOffice.Sheets.UI/LedgerStructureCommands.cs | full | own undo outside the checkpoint: names lost on undo |
| src/PulseOffice.Sheets.UI/LedgerCellShiftCommands.cs | full | no check for split blocks |
| src/PulseOffice.Sheets/Worksheet.cs | 1-180 | inventory of range-bearing members |
| src/PulseOffice.Sheets/Workbook.cs | 1-215 | names, sheet removal/move reindexing |
| src/PulseOffice.Sheets/{Sparklines,Scenario,QueryBinding,SheetLabels,FrozenPanes}.cs | full | |
| src/PulseOffice.Sheets/Pivot/PivotDefinition.cs | full | source/destination by sheet name |
| src/PulseOffice.Sheets/ExternalData.cs | 1-150 | area links follow |

## Matrix (object × edit), before → after

Rows/Cols = whole rows/columns inserted or deleted; Band = cells inserted/deleted with shift right/down/left/up.

| Object | Rows/Cols | Band | Sheet rename/move/delete |
|---|---|---|---|
| Cell formulas (all sheets) | yes | yes | yes |
| Defined names (global, sheet-scoped) | **no → yes** | **no → yes** | yes (rename) |
| Database ranges, their filters | yes | **no → yes** | yes (index map) |
| AutoFilter, sheet standard/advanced filter, criteria ranges | yes (r92) | **no → yes** | yes |
| Conditional formats (own sheet) | yes | yes | — |
| Conditional formats / validations of other sheets referring to the edited sheet | **no → yes** | **no → yes** | — |
| Validations | yes | yes | — |
| Merges | yes | yes; split now refused | — |
| Array formulas | yes | **moved partially → split refused** | — |
| Print ranges | yes | **no → yes** | — |
| Row/column page breaks | **no → yes** | n/a | — |
| Frozen panes | **no → yes** | n/a | — |
| Charts (compact and per-series sources) | yes | yes | yes |
| Pivot source range, fields, destination | **no → yes** | **no → yes** | name-based (not changed) |
| Query bindings | **no → yes** | **no → yes**, split refused | — |
| Area links / data stream | yes | **no → yes**, split refused | yes |
| Sparklines (cell and source) | **no → yes** | **no → yes** | — |
| Scenarios | **no → yes** | **no → yes**, split refused | — |
| Label ranges | **no → yes** | **no → yes** | — |
| Form-control links | yes | yes | yes |
| Shape anchors | yes | **no → yes** | — |
| Comments | yes | yes | — |
| Outline groups | yes | n/a | — |
| Tracked changes | yes | yes | yes |
| Cell hyperlinks to cells (`#Sheet.A1`) | no | no | no — by design, as the reference suite and Excel |
| Sheet protection allowed ranges | not in the model | | |

## Defects and fixes

- **R1 (major, silent corruption)**: defined names never followed any structural edit; checkpoints did not hold them, so
  undo could not restore them either. `NamesPart` rewrites each expression with `StructureRewriter` (scope sheet for
  unqualified references; a global name's unqualified references belong to the sheet using it and are left alone);
  `WorkbookCheckpoint.Names`.
- **R2 (major)**: cell shifts ran a hand-written duplicate of the pipeline (raw cell moves, formulas, form links, charts)
  plus five band parts: filters, database ranges, print ranges, area links, shapes, names did not move. Restructured:
  `SheetStructure` has one part list; parts that only make sense for whole lines declare `WholeLinesOnly`; `ShiftBand`
  runs every other part, so cell shifts and whole-line edits share one mechanism. `SheetCellShift` only builds the
  `StructureChange`.
- **R3 (major)**: a band shift through part of a merge, array, AutoFilter/filter/database range, query binding, area link
  or scenario moved half of it. Parts expose `Blocks` (must not be crossed by the band edge) and `RigidBlocks` (merges,
  arrays: also not cut along the shift axis); `ISheetCellShift.KeepsBlocksWhole` refuses, with
  `status.cells.wouldSplit` in five languages.
- **R4 (medium)**: pivot source range, field column indices and destination; query bindings; sparklines; scenarios;
  label ranges; page breaks; frozen panes did not follow. New parts in `ReferenceStructureParts.cs`.
- **R5 (medium)**: conditional-format and validation formulas on other sheets referring to the edited sheet were not
  rewritten (`ConditionsElsewherePart`).
- **R6 (medium)**: insertion past the last row/column produced invalid addresses in `StructureShift`; it now drops them.
- **R7 (medium)**: whole-line commands kept their own undo (one sheet snapshot + placements + external data), which
  missed names, pivots and other sheets' conditions. They now use `IWorkbookCheckpoints` like cell shifts: one step.
  `SheetSnapshot` holds scenarios, query bindings, labels and frozen panes.

## Remaining

- Cut-paste of ranges (references following moved cells) and sheet copy were not audited in depth this round.
- Cell hyperlinks pointing to cells do not follow (same as the reference suite and Excel).
- `RemoveDuplicates` deletes cells through `ISheetCellShift.Delete` without the split check (its area is the selection
  band, so it never crosses a block edge across the band).

## Tests

- tests/PulseOffice.Sheets.Engine.Tests/Round95ReferenceFollowTests.cs (9 tests)
- tests/PulseOffice.Sheets.UI.Tests/ChartSeriesSourcesCommandTests.cs `Inserted_rows_move_names_and_frozen_panes_in_one_undo_step`

Runs: Sheets.Engine.Tests 1817/1817, Sheets.UI.Tests 468/468, Sheets.Tests 356/356, Localization.Tests 32/32,
Architecture.Tests 43/43.
