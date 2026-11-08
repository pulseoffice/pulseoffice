# Deep audit round 43: Folio editing core (paired marks, deletion, paste, revisions, tracked rows)

Follows [round 35b](round-35b-folio-editing.md), [round 36](round-36-bullets-links.md), [B9](losses-b9.md).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Documents/Editing/RangeDeletion.cs (with `MarkPurge`) | 267/267 | read in full; see defects |
| src/PulseOffice.Documents/Editing/ParagraphText.cs | 282/282 | read in full; see defects |
| src/PulseOffice.Documents/Marks/InlineMark.cs | 142/142 | read in full; see defects |
| src/PulseOffice.Documents/Clipboard/FragmentExtractor.cs | 139/139 | read in full; see defects |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | 205/205 | read in full; see defects |
| src/PulseOffice.Documents/Revisions/RevisionResolver.cs | 173/173 | read in full; see defects |
| src/PulseOffice.Documents/Revisions/TrackedRows.cs | 63/63 | read in full; see defects |
| src/PulseOffice.Documents/Editing/TableEditing.cs | 247/247 | heir cell of a vertical merge lost `Protected` and `NumberFormat` |
| src/PulseOffice.Documents/Revisions/ChangeMarking.cs | 70-160 | orphan change edges are ignored by `ChangeRegions` |
| src/PulseOffice.Documents/Revisions/TrackedEditing.cs | 30-260 | `Removed` stripped every mark |
| src/PulseOffice.Documents/Revisions/ChangeRegions.cs | 45-90 | a lone `End` is skipped, a lone `Start` becomes a point region |

## Defects

1. **Serious** - a tracked deletion purged footnotes, endnotes and inline objects from their stores and `TrackedEditing.Removed`
   stripped every mark from the deleted content, so rejecting the deletion brought the text back without its note, its inline
   object, its fields and its content controls (only undo recovered them). The readers already keep notes referenced from a
   pending `Deletion.Removed` in the store and both writers write them. Now `IRangeDeletion`/`IDirectDocumentEditor.Delete` take
   an `AnnotationFate`: tracked deletions use `Retain` (notes and inline objects stay in their stores), and the new
   `IDeletedAnnotations` keeps in `Removed` the note, object, field and control marks that left the live text. Rejecting or
   reinstating pastes them back with their notes and objects (fresh ids through the remapper, the originals released in the same
   undo step); accepting the deletion, rejecting the insertion that holds a nested deletion (decision 24) and removing an orphan
   deletion with its tracked rows release them. Merging adjacent own deletions (`Joined`) no longer drops marks. Tests
   `TrackedDeletionAnnotationTests` (4, all fail before), DOCX round trip
   `A_footnote_inside_a_pending_deletion_made_by_editing_crosses_docx`.
2. **Medium** - `ParagraphText.Split` at a collapsed range (start and end at the same offset) sent the start right and kept the
   end left: splitting a paragraph, or pasting through a split (as rejecting a deletion does), left an end before its start
   (bookmarks, fields, controls, comments, change ranges). The start and the inner field parts of a range collapsed at the split
   offset now stay with the end. Tests `SplittingAtACollapsedBookmarkKeepsItsStartBeforeItsEnd`,
   `RejectingATrackedDeletionNeverLeavesABookmarkEndBeforeItsStart`.
3. **Medium** - deleting across paragraphs dropped the bookmark, index entry and change edges held by wholly removed paragraphs
   while the other edge survived: orphan `bookmark-end` in ODT and DOCX, and a tracked insertion whose start was lost became a
   point region and was pruned on accept (its remaining text silently untracked). `MarkPurge` now records the lost edges: with
   a caret (range deletion) an edge whose partner survives is put back at the deletion point, as a deletion inside one paragraph
   already does; without one (row, column, table deletion) the surviving bookmark or index edge is removed with it. Tests
   `DeletingTheParagraphThatHoldsABookmarkStartMovesTheStartToTheDeletionPoint`,
   `DeletingTheParagraphThatHoldsATrackedInsertionStartKeepsTheRestOfTheInsertionTracked`,
   `DeletingATableRowThatHoldsABookmarkStartDropsTheWholeBookmark`.
4. **Medium** - a deletion across paragraphs that covered a field spanning them (begin in the first paragraph, end in the last)
   left an empty field: round 35b's covered rule only looked inside one paragraph. `MarkPurge.Reach` now purges a field whose
   begin lies in the removed tail of the first paragraph and whose end lies in the removed head of the last one. Test
   `DeletingAcrossParagraphsAFieldThatSpansThemRemovesTheField`.
5. **Minor** - deleting the top row of a vertical merge made a new origin cell (`TableEditing.Heir`) without `Protected` and
   `NumberFormat`: the merged cell lost its protection. Test
   `DeletingARowUnderAVerticalMergeKeepsTheProtectionAndNumberFormatOfTheMergedCell`.

## Checked without defects

- `FragmentExtractor.Balance` and `FragmentRemapper` (fresh field and control ids, unique bookmark names, change marks dropped,
  tracked row ids dropped with them) after round 35b.
- `RevisionResolver` accept/reject of nested deletions: rejecting an insertion removes its nested deletions; accepting it
  releases them (`ChangeMarking.Release`); `Prune` keeps an empty insertion while a deletion names it.
- `TrackedRows.Resolve`: deleting every tracked row of a table falls back to `DeleteTable`; later rows are located by index
  each time.
- Undo/redo: every new change goes through the same `EditRecorder` step (paragraph touches and store snapshots).

## Left open

- Floating objects anchored to a paragraph wholly removed by a tracked deletion are still purged (`RemoveObjectsAnchoredTo`):
  the removed paragraphs get fresh ids in `Removed`, so the anchor has nothing to follow; rejecting does not bring them back.
- Comments whose anchor is deleted with tracking on are removed (as without tracking); a pending deletion does not carry them.
- A field whose begin alone lies in a wholly removed paragraph is still purged entirely (round 35b), and a tracked deletion of
  half a field carries neither half, so rejecting brings the text back as plain text.
- Without a caret (row or table deletion) a lost change edge is left as is: `ChangeRegions` ignores the lone end.
- `RoundSixteenCoreTests.RefillingManyEmptyControlsWalksTheDocumentOnce` (2 s budget) fails in the whole project run while a
  corpus run loads the machine, and passes alone.

## Tests run

Documents.Tests 320 (one timing test under load, see above), Documents.UI.Tests 311, Formats.OpenXml.Tests 2340, Formats.Odf.Tests
783, Architecture.Tests 40.
