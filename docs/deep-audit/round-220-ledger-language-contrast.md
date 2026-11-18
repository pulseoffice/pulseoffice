# Round 220: Ledger table pickers and lost tables, XLSX rule ids, edit language switch, high-contrast theme

Scope: four queued items (rounds 216 and 217). Not built or run in this round; the coordinator builds and tests after
the join.

## Fixed

1. **Chart series sources: table column picker and lost tables.** `ChartSeriesRanges` (shared chart editor) gets an
   optional `ChartSeriesTables`: a "Table" button beside each series range opens a menu of the workbook's tables and
   their columns and writes `Table[Column]`; a caption under the box names the table and column of a structured
   source, so table references read as such. Ledger supplies it from `IChartTableSources.Tables`, `Describe` and
   `Reference` (column names escaped with `'`), and the series validator now accepts a table reference whose table
   exists. When a table behind a series disappears, `ChartDataExtractor` marks the data with `MissingTables`, and
   `ChartRenderer` draws a dashed placeholder naming the table instead of the chart; `ChartTableLossWatch` (one per
   Ledger view, checked after each history step so a rename's remove-and-define does not warn) reports
   "A chart lost its source table" in the status bar once per newly missing table. Tests: `Round220ChartTableTests`.
2. **XLSX table-reference rule forms matched by a stable id.** Each `rule` entry of the `urn:pulseoffice:spreadsheet`
   worksheet extension now carries the rule's priority and its ordinal among rules sharing range and priority; each
   `validation` entry carries its position. The reader first takes the rule or validation the id names and restores
   it only if its flat formulas still match; entries whose id fails fall back to the range and flat-formula match over
   the rules not yet taken. Identical flat rules with different structured forms no longer swap, and a rule edited in
   Excel keeps its flat form without stealing another's. Tests: `Round220TableReferenceRuleIdTests`.
3. **Language switch during a Ledger cell edit.** Chosen: translate the edit rather than commit or cancel it, so the
   user loses neither the typing nor the place. `IFormulaLocalization.Translate` converts typed text from one
   language's separators and function names to another's without requiring a complete formula;
   `LedgerCellEditing.FollowLanguage` applies it to the in-cell editor or to an edit in the formula bar when the
   language changes, remembering the language the text was shown in. Plain entries are left as typed. Tests:
   `Round220EditLanguageTests`.
4. **High-contrast theme.** `ThemePreference.HighContrast` (setting choice `highContrast`, five languages) maps to a
   custom Avalonia theme variant inheriting Dark, so every control that repaints on a theme variant change repaints
   at run time. `Palette` has a third table (`PaletteVariant.HighContrast`): black surfaces, white primary text,
   secondary text and the status colours at 7:1 or more on every background, white field borders, a black chart page
   with white ink and a light guide. Module accents are made readable as text at 7:1 in this variant. Focus rings
   use a new `PulseFocusRing` token: the module accent in light and dark, yellow in high contrast. Tests:
   `Round220HighContrastTests`.

## Open

Listed in docs/audit-queue.md (round 220).
