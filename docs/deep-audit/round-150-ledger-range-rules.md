# Round 150: Ledger range-bound rules after round 149 (2026-11-16)

Follow-up of round 149's open suspicions, then the other Ledger features bound to ranges (named ranges, data bars and
icon sets, filters, sparklines, protection) checked for the same patterns: clipped starts, re-anchoring, sheet
renaming, cost of lookups while saving.

## Defects fixed

- **Conditional formats: deleting the first rows or columns of a range broke relative formulas** (medium, command
  doing the wrong thing). `RulesPart` re-translated the formula only when a whole first range was dropped, not when a
  deletion clipped the start of a range, so `B1>50` on A1:A10 became a reference to a deleted row after deleting rows
  1-2. The surviving start is now computed by one shared `SurvivingStart` service, used by both `RulesPart` and
  `ValidationsPart`, and the formula is translated from the old anchor to it before the structure rewrite. Tests:
  `ScopedNamesAndMultiRangeRuleTests.Deleting_the_first_rows_of_a_rule_keeps_its_relative_formula_on_the_surviving_cells`
  and the columns twin.
- **ODS writer: validation names looked up linearly for every cell** (medium, performance). Measured with one rule
  per row and 20 values per row: 1500 rules saved in 1.1 s, 6000 in 12 s (x11 for x4). `OdfSheetValidationNames` now
  indexes the rules by row band (binary search over the band starts, only the rules of that band are scanned).
  Relative test: `OdsValidationWritingPerformanceTests.Saving_many_validations_grows_linearly_with_their_number`
  (4x the rules must take less than 8x the time).
- **Renaming a sheet left conditional-format and validation formulas pointing to the old name** (medium, silent
  breakage). `SheetRenaming` rewrote cell formulas, names and charts, but not the formulas of conditional rules and
  validations on any sheet (for example a list validation `Sheet1.$A$1:$A$5`). The plan now carries, per sheet whose
  rules change, the rules and validations before and after (`SheetRuleRename`), applied and reverted with the rest.
  Test: `SheetRenameAndDuplicateTests.Renaming_a_sheet_rewrites_the_rules_and_validations_that_point_to_it`.

Each test failed on the code before its fix and passes after.

## Checked without defects

- ODS `table:base-cell-address` different from the range start: a relative condition is re-anchored to the range
  (new test `OdfValidationAuditTests.A_relative_condition_follows_a_base_cell_other_than_the_first_cell_of_its_range`,
  green on the existing code).
- Named ranges: structure edits rewrite their expressions with the scope's sheet; renaming rewrites them.
- Sparklines: cell and source move or drop with structure edits (sources are same-sheet ranges).
- Filters, range sort, database ranges and consolidation areas follow structure edits (`FiltersPart`).

## Open (not verified)

- Formula thresholds of data bars, colour scales and icon sets (`ConditionalThreshold.Formula`) are not rewritten by
  structure edits, range moves or renaming; not probed with a test in this round.
- Sheet duplication of rules and validations that refer to the source sheet by name was not probed.

**Status: medium found**, none serious or medium open in the fixed scope.
