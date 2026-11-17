# Round 154: Ledger references under structural edits (2026-11-17)

Continuation of round 153: chart sources, hyperlinks to cells, print ranges, names and validation lists under row and
column insertion and deletion, range moves, sheet renaming and sheet deletion.

## Defects fixed

- **Inserting or deleting rows and columns left a cell's hyperlink and named style behind** (serious, silent data
  misplacement). `CellsPart` moved values, formulas and own styles but not `Cell.Hyperlink` or `Cell.StyleName`: the
  link stayed on the old address, now attached to another cell. The part now clears both at the origin and writes them
  at the destination. Test: `Round154LinkReferenceTests.Inserting_rows_carries_a_cells_own_link_with_the_cell`.
- **Internal hyperlinks never followed their target** (medium). Links such as `#Sheet1.A5`, `#'My Data'!C5` or the
  unqualified `#A6:B7`, on cells and on placed objects, were ignored by structure edits, range moves, sheet renaming and
  sheet deletion. They now go through the same rewrite as chart sources (`ChartReferenceRewrite.Link`, parsed by
  `CellLinkTargets`, which keeps the original quoting and separator); a vanished target becomes `#Sheet.#REF!`.
  `IChartSheetReferences` now returns `SheetReferenceChange` entries (`ObjectReferenceChange`, `CellLinkChange`), so
  the rename plan (`SheetRenamePlan.ObjectsAndLinks`) and sheet deletion apply and revert them in the same undo step.
  Links to names, web addresses and unknown sheets are left alone. Tests: the other five in
  `Round154LinkReferenceTests`.

All tests failed on the code before the fixes (except the one checking that unrelated links are untouched) and pass after.

## Checked without defects

- Chart series sources (`SourceSheet`/`SourceRange`/`SeriesSources`): follow structure edits, moves, renaming and sheet
  deletion through `ChartStructureRewrite`, `ChartMoveRewrite` and `ChartSheetRewrite`.
- Print ranges: stored per sheet without a sheet name, so renaming cannot break them; `PrintRangesPart` shifts them and
  drops deleted ones.
- Named ranges and validation and rule formulas naming other sheets: covered by `NamesPart`, `ConditionsElsewherePart`,
  `MovedNamesPart`, `MovedValidationsPart` and the rename plan (rounds 150 and 153).

## Open (minor)

- Repeated print rows and columns are counts from the top of the print area, not references: inserting rows inside the
  repeated band does not grow it.
- Print ranges do not follow a range move.
- An unqualified internal link in a cell moved to another sheet is read against the new sheet.
- Hyperlinks of members inside shape groups are not rewritten (only the placed object's own link).

**Status: serious and medium found and fixed**; minor items open.
