# Round 299: Ledger conditional formatting, remaining parts

Area: the parts of Ledger conditional formatting left by round 294: the manage rules dialog and the rule editor, the
quick rule commands, the ODS `style:map` reading path, the x14 icon set extension, range following on insert, delete
and move, paste with Transpose, and XLSX theme and indexed colours of cell styles and borders.

Method: code reading of every piece, comparison with Excel and the reference suite behaviour and with ODF 1.3 (16.3
`style:map`, `style:condition`) and ECMA-376 (`cfvo`, `x14:cfvo`, `x14:iconSet`); one small test per fix. No build
or test was run in the round (audit cycle rule); the coordinator runs them at the join.

## Defects

### Serious
- ODS standard conditional formatting was never read: `style:map` elements of `table-cell` styles (ODF 1.3, written
  by applications without the extension) were ignored, so their conditional formatting disappeared on opening.
  `OdsStyleMaps` collects the mapped style names from the automatic and common styles, records the regions of cells
  that use them while rows stream (before row compaction drops plain cells' style names), merging runs across columns
  and repeated rows; `OdfConditionalMapper.ReadStyleMaps` turns each map into a `calcext:condition` equivalent
  (`cell-content()op`, `cell-content-is-between`, `cell-content-is-not-between`, `is-true-formula`) and reads it
  through the existing condition codec, with the base cell. Read only when the table has no
  `calcext:conditional-formats`, since files with both carry the same rules twice.

### Medium
- ODS conditions whose operand is a reference or an expression (`>[.$D$1]`, `between([.B1];10)`) kept the text as a
  literal operand, so the rule compared cells with the text "[.$D$1]". Unquoted non-numeric operands are now read as
  expressions and become a formula relative to the base cell (`ConditionalComparisonFormula`, shared with the XLSX
  `cellIs` reader, which did the same inline).
- Paste with Transpose dropped the conditional rules of the copied cells. The rules now land on the turned ranges and
  their formulas go through `FormulaTranslator.Transpose` (relative references swap their row and column offsets
  from the anchor; absolute parts are kept).
- XLSX cell styles: font, fill and border colours given as `theme` or `indexed` (Excel's default for themed styles)
  were read as black or no fill. The cell style reader now resolves them through the workbook palette with tint.
- Percent, percentile and number thresholds that take their value from a formula (`cfvo type="percent"
  val="$C$1"`, x14 `xm:f`, ODS `calcext:value`) were read as percent 0 or as a plain formula threshold. The kind is
  kept with the formula, written back in XLSX, x14 and ODS, and evaluated by the engine.
- Clear rules from the selected cells removed every rule touching the selection, including its cells outside it.
  Rules are now trimmed to the cells outside the selection (as Excel does), with formulas reanchored; undo restores.
- Quick conditional commands (greater than average, negative, data bars, colour scale) appended the rule at the
  lowest precedence with priority equal to the rule count, which could collide with sparse priorities read from files;
  they now go first like the condition dialog (`IConditionalRuleInsertion`).
- The rule editor's custom format (background, font colour, bold) rebuilt the style from scratch, dropping the
  italic, underline, borders and number format of a rule read from a file. It now edits the rule's style.

### Minor
- The rule editor forgot the values typed for a kind after switching to another kind and back; they are kept per kind.
- The manage dialog added an undo step when nothing changed if the sheet's priorities were sparse.
- Splitting a rule on a partial range move reanchored only the main formula, not threshold formulas.

## Tests
- `Round299ConditionalTransposeTests`, `Round299ConditionalInsertionTests`, `Round299ConditionalFollowTests`
  (row deletion, column insertion, partial move) and the trimmed clear in `ConditionalRuleRemovalTests`
  (Sheets.Engine.Tests).
- `Round299XlsxThemeStyleColourTests`, `Round299ThresholdFormulaTests` (Formats.OpenXml.Tests).
- `Round299OdsStyleMapTests`, `Round299OdsThresholdFormulaTests` (Formats.Odf.Tests).
- `Round299ConditionalEditorTests` (Desktop.Tests, headless).

## Still open
Queued in docs/audit-queue.md under Ledger with "(299)": writing standard `style:map` for other ODF readers, column
default cell styles in the `style:map` path, mixed absolute references under Transpose, missing data bar and icon
controls in the editor, manager reordering under the selection scope.
