# Round 331: find panel leftovers, Folio find leftovers, Ledger find and replace

Area: the "Find panel (326)" and "Folio find and replace (326)" queue items, then Ledger find and replace (within
sheet or workbook, by rows or columns, formulas, values and comments, entire cell, formats, replace all as one undo
step). No builds or tests were run (audit cycle rule); the tests below run at the join.

## Design

- `ITextFindHighlights` (UI.Controls/Search, singleton) holds one literal query per document object and computes the
  spans of a text; `RichTextScene` gained `Found` and `FoundFill`, and `RichTextFramePainter` fills the found spans
  under the glyphs, so any rich text frame can show find highlights.
- Deck: `ISlideTextSources` (Presentations) lists the searchable texts of an element: text boxes, drawn shape text
  (through `IShapeRichText`) and every table cell (header included, covered merge cells skipped). `DeckTextSearch`
  walks them, so Find, Find Next and the count see shapes and tables; a cell match carries its row and column.
  `IDeckElementTextRewrite` writes a rewritten text back (text boxes, shapes through `FromRich`, cells through
  `IDeckTableKeys.Write`), and `DeckTextActions.RewriteTexts` uses it, so Replace All (and the other whole-deck text
  rewrites: case conversion, auto-redaction) cover shapes and tables in the same undo step. The find panel's source
  shows the query in `ITextFindHighlights`; `DeckView` passes it to `SlideView.Found`, which fills the spans in text
  and shape text through the slide palette and tints table cells with a match (`ISlideFoundCells`). The fill is the
  theme's warning colour at 35 %.
- Ledger: `ILedgerFindHighlights` (Sheets.UI, singleton) keeps the panel's query per workbook and computes the
  matching cells of a sheet, cached by the sheet's revision; `SheetGridView.FindHighlights` paints them with the
  warning token (`GridOverlayPainter.PaintFound`). `LedgerFindTarget` now supports Find Next and Find Previous: it
  selects the next match after the current cell, by rows or by columns (new "Search by columns" option), across sheets
  with "All sheets", wrapping (`IWorkbookMatchOrder`), and reports the cell found.
- Folio: `SearchStory` carries the document's `StyleSheet`; `DocumentSearch` resolves bold, italic and underline
  through `IStyleResolver.EffectiveRun` (character style, paragraph direct formatting, paragraph style chain).
  `SearchStory.SectionStarts` marks the first paragraph inside a `SectionBlock` and the first after it, which the
  search text turns into `^b` marks. `^b` in a replacement splits the paragraph and starts a page style section with
  the page style in effect. `IFolioFindHighlights` keeps matches inside comments and the focused comment match;
  `FolioCommentMargin` marks them inside the balloon text, the focused one stronger, while Find Next still selects
  the comment's anchor in the text.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Serious | Ledger Find Next did not move to any match: the target had no navigation, the panel's Next only counted. | Find Next and Previous select the match, by rows or columns, across sheets. |
| Medium | Ledger Replace All with "Search by format" replaced in every cell regardless of format (styles not passed to the replace pass). | `ISheetSearch.ReplaceAll` takes the style catalogue; `WorkbookSearch` passes it. |
| Medium | Deck find ignored text in drawn shapes and tables; Replace All left them untouched. | `ISlideTextSources`, `IDeckElementTextRewrite`. |
| Medium | Ledger and Deck find panel did not highlight matches. | Grid and slide highlights. |
| Medium | Folio format search ignored formatting inherited from styles. | Effective run resolution. |
| Medium | `SectionBlock` boundaries were not `^b` matches; `^b` could not be inserted. | Section starts in the search text; `^b` replacement. |
| Medium | A match in a comment could not be seen inside the comment. | Marks in the comment balloon. |
| Minor | Ledger "Entire cell" was ignored with regular expressions. | The pattern is anchored. |
| Minor | Deck find panel showed no match count. | `Summarize` counts. |

## Still open (queued)

See docs/audit-queue.md, entries marked (331): Ledger displayed-value search, invariant number parsing of
replacements, search within a multi-cell selection, shape text in Ledger, frozen pane highlights, style-only changes
in the highlight cache; Deck table matches select the table, literal-only matching, notes not highlighted; Folio
section blocks are not removed by replacing their `^b`, comment text not edited in place; nothing tried live.
