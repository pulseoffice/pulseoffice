# Folio: deletions nested in another author's insertion (2026-11-05)

Open point left by round 14 (`TrackedEditing.Delete`): with tracking on, deleting text inserted by another reviewer
removed it with no trace. User decision: behave like Word (CLAUDE.md, decision 24).

## Model
- `Deletion.WithinInsertion` (cloned, so undo snapshots and auto-redaction keep it).
- `TrackedEditing.Delete`: a range covered by an insertion of the same author is removed for good (unchanged); covered by
  another author's insertion it becomes a deletion nested in it. Backspacing merges only deletions with the same host.
- `ChangeMarking.Prune` keeps an emptied insertion while a deletion refers to it; `Span` re-marks a change's extent;
  `Release` turns nested deletions into plain ones.
- `RevisionResolver`: accept releases nested deletions of an accepted insertion, then prunes; rejecting an insertion
  removes its nested deletions; rejecting a nested deletion pastes the text back and extends the host insertion over it.
  Every verdict stays one undo step.

## Formats
- DOCX: the writer wraps the nested `w:del` pieces in the host's `w:ins`; the reader sets `WithinInsertion` for a
  `w:del` met inside a `w:ins`.
- ODT: ODF 1.3 has no nested changed regions; the deletion region carries `pt:within-insertion` (namespace
  `urn:pulseoffice:text`) on `text:deletion`. No loss code is needed: our files round-trip, other readers see a plain
  deletion at the same point.

## UI
- Layout: nested deleted text is struck and underlined in the deleter's colour (`InlineInsertion.Underlined`).
- Review panel: the item shows "in the insertion by …" (`review.nested`, five languages) through `ReviewItem.Place`.

## Tests (all failing before the change, except the UI place test written with it)
- `tests/PulseOffice.Documents.Tests/NestedRevisionTests.cs` (15): 11 failed before.
- `tests/PulseOffice.Formats.OpenXml.Tests/NestedRevisionFormatTests.cs` (7): DOCX structure, DOCX and ODT round trips,
  ODT then DOCX; all 7 failed before.
- `ChangeMarkupTests.ADeletionNestedInAnotherAuthorsInsertionIsStruckAndUnderlinedInTheDeletersColour`.
- `FolioReviewTests.ADeletionNestedInAnotherAuthorsInsertionSaysWhoseInsertionItIsIn`.

Projects run: Documents.Tests 293/293, Formats.OpenXml.Tests 2162/2162, Formats.Odf.Tests, Documents.Layout.Tests
198/198, Localization.Tests 32/32, Documents.UI.Tests (review and auto-redaction filters) 12/12.

## Limits
- ~~A deletion that only partly overlaps another author's insertion is still one plain deletion~~: closed in round 16
  (`docs/deep-audit/folio-editor-round16.md`): the range is cut at insertion edges and each piece is handled on its own.
- Not yet seen live in Folio nor in Word.
