# Round 149: Ledger data validation (2026-11-16)

Focused code audit of an area whose last deep audit was among the oldest: Ledger data validation, in its two
halves — the ODS/XLSX codecs (`OdfSheetMetadataMapper`, `XlsxMetadataMapper`) and how rules follow structure edits
and placement (`ValidationsPart` in `SheetStructure`, `ValidationPlacement`, `DataValidator`).

## Defects fixed

- **ODS: a validation shared by scattered cells spread to the cells between them** (medium, silent data change on
  reading). The reader took the bounding box of all cells naming a `table:content-validation`; other suites reuse
  one name for identical rules in distant cells, so a rule on A1 and C3 covered the whole A1:C3 block. The reader now
  groups the named cells into rectangles (`ValidationAreas`: horizontal runs per row band, extended downwards while
  the same column span continues) and adds one rule per rectangle. It also now honours `table:base-cell-address`:
  the condition's relative references are anchored to the base cell and re-anchored to each rectangle (our files
  write the base at the range start, so they read as before). Test:
  `OdfValidationAuditTests.A_validation_shared_by_scattered_cells_does_not_reach_the_cells_between_them`.
- **Deleting the first rows or columns of a rule broke its relative limits** (medium, command doing the wrong
  thing). The range moved to the first surviving cell, but the formulas, relative to the old start, were rewritten
  against deleted cells and became `#REF!`; the validator then accepted everything. `ValidationsPart` now re-anchors
  the formulas to the first surviving cell before the structure rewrite, through `IValidationAnchoring`
  (`ValidationAnchoring`), the same service now used by `ValidationPlacement` and the ODS reader. Test:
  `DataValidationTests.Deleting_the_first_rows_of_a_rule_keeps_its_relative_limits_on_the_surviving_cells`.

Both tests failed on the code before the fixes (run with the old sources) and pass after.

## Checked without defects

- XLSX validation writing and reading, including the x14 extension list; literal lists with commas or over 255
  characters are already declared in `CheckFidelity`.
- `DataValidator` comparisons, list choices and custom formulas.

## Open (not verified)

- Conditional formats (`RulesPart`) re-translate the formula only when the first range of a multi-range rule is
  dropped, not when a deletion clips the start of a single range; the same defect may exist there. Not probed in
  this round.
- The ODS writer looks up the validation name per cell linearly over the sheet's rules
  (`OdfSheetValidationNames.For`); with thousands of rules on large sheets this is quadratic. Not measured.
- `table:base-cell-address` anchoring on a file from another suite is covered only through the rectangle test,
  not with a dedicated relative-formula probe.

**Status: medium found**, none serious or medium open in the fixed scope.
