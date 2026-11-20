# Round 293: Ledger conditional formatting (2026-11-20)

## Scope covered
- Rule evaluation (`ConditionalFormattingEvaluator`): cell value, formula, top/bottom N and percent, above/below
  average with standard deviations, duplicates/unique, text tests, date periods, blanks/errors, data bar layout
  (axis, negative colours, min/max length, direction), colour scales (default and explicit points), icon sets
  (default thresholds, reverse, custom icons, show icon only), priority and stop-if-true.
- XLSX differential formats (`DifferentialFormatTable`) and comparison operands.

## Defects found and fixed
- **Medium, data loss**: XLSX `dxf` kept only bold, italic, font colour and fill. Underline (single and double),
  strikethrough and borders of a conditional format were dropped on write and ignored on read, while ODS keeps them
  through the full condition style. Now written and read; the reader also accepts the fill colour in `fgColor` and
  honours `val="0"` on bold, italic and strike. Test: `Round293ConditionalDxfTests`.
- **Medium, Excel parity**: when several style rules matched one cell only the highest-priority rule's style applied;
  Excel merges them, the higher priority winning each property it sets. `ConditionalStyleMerge` now combines fill,
  font flags, font colour, borders per edge and number format; stop-if-true still ends the merge. Test:
  `Round293ConditionalStyleMergeTests`.

## Not covered in this round (queued)
- ODS codecs (`calcext:conditional-format`, `style:map`, date-is) audited only for the style path.
- x14 data bar and icon set extensions (`X14DataBars`, `X14RuleReaders`), the manage rules dialog and editor, and
  ranges following insert/delete/move/copy-paste were not audited.
- Number format in XLSX `dxf` (`numFmt`) is still neither written nor read; theme and indexed colours in `dxf` are
  ignored on read.
- Top/bottom percent rounding against Excel (ceiling here) not verified.
