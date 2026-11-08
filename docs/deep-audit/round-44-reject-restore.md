# Deep audit round 44: rejecting a tracked deletion restores everything it took

Follows [round 43](round-43-folio-editing.md), which declared four limits: rejecting a tracked deletion did not bring back
(a) floating objects anchored to wholly deleted paragraphs, (b) comments whose anchor was deleted, (c) a field half held by a
wholly deleted paragraph (it came back as plain text); and (d) row, column and table deletion left a lost change edge orphaned.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Editing/RangeDeletion.cs | 327/327 | `MarkPurge` restructured (see fixes) |
| src/PulseOffice.Documents/Revisions/DeletedAnnotations.cs | 63/63 | extended: anchored objects and comments |
| src/PulseOffice.Documents/Revisions/TrackedEditing.cs | 226/226 | origin map of the removed paragraphs |
| src/PulseOffice.Documents/Editing/ParagraphText.cs | 282/282 | no defect |
| src/PulseOffice.Documents/Revisions/RevisionResolver.cs | 187/187 | no defect |
| src/PulseOffice.Documents/Revisions/ChangeRegions.cs | 86/86 | lone end: fixed at the source (edges are parked) |
| src/PulseOffice.Documents/Revisions/TrackedRows.cs | 67/67 | no defect |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | 205/205 | no defect |
| src/PulseOffice.Documents/Editing/ObjectAnchoring.cs | 42/42 | no defect |
| src/PulseOffice.Documents/Editing/TableEditing.cs | 95-180 | per-cell discards: batched |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxRevisionWriter.cs | 30-140 | see formats |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 290-370 | see formats |
| src/PulseOffice.Documents/Editing/Tables/TableConversion.cs | 140-160 | discard of dropped header rows batched |
| src/PulseOffice.Documents/Annotations/UnanchoredAnnotations.cs | 1-40 | already counts comments held by pending deletions |
| src/PulseOffice.Documents/Clipboard/FragmentExtractor.cs | 1-60 | keeps paragraph ids: used as origin map |
| src/PulseOffice.Documents/Marks/InlineMark.cs | 142/142 | no defect |

## Fixes

1. **Serious (a)** - a floating object anchored to a paragraph wholly removed by a tracked deletion was purged
   (`MarkPurge.RemoveObjectsAnchoredTo`), so rejecting did not bring it back. With `AnnotationFate.Retain` the object stays in
   the store; `TrackedEditing.Removed` now returns the map from each removed paragraph to its copy in `Deletion.Removed`, and
   `IDeletedAnnotations.Keep` re-anchors the objects of paragraphs that left the live text to their copy (same undo step,
   through `IObjectAnchoring`). `Revival` pastes them back (the remapper re-anchors them to the revived paragraph) and
   `Release` removes them on accept and on reject (the originals). Both writers already wrote objects anchored to removed
   paragraphs (`w:del` around the drawing, the frame inside `text:deletion`) and both readers read them back.
2. **Serious (b)** - a comment whose range was deleted with tracking on was removed from the store, as without tracking.
   With `Retain` the comment stays; its edges travel in `Deletion.Removed` (`IDeletedAnnotations.Keep` now lets
   `CommentMark` travel), `Revival` pastes the thread back with fresh ids and `Release` removes the originals; accept removes
   the thread for good. An edge whose partner survives is parked at the deletion point (the comment keeps its live part).
   DOCX wrote one `w:commentReference` per edge inside `w:del`, so the range came back as a point: the writer now writes
   `w:commentRangeStart` and `w:commentRangeEnd` + reference inside `w:del`, and the reader of `w:del` reads them.
3. **Medium (c)** - a field whose begin lay in a wholly removed paragraph and whose end survived was purged in the live text
   and came back from reject as plain text. With `Retain` field, comment and control parts held by removed paragraphs are no
   longer purged: if the whole range left (begin removed and end removed, through the removed tail and head of the boundary
   paragraphs or through removed paragraphs) its live remnants are stripped and it travels whole in `Removed`; otherwise the
   lost parts are parked at the deletion point, so the field stays whole and balanced while the deletion is pending and after
   reject.
4. **Medium (d)** - row, column and table deletion left a change edge whose partner survived as a lone mark (`ChangeRegions`
   ignored a lone end, a lone start became a point and the insertion was pruned). The lost start now goes to the start of the
   first surviving paragraph after it, the lost end to the end of the last surviving paragraph before it. The table discards
   are now one batch per command (`IRangeDeletion.Discard` takes the blocks): edges split across cells of the same row were
   judged one cell at a time.

`MarkPurge` was restructured instead of patched: one set of stripped keys replaces the per-kind sets, lost marks remember
their paragraph, "covered" ranges are computed for every spanning kind, and parking is shared by the caret and no-caret
paths.

## Tests

New: `RoundFortyFourRejectRestoreTests` (7, 6 failed before), `DocxParagraphMarkRevisionTests.A_frame_and_a_comment_held_by_a_pending_deletion_cross_docx`
(failed before: comment range came back as a point), `OdtParagraphMarkRevisionTests.A_frame_and_a_comment_held_by_a_pending_deletion_cross_ODT`,
`FolioReviewTests.RifiutareLaCancellazioneDiUnParagrafoRiportaIlCommentoEIlRiquadro`.
Runs: Documents.Tests 327, Documents.UI.Tests 312, Formats.OpenXml.Tests 2344, Formats.Odf.Tests 784, Architecture.Tests 40: all pass.

## Left open

- A comment, field or control half held by removed paragraphs keeps only its live part: the lost edge is parked at the
  deletion point, so after reject the range starts (or ends) at the deletion point, not at its original offset.
- Column deletion with a horizontal merge replaces the heir cell before the batch discard; a change edge inside the detached
  cell cannot find a nearest surviving paragraph and stays lost (as before).

## Try live

Track changes on; delete from mid-paragraph to mid-paragraph two below, the middle one holding a comment and a floating frame;
save DOCX and ODT, reopen, reject: text, comment with its range and frame return. Accept: both go, undo brings them back.
Delete a table row holding the start of a tracked insertion continuing after the table: the rest stays marked inserted.
