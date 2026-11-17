# Round 153: Ledger rule references after round 150 (2026-11-17)

Follow-up of round 150's open items: formula thresholds of data bars, colour scales and icon sets, sheet duplication,
protection ranges; plus pivot tables met while checking sheet renaming.

## Defects fixed

- **Formula thresholds of data bars, colour scales and icon sets never followed the sheet** (medium, silent breakage).
  `ConditionalThreshold.Formula` was ignored by row/column insertion and deletion (on the rule's sheet and on other
  sheets), by range moves and by sheet renaming, so a threshold `$C$5` kept pointing at the old cell. Every rewrite now
  goes through one model operation, `ConditionalRule.WithFormulas`, which maps the condition formula and every threshold
  formula; `RulesPart`, `ConditionsElsewherePart`, `MovedRulesPart` and `SheetRenaming` use it (thresholds are not
  re-anchored: they are evaluated without translation). Tests in `Round153RuleReferenceTests`: insertion on the same
  sheet, insertion seen from another sheet, renaming with undo, moving the referenced cells.
- **Renaming a sheet left pivot tables on the old name** (medium): `PivotDefinition.SourceSheet` and `DestinationSheet`
  are names, and the rename plan did not carry them, so refreshing failed ("source sheet does not exist") or recreated a
  sheet with the old name, and the pivot commands no longer found the pivot on its sheet. The rename plan now carries
  each pivot before and after, applied and reverted with the rest. Test:
  `Round153RuleReferenceTests.Renaming_a_sheet_keeps_the_pivot_tables_that_read_from_it_or_live_on_it`.

All five tests failed on the code before the fixes and pass after.

## Checked without defects

- Sheet duplication: rules and validations are copied as they are, exactly like the copied cell formulas, so a rule
  that names the source sheet keeps reading the source, consistent with the copy's cells; local names and pivots are
  rebased as before. No change.
- Protection ranges (allowed edit ranges): the model has none (`SheetProtection` carries only flags), so nothing can
  miss a structure edit.

## Open (not verified)

- The wider survey (chart series sources, hyperlinks to cells, print ranges under rename) was not repeated in this
  round, by request, to keep the release scope narrow.

**Status: medium found**, none serious or medium open in the scope.
