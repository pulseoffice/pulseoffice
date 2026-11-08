# Declared losses, block B9

Block B9 of [declared-losses-analysis.md](../declared-losses-analysis.md): tracked changes on paragraph marks and table
rows left open by [B7](losses-b7.md): DOCX121, DOCX117, DOCX122, DOCX150, DOCX001.

## Files read

| File | Outcome |
|---|---|
| `Documents/Annotations/TrackedChanges.cs` | a paragraph-mark deletion is a point `Deletion` whose `Removed` holds two or more paragraphs |
| `Documents/Revisions/RevisionResolver.cs`, `ChangeRegions.cs`, `TrackedEditing.cs` | reject pastes `Removed` at the point: the host keeps its paragraph properties, the last removed paragraph gives its own to the tail |
| `Documents/Clipboard/FragmentPaster.cs` | confirms the convention above |
| `Formats.OpenXml/Wordprocessing/DocxRevisionWriter.cs` | root cause of DOCX001: a paragraph break in a deletion is written as a deleted `w:br` |
| `Formats.OpenXml/Wordprocessing/DocxDeletedParagraphMarks.cs`, `DocxBlockReader.cs`, `DocxBlockWriter.cs`, `DocxInlineWriter.cs` | reader folds only empty paragraphs; writer has no paragraph split |
| `Documents/Revisions/ChangeMarking.cs`, `Documents/Editing/TableEditing.cs`, `Documents/Editing/DocumentEdit.cs` (table memento) | row deletion reuses `ITableEditing.DeleteRow` (spans, undo); the memento now keeps `TableRow.ChangeId` |
| `Documents.Layout/Flow/ChangeMarkup.cs`, `Documents.UI/Editing/FolioReviewSource.cs` | rows have no marks: spans and review items are added from the rows themselves |
| `Formats.Odf/Odt/OdtWriter.cs`, `OdtReader.cs` (tracked changes), `OdtBodyWriting.cs`/`OdtBodyReading.cs` (rows) | ODT already carries a multi-paragraph deletion (`text:deletion` holding several `text:p`, ODF 1.3 §5.5.5); rows needed a carrier |

## Citations rechecked

- Deleted paragraph mark: `w:pPr/w:rPr/w:del`, ECMA-376 Part 1 §17.13.5.15 ("Deleted Paragraph"). B7 cited §17.13.5.14,
  which is deleted run content (`w:del` around runs).
- Tracked rows: `w:trPr/w:ins` §17.13.5.18 and `w:trPr/w:del` §17.13.5.12. Single cells: `w:cellIns` §17.13.5.2,
  `w:cellDel` §17.13.5.1, `w:cellMerge` §17.13.5.3.
- ODF 1.3 Part 3 §5.5 tracks text regions only: a paragraph merge is a `text:deletion` (§5.5.5) holding the removed
  paragraphs at a `text:change` point (§5.5.7). There is no row-level change, so the row carries a foreign attribute
  (§3.17) `pt:change-id` that names a `text:changed-region`.

## Changes

| Code | Outcome |
|---|---|
| DOCX122 | **Removed.** `DocxParagraphMarkJoins` (replaces `DocxDeletedParagraphMarks`) joins every paragraph whose mark is deleted with the next one, empty or not: the joined paragraph keeps the first paragraph's properties (as an untracked join in Folio does), and each junction becomes one point `Deletion` whose `Removed` holds the deleted tail of the paragraph before (with its properties) and the deleted head of the one after (with its properties). Text deletions of the same author right at the junction are absorbed, others stay their own changes; wholly deleted paragraphs in the chain extend the same junction. Anchored objects follow the joined paragraph. |
| DOCX001 | **Narrowed** to deletions holding tables or sections. `DocxParagraphMarkSplits` writes a paragraph holding such deletions as several `w:p`: the first keeps the host properties, the following ones take the properties of the removed paragraph they end with, every paragraph but the last gets `w:rPr/w:del`; removed text goes in `w:del` runs through synthetic one-paragraph deletions registered in `DocxWriteContext.SplitChanges` (`DocxWriteContext.Change` resolves them), so `DocxRevisionWriter.Deleted` keeps writing notes, comments and drawings of the removed text. |
| DOCX150 | **Narrowed** to marks before a table, at the end of a content control and on a section break. At the end of a story (body, cell, header, note) the last paragraph mark cannot be removed in Word, so its deletion has no effect: the chain before it is joined into it and nothing is declared. |
| DOCX121 | **Narrowed** to single-cell changes (`w:cellIns`, `w:cellDel`, now also `w:cellMerge`). `TableRow.ChangeId` names an `Insertion` or a `Deletion` with nothing removed (the row stays in the table until accepted). DOCX: `DocxRowChanges` reads and writes `w:trPr/w:ins|w:del`. ODT: `pt:change-id` on `table:table-row`, the region in `text:tracked-changes` (also in headers, `OdtChangeWriter.References`). Undo memento, clone and session snapshot keep the id. |
| DOCX117 | **Left declared.** A field holds one instruction string; a change inside it (`w:delInstrText` next to `w:instrText`, `w:ins` around `w:instrText`) would need tracked marks inside field instructions. |

Accept/reject in the model: `ITrackedRows` (`TrackedRows`) finds the rows of a change; `RevisionResolver` resolves row
changes first: accepting an insertion or rejecting a deletion clears the id, accepting a deletion or rejecting an
insertion removes the rows through `ITableEditing.DeleteRow` (spans and undo as for a manual delete) and drops the
text changes left without marks inside them. Paragraph-mark deletions needed no change: rejecting pastes `Removed`
back and splits the paragraph again with its own properties.

Display: `ChangeSpans` adds spans for tracked rows (inserted rows underlined, deleted rows struck through in the
author's colour; a range `Deletion` is now struck through too). Review panel: row changes are listed as "Table row:
<text>" (`review.row`, five languages) and Go selects the row.

## Tests

- `Formats.OpenXml.Tests/DocxParagraphMarkRevisionTests` (join, round trip, three-paragraph deletion from the model,
  cell end, before a table, tracked rows round trip); `DocxRangeAndNoteReadingTests` updated (the chain with text is now
  joined, DOCX122 gone).
- `Formats.Odf.Tests/Odt/OdtParagraphMarkRevisionTests` (joined paragraphs and tracked rows cross ODT).
- `Documents.Tests/ParagraphMarkRevisionTests` (reject splits with the right properties, accept keeps one paragraph,
  accept/reject of tracked rows with undo).
- Whole projects run: Formats.OpenXml.Tests 2252, Formats.Odf.Tests 775, Documents.Tests 304, Documents.Layout.Tests
  200, Documents.UI.Tests 302, Localization.Tests 32, Architecture.Tests 40: all green.

## Corpus

`corpus-audit one` on the 38 corpus DOCX files with deleted paragraph marks or tracked rows: LOSS 0 on all.

Full run after B9 (worktree also holding main up to B8): **LOSS 0, DECL 137**. The only rows left from this block are
`rprchange_closed.docx` (DOCX117, docx->docx and docx->odt); DOCX122 rows (2) are gone, no DOCX121/150/001 row.
