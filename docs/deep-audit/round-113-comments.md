# Deep audit round 113: comments across the three modules (2026-11-07)

## Coverage (updated every five files)

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Presentations/SlideComment.cs | 18/18 | no replies or resolved state in Deck's model (by design) |
| src/PulseOffice.Sheets/CellComment.cs | 29/29 | ok |
| src/PulseOffice.Documents/Annotations/Comments.cs | 94/94 | D1 (no repair of broken threads) |
| src/PulseOffice.Formats.OpenXml/XlsxCommentMapper.cs | 113/113 | see below |
| src/PulseOffice.Formats.OpenXml/PptxCommentMapper.cs | 129/129 | see below |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxAnnotationsCodec.cs | 169/169 | D1, D2 |
| src/PulseOffice.Documents/Commenting/CommentEditing.cs | 164/164 | ok (cycle guards present) |
| src/PulseOffice.Documents/Annotations/UnanchoredAnnotations.cs | 41/41 | ok |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs (comment reader, annotation dispatch) | 360-520/1010 | D3 |
| src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs (comment writer) | 320-380/873 | ok |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxInlineReader.cs (comment edges, 130-430) | partial | reply marks dropped through IsReply: depends on D1 |
| src/PulseOffice.Formats.Odf/Odt/OdtReader.cs (Finish, Paragraphs) | 340-400/512 | D3 fix lands here |
| src/PulseOffice.Formats.Odf/Odt/OdtReadContext.cs | 1-140/140+ | D3 |
| src/PulseOffice.Sheets.UI/LedgerCommentCommands.cs | 133/133 | ok (undo symmetric, protection refuses) |
| src/PulseOffice.Documents/Editing/MetadataRedaction.cs (comment part) | 120-175 | ok (authors and initials neutralised in one step) |
| src/PulseOffice.Documents.UI/Rendering/FolioCommentSheet.cs | 54/54 | ok |
| src/PulseOffice.Formats.Package/XmlCharacterFilter.cs | 62/62 | reused for D5 |
| src/PulseOffice.Presentations.UI/DeckReviewSource.cs | 88/88 | D7 consequence (deletion by id hits every duplicate) |
| src/PulseOffice.Presentations/Slide.cs (comment members) | 135-165 | D6 |
| src/PulseOffice.Presentations.UI/DeckTextActions.cs (comment actions) | 80-125 | D6 |
| src/PulseOffice.Formats.Odf/Presentation/Reading/PageReaders.cs (CommentReader) | 270-295 | D7 |
| src/PulseOffice.Formats.Odf/Presentation/Writing/ElementWriters.cs (CommentsWriter) | 345-360 | ok |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs (OdsAnnotationBuilder) | 90-105 | ok (date written as UTC, read as UTC) |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs (CommentPartReader) | 68-90 | ok |

## Defects

| Id | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | serious | DocxAnnotationsCodec.ReadComments | a `w15:paraIdParent` that names the comment itself or forms a cycle turned anchored comments into "replies"; their range marks were dropped and the unanchored sweep removed the whole thread (silent loss) | `CommentCollection.DetachBrokenReplies()` (model, shared by both readers) clears missing parents, self parents and breaks every cycle at its first comment in document order; called after `commentsEx` is applied |
| D2 | serious | DocxAnnotationsCodec.WriteComments | a comment with no paragraph (empty, or ending with a table: possible from ODT) made `OfType<Paragraph>().Last()` throw, so the DOCX save failed | an empty paragraph is appended when the last block is not a paragraph; it carries the `paraId` |
| D3 | serious | OdtCommentReader / annotation-end | `loext:parent-name` and `office:annotation-end` used the raw name even when the reader had to rename a duplicate or invalid `office:name`; replies whose parent was missing, came later in the file or formed a cycle had no mark, stayed invisible in the pane and were dropped on save | names map to model ids (`OdtReadContext.CommentNames`); every annotation gets its mark, parents are resolved after the body (`OdtReader.Thread`), broken threads are repaired with D1's method, then the marks of real replies are removed |
| D4 | serious | XLSX threaded comments | Excel 365 comments were read from the legacy part only: author `tc={guid}`, text with Excel's English placeholder banner, replies mangled, no date | `XlsxThreadedComments` (sheet part reader) reads `threadedComments` + `persons`: display name, date, root text, replies joined as `author: text`; `XLSX235` (info) declares the flattened thread and lost resolved state; capped at 10 000 replies per cell |
| D4b | serious | PPTX modern comments | `ppt/comments/modernComment_*.xml` (PowerPoint 365 default) were not read at all: silent loss | `PptxCommentMapper.Modern` reads them with authors from `authors.xml`, date and position; replies joined; `PPTX223` (info) declares threads and resolved state |
| D5 | medium | PptxCommentMapper.Write | a control character in a slide comment's text or author made the PPTX save fail | text and author go through the shared `XmlCharacterFilter` (as ODP does); OpenXml project now references `PulseOffice.Formats.Package` |
| D6 | minor | DeckTextActions.RemoveComments | undoing a deletion appended the comments at the end of the slide (order, and PPTX index, changed) | indices captured; `Slide.InsertComment` restores each at its place |
| D7 | medium | ODP CommentReader | repeated `draw:id` values produced duplicate comment ids: deleting one comment from the review panel deleted all of them, editing hit the first | ids unique per read (`OdpReadContext.CommentIds`), a fresh id for repeats |

## Not defects / by design

- Deck and Ledger comments have no replies or resolved state in the model: threads from OOXML are flattened and declared (D4, D4b). Folio keeps full threads in DOCX (commentsEx/commentsIds) and ODT (`loext:parent-name`, `loext:resolved`).
- XLSX legacy comments carry no date: already declared (`XLSX035`).
- Auto-redaction (decision 23) neutralises comment authors and initials in the same step; dates are kept.

## Tests

- `tests/PulseOffice.Formats.OpenXml.Tests/CommentsRound113Tests.cs` (D1, D2, D4, D4b, D5)
- `tests/PulseOffice.Formats.Odf.Tests/OdtCommentThreadTests.cs` (D3), `OdpCommentIdTests.cs` (D7)
- `tests/PulseOffice.Documents.Tests/CommentThreadRepairTests.cs` (D1 model)
- `tests/PulseOffice.Presentations.UI.Tests/DeckCommentUndoTests.cs` (D6)

Runs: Formats.OpenXml.Tests 2467/2467, Formats.Odf.Tests 834/834, Documents.Tests 382/382, Presentations.UI.Tests 107/107,
Architecture.Tests 43/43.

## Remaining / live checks

- Open files with threaded (Excel 365) and modern (PowerPoint 365) comments in the real applications and check the joined text.
- Not read in depth this round: the shared `ReviewPanel` (394 lines), the Folio margin renderer and the PDF output of comments.
