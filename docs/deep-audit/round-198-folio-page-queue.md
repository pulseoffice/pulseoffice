# Round 198: Folio page-layout queue

Area: the Folio page-layout items left open by round 197 (columns, page sides, line numbering, gutter, table width,
page number formats, mirrored margins). No build or test run in this round: the coordinator runs
`tools/run-tests.py --changed` at the join.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Medium | Columns of different widths were not modelled: DOCX declared them (DOCX245), ODT read them as equal columns. | `SectionColumns.Spans` (`ColumnSpan`: relative width and gap after); `ColumnLayout.Shares` lays them out (width, left and separator per column); ODT `style:column` `rel-width` with start and end indents, DOCX `w:equalWidth="0"` with `w:col` widths scaled to the text width. DOCX245 removed. The page and section dialogs keep the widths while the column count is unchanged. `OdtColumnsCodec` moved to its own file. | `Round198FolioPageQueueTests` (Layout, OpenXml), `Round198OdtPageQueueTests` |
| Medium | Even headers and mirrored margins took the side from the displayed page number, so a page-number restart flipped them. | `PageSide.Physical` (page index + 1) decides even or odd; incremental layout no longer reuses pages of a mirrored style at a shifted index. | `Round198FolioPageQueueTests` (Layout) |
| Medium | Word line numbering restarted per section and its start value were read as continuous numbering from 1; a `w:lnNumType` without `w:restart` was read as continuous although the default is per page. | `LineNumbering.RestartEachSection` and `Start`, laid out (restart where the page style changes), in the line numbering dialog (five languages), DOCX `w:restart="newSection"` and `w:start` (zero based), ODT our `pt:restart-on-section` and `pt:start`. | `Round198FolioPageQueueTests` (Layout, OpenXml), `Round198OdtPageQueueTests` |
| Medium | The gutter was folded into the left margin on reading and never written back. | `PageStyle.Gutter` and `GutterAtTop` (`BoundMargins` adds it), laid out, kept in snapshots; DOCX `w:pgMar/@w:gutter` and `w:gutterAtTop` (DOCX246 when left and top gutters are mixed); ODT folds it into `fo:margin-left` or `fo:margin-top` for other readers and records it in `pt:gutter` and `pt:gutter-at-top`. | `Round198FolioPageQueueTests` (Layout, OpenXml), `Round198OdtPageQueueTests`, `Round197FolioPageLayoutTests` updated |
| Medium | An inserted table took the text width of the document's first page style and ignored page columns. | `IPageTextWidth` finds the page style and section columns at the caret; used by the editor and the table commands. | `Round198FolioPageQueueTests` (Documents) |
| Minor | Hiragana page numbers were written as Arabic in DOCX without a diagnostic. | Written as Word's full-width Katakana (`aiueoFullWidth`), declared as DOCX247. | `Round198FolioPageQueueTests` (OpenXml) |
| Minor | Mirrored margins were written to DOCX only when every used page style was mirrored. | Also written when the other page styles are symmetric (equal left and right margins, no gutter), which mirroring leaves unchanged; DOCX037 remains only for asymmetric styles. | `Round198FolioPageQueueTests` (OpenXml) |

## Open items

Listed in the Folio section of `docs/audit-queue.md` (marked 198).
