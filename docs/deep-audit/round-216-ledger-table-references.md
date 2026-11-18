# Round 216: Ledger table references outside cells, relative-period menu, engine split

Scope: the round 210 Ledger item of docs/audit-queue.md. Not built or run in this round; the coordinator builds and
tests after the join.

## Fixed

1. **Chart series sources holding table references.** A series name, values or categories text may be a structured
   reference (`Sales[Amount]`, `Sales[[#Headers],[Amount]]`). `ChartTableSources` (Sheets.Engine, behind
   `IChartTableSources`) recognises it by parsing, resolves it against the current table range when the chart data is
   read (`ChartDataExtractor`), so the series grows and shrinks with the table rows, and renames it with the table or a
   header (`TableReferenceOwners`, used by `TableRenaming` and by the engine's header-rename follower). Structure edits
   leave the text alone because the table range itself moves. Formats: XLSX writes the resolved A1 ranges in `c:f`
   (Excel does not store structured references in charts) and keeps the structured list in a chart-space extension
   (`urn:pulseoffice:chart` `tableSources`); ODS writes the resolved ranges and keeps the structured list in a
   `urn:pulseoffice:spreadsheet` `table-sources` child of `chart:chart`, which other readers ignore. Tests:
   `Round216ChartTableSourceTests`, `Round216ChartTableSourceFormatTests`.
2. **XLSX conditional formats and validations with table references.** Excel refuses structured references there.
   `XlsxTableReferenceRules` (one per write) flattens every rule formula, threshold formula and validation formula to
   A1, feeds the same flattened instances to the main `conditionalFormatting`, to the x14 extension (data bars, extended
   icon sets) and to `dataValidations`, and writes a `urn:pulseoffice:spreadsheet` worksheet extension listing each
   changed rule or validation by range with its flat and structured formulas. The reader matches entries back by range
   and flat formula (ignoring `$`, spaces, the leading `=` and case) and restores the structured form. A file whose rule
   was edited elsewhere no longer matches and keeps the flat form. Test: `Round216TableReferenceRuleTests`.
3. **ODS colour-scale, data-bar and icon-set thresholds.** The threshold formulas were flattened but their structured
   form was dropped. Each `calcext:formatting-entry` or `calcext:color-scale-entry` whose formula changed carries the
   same `table-formula` extension attribute as rules and validations, read back in entry order. Test:
   `Round216ThresholdTableReferenceTests`.
4. **Relative-period filters in the AutoFilter menu.** A date column shows a "Date period" list (no period, today,
   yesterday, tomorrow, this/last/next week, month, quarter and year, year to date; five languages) that sets
   `ColumnFilter.Period`; a period read from a file is preset, so confirming the menu no longer drops it. ODS storage is
   unchanged and documented here: ODF has no relative filter, so the writer stores two bound conditions (`>=` start,
   `<` end) computed for the save date, each carrying our `relative-period` attribute; our reader takes the period
   first, other readers apply the bounds of the day the file was saved. Test: `Round216RelativePeriodMenuTests`.
5. **`CalculationEngine` under the type cap.** Spill handling (spill ranges, owners, blocked spills, loaded spills,
   materialising array results, declared array areas) moved to `SpillArea`, and the format lookup used for inferred
   number formats to `FormatLookup`; the engine is 797 lines and its exemption in `DesignRulesTests` and in
   docs/architecture.md is gone. Behaviour is unchanged: the same code, called through the new owner.

## Researched

Excel stores chart series, conditional-format and validation references as A1 text; structured references appear only
in cell formulas and defined names, so a native mapping does not exist and the extension route of rule 7 applies.

## Open

Listed under «Ledger» in docs/audit-queue.md (round 216).
