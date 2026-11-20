# Round 315: Folio columns and lists leftovers

Area: the Folio columns (313) and Folio lists (313) queue items, plus what round 313 did not cover (column balancing
before continuous breaks, odd and even section breaks through ODT page style usage, `w:equalWidth` omitted).
No builds or tests were run in this round (audit cycle rule); the coordinator runs them at the join.

## Defects

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Medium | The column separator's style, width, colour, height and vertical alignment were not modelled: ODT always wrote a solid black 0.5 pt line, losing what the file had | Fixed |
| 2 | Medium | A Word `nextColumn` section break read back as a column break on the first paragraph and was written as continuous plus that break; a section starting with a table lost it | Fixed |
| 3 | Medium | Right and left page styles (Word odd and even page section breaks, ODT `style:page-usage`) never inserted a blank page, so the section started on the wrong side | Fixed |
| 4 | Minor | The shared list sample view drew every number left-aligned whatever its alignment | Fixed |
| 5 | Minor | `w:cols` with `w:col` children and no `w:equalWidth` read as equal columns | Fixed |
| 6 | Medium | Word balances page-level columns before a continuous break; ours are never balanced | Queued |
| 7 | Minor | ODF restart level stays our own attribute | Researched, queued |
| 8 | Minor | Column editor has no controls for the separator line properties | Queued |

## Fixes

1. `SectionColumns.SeparatorLine` (`ColumnSeparatorLine`: `BorderLineStyle`, width in points, colour, `HeightPercent`,
   `ColumnSeparatorAlignment`). ODT reads and writes `style:column-sep` with `style:style`, `style:width`,
   `style:color`, `style:height` and `style:vertical-align` natively; ODF has no double style, so a double line is
   written solid with our `pt:line-style="double"`. Layout passes it to `ColumnLayout` and `PaginationRun` draws the
   separator with its colour, width, style, height and position; the renderer draws it through the border painter,
   so dotted, dashed and double lines show. The column editor keeps the line when only count or widths change.
   DOCX research: ECMA-376 `w:cols/@w:sep` is a boolean, and no later Word namespace extends it; Word itself offers
   only the on/off. A non-default line is declared as DOCX274.
2. `SectionBlock.StartsInNextColumn`. The DOCX reader wraps every `nextColumn` section in a section block (even when
   its columns equal the page's), the writer emits `w:type nextColumn` for it, ODT carries our
   `pt:starts-in-next-column`. Layout: a section starting in the next column advances to the next column (or page
   when on the last one); with the same column grid it continues in the current region instead of opening a new one.
3. `PaginationRequest.Usage` gives each page style's usage; `NewPage` inserts an empty page of the current style when
   a right style would land on an even page number or a left style on an odd one. The incremental layout no longer
   treats documents with right or left page styles as parity-stable.
4. `ListSampleRow.Alignment` and `LabelLeft` place centred and right-aligned numbers around the number position.
5. `w:col` children count as uneven widths when `w:equalWidth` is missing and they cover every column.

## Research: ODF restart level

ODF 1.3 list level styles (`text:list-level-style-number`, `text:outline-level-style`) carry no restart level; `text:list`
has only `text:continue-numbering` and `text:continue-list`, and `text:start-value` on a list item is a value, not a
level rule. No native mapping exists; `pt:restart-after` stays and degrades to the default (restart after any higher
level).

## Tests

- `Round315OdtColumnTests` (ODT round trip of the line and the next-column start).
- `Round315SectionColumnTests` (DOCX table-led next-column section and its write back, widths without equalWidth, DOCX274).
- `Round313SectionBreakTests` updated to the section block form.
- `Round315ColumnAndSideLayoutTests` (separator geometry and colour, blank page before a right page style).
- `Round315ListSampleAlignmentTests`.

## Join risks

`PaginationRun` grew to about 708 lines. Existing tests that expected a Word `nextColumn` section as a
`ColumnBreakBefore` paragraph, or a right/left page style to follow without a blank page (default Left/Right page
styles in `DefaultStyles` chain each other, which keeps parity), may need updating.
