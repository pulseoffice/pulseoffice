# Round 157: Folio track changes

Area: Folio tracked changes (verdicts, DOCX/ODT crossing, Merge Documents). Each fix has a test that fails on the
parent code and passes with the fix.

## Defects fixed

1. **Medium: DOCX lost the previous formatting of a format change spanning several runs.** `DocxInlineWriter`
   picked the `w:rPrChange` previous run by counting written runs, so when the change merged or split runs the
   wrong previous formatting was written and rejecting the change in Word or after reopening restored the wrong
   formatting. The writer now walks the previous runs by character offset (`FormatPieces`, `PreviousAt`) and splits
   output runs at previous-run boundaries. Test: `Round157TrackChangesTests`.
2. **Medium: Merge Documents dropped the style and character formatting of added paragraphs.** `DocumentComparison`
   inserted the other document's paragraphs as plain text. The merge now adopts the other paragraph's content
   (style, paragraph and run properties) onto the inserted paragraphs inside the same merge transaction; the change
   marks are kept because they live outside the paragraph content. Test: `Round157MergeTests`.
3. **Minor: Reinstate on a tracked table row acted like Reject.** `TrackedRows` had no reinstate path, so an inserted
   row was deleted and a deleted row was kept untracked. `ITrackedRows.Reinstate` now reverses the change on the
   same id with the current author: an inserted row becomes a tracked deletion, a deleted row a tracked insertion.
   Undo restores the original change. Test: `Round157RowReinstateTests`.

## Open items

- Merge Documents still applies word-level differences inside paired paragraphs as plain text (`MergeWords`):
  character formatting of the inserted words comes from the insertion point, not from the other document.
- Not audited in this round: accept and reject on paragraph property changes, moved text (move-from/move-to
  pairs), and undo of each verdict beyond rows.
- `RoundSixteenCoreTests.RefillingManyEmptyControlsWalksTheDocumentOnce` and
  `OpenXmlLargeDocumentPerformanceTests.A_group_of_twenty_thousand_shapes_reads_in_linear_time` failed under the
  load of four parallel auditors, with and without these fixes. They are timing tests and are not related to this round.
