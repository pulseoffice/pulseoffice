# Round 209: Folio and chart queue (line number restarts, section column widths, RTF Hiragana, pivot chart categories)

Area: four items of `docs/audit-queue.md`. No build or test run in this round: the coordinator runs
`tools/run-tests.py --changed` at the join.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Medium | DOCX wrote a Folio line number restart only as a continuous section with the document's start value, and only when numbering restarted per section; a restart with another value, a restart on a page style change or a restart opening a column section was lost (DOCX248). | Each Word section carries its own `w:lnNumType`: `DocxSectionCodec.Write` takes the section's line restart and writes `w:restart="newSection"` with `w:start` = value − 1. The writer gives that value to the section opened by a restart paragraph (a new continuous section), by a page style change, and to the closing section of a column `SectionBlock` whose first paragraph restarts. The reader keeps the document's numbering from the first section (later sections no longer overwrite it) and reads a `newSection` restart of any later section as a restart of its first paragraph, except a page style section restarting at the start value when numbering already restarts per section. DOCX248 now covers only what no body section can carry; its text is updated in the five languages. | `Round209DocxLineRestartTests`, updated `Round204DocxFolioMinorQueueTests` |
| Minor | The section dialog seeded and scaled uneven column widths against a fixed 17 cm text width. | `IPageTextWidth.PageAt` gives the text width of the page style a paragraph sits on (columns ignored); the insert dialog uses the caret's paragraph, the edit dialog the first paragraph of each section. | `Round209PageTextWidthTests` |

## Research, no change

- **RTF Hiragana numbering.** The `\levelnfc` codes of the RTF specification follow Word's number formats: 12 (aiueo)
  and 13 (iroha) are Katakana, 20 and 21 their double-byte Katakana forms; no code gives Hiragana (Word's own number
  formats have none either, which is also why DOCX writes Hiragana page numbers as Katakana, DOCX247). RTF007 stays.
- **Pivot chart categories in XLSX.** A chart linked to a pivot table by `c:pivotSource` is rebuilt from the pivot table
  by spreadsheet applications on refresh; the category formula is informative and the `c:multiLvlStrCache` carries the
  hierarchy until then. In a compact layout every parent item sits on its own row, so no cell range holds the outer
  labels aligned with the leaves; a wider formula, or a hidden helper range, would only give a refreshing application
  wrong labels or extra cells the user did not create. The one-column `c:multiLvlStrRef` with the full cache stays.

## Open items

Listed in `docs/audit-queue.md` (marked 209): the line number restarts DOCX still loses, RTF007, and opening a pivot
chart file in Excel.
