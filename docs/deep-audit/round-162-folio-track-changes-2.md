# Round 162: Folio track changes (second pass)

Area: the parts of Folio track changes that round 157 left open. Tests and builds are run by the coordinator after
the four audits of the cycle are joined; none was run during this round.

## Defects fixed

1. **Medium: Merge Documents applied word-level differences as plain text.** `DocumentComparison.MergeWords`
   inserted the other document's words with `InsertText`, so their character formatting came from the insertion
   point and a soft line break in them split the paragraph. The words are now pasted as a tracked one-paragraph
   fragment sliced from the other paragraph's runs (`ITrackedEditing.Paste`). Test:
   `Round162TrackChangesTests.Merged_words_keep_the_formatting_of_the_other_document` (and a second test checking
   style and runs of added paragraphs together with reject-all, over round 157's `Adopt`).
2. **Medium: rejecting a tracked list removal read from DOCX did not bring the numbering back.** The previous state
   of `w:pPrChange` kept the list name and level but not the numbered/bulleted kind nor the list thread, so the
   restored paragraph was not a list item. `DocxBlockReader` now builds the list properties of the current and the
   previous paragraph with the same `Listed` step. Test:
   `Round162DocxMoveTests.A_list_removed_with_tracking_keeps_its_numbering_in_the_previous_paragraph`.
3. **Minor: DOCX moved text was imported silently as unrelated changes.** `w:moveFrom`/`w:moveTo` (runs and paragraph
   marks) become a tracked deletion and a tracked insertion, which accept and reject independently and are written
   back as `w:del`/`w:ins`. The loss is now declared as `DOCX224`; the grouped-shape (`DOCX120`) and field-instruction
   (`DOCX117`) revision checks also count move runs. ODF has no move change, so ODT is unaffected. Test:
   `Round162DocxMoveTests.Moved_text_is_read_as_deletion_and_insertion_and_the_loss_is_declared`.

## Checked clean

- Accept and reject of paragraph format changes made in the app: the whole previous `TextParagraph` (style, indents,
  alignment, list) is captured and restored inside one history step, so undo and redo of the verdict hold.
- Verdicts and accept/reject all each run inside one history transaction (`RevisionResolver`), rows included.
- Reinstate on format and paragraph format changes falls back to reject, as in Word.

## Open

- Moves are not modelled: a native move change pairing source and destination would remove `DOCX224`.
- Rejecting a character format change whose region spans several paragraphs (possible from ODT or DOCX input) only
  removes the mark; the previous formatting is restored only for single-paragraph regions.
- Tracked changes inside headers, footers, notes, text boxes and comments were not audited in depth this round.

## Status

Three defects fixed (two medium, one minor). Awaiting the coordinator's build and test run.
