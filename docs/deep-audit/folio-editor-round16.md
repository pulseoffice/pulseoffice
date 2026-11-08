# Deep audit, round 16: the Folio editor and the minors left by round 14

Scope: `src/PulseOffice.Documents.UI/Editing` (document view, caret and selection, typing and IME, dragging,
clipboard, page rendering and invalidation, scrolling, zoom), the four minors left open by round 14
(`folio-core-round14.md`) and the partial nested deletion limit (`folio-nested-revisions.md`). Rounds 4 and 5
(`folio-ui-round4.md`, `folio-ui-round5.md`) were read first so as not to repeat them.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Documents.UI/Editing/FolioEditor.cs | 913/913 | **medium, fixed**: defect 1. Minor left: `Replace` computes the caret end as `start + text.Length`, wrong when the replacement holds a paragraph break (the clamp keeps it in range) |
| Documents.UI/FolioView.cs | 323/323 | no defect (text input, key routing, IME client wrapping, focus) |
| Documents.UI/Canvas/FolioCanvas.cs | 1134/1134 | no defect; note: the caret blink repaints the visible pages (not all pages) and `SelectionMap` walks the selected paragraphs on each paint, linear in the selection |
| Documents.UI/Editing/FolioEditingKeys.cs | 99/99 | no defect (undo and redo go through the effect guards) |
| Documents.UI/Editing/PreeditLayout.cs | 30/30 | no defect (offset clamped, state restored in `finally`) |
| UI.Controls/Text/Editing/InlineImeClient.cs | 74/74 | no defect |
| Documents/Controls/ContentControlEditing.cs | 130–245 | **minor, fixed**: defect 2 |
| Documents/Editing/ParagraphText.cs | 55–170 | **minor, fixed**: defect 3 |
| Documents/Editing/Tables/TableConversion.cs | 1–190 | **minor, fixed**: defects 4 and 5 |
| Documents/Revisions/TrackedEditing.cs | 197/197 | **medium, fixed**: defect 6 (the declared limit) |

Probed by tests and found sound: undo after typing, after a paragraph split and after a deletion across paragraphs
keeps the caret where the edit was (tracked positions), it never falls back to the top of the document.

Not read in this round (left for the next one): the other files of `Documents.UI/Editing` (`CaretNavigator`,
`FolioObjectMover`, `FolioObjectEditing`, `FolioReviewSource`, `FolioNavigatorSource`, `FolioTypingAssist`, the
smaller services) and `Documents.Layout/Flow/Placement.cs` (the column balancer minor stays as in round 14,
guarded by `ALongBalancedSectionIsLaidOutInLinearTime`).

## Defects

1. **Medium, fixed** (`FolioEditor.Type`): text committed while a composition was showing did not end the
   composition. An input method that commits without clearing its preedit first left the preedit drawn after the
   committed text, so the composed word appeared twice until the next IME event. Committed text now ends the
   composition. Test `FolioEditorRound16Tests.CommittedTextEndsTheComposition`.
2. **Minor, fixed** (`ContentControlEditing`): `Replace` looked every mark up again in the ordered list
   (`IndexOf` inside `Select`, quadratic in the marks at one offset; 7.5 s with 30,000 marks), and `Refill` walked
   the whole document again for each empty control (4.9 s for 6,000 controls). Ranks are now a reference-keyed
   dictionary; `Refill` uses the span it already has and rereads only the paragraph's own marks when an earlier
   control in the same paragraph was refilled. Tests `RoundSixteenCoreTests.FillingAControlBesideManyMarksTakesLinearTime`,
   `RefillingManyEmptyControlsWalksTheDocumentOnce`.
3. **Minor, fixed** (`ParagraphText.Delete`/`MarksRemovedBy`): removed marks were filtered with `List.Contains` and
   every control and comment looked its closing mark up linearly (17 s to delete a paragraph with 30,000 comments).
   Closings are gathered once in dictionaries and the removed set is a reference hash set. Test
   `DeletingAParagraphFullOfCommentsTakesLinearTime`.
4. **Minor, fixed** (`TableConversion.SplitTable`): splitting inside the heading left the first table with more
   heading rows than rows and the second without its heading rows. The first table keeps the heading rows above the
   cut, the second counts the heading rows it received (and copies the heading only when it received none). Test
   `SplittingATableInsideItsHeadingKeepsTheHeadingCountsWithinEachTable`.
5. **Minor, fixed** (`TableConversion.MergeWithNext`): the dropped duplicate heading rows took their notes and
   anchored objects with them without discarding them (orphan notes). They now go through `IRangeDeletion.Discard`
   in the same undo step. Constructor change declared: `TableConversion` takes `IRangeDeletion` (already registered;
   two test constructors updated). Test `MergingTablesWithTheSameHeadingDiscardsTheNotesOfTheDroppedHeading`.
6. **Medium, fixed** (`TrackedEditing.Delete`): a tracked deletion only partly covering an insertion was one plain
   deletion; Word splits it. The range is now cut at the edges of the insertions it touches and each piece is
   handled from the last to the first: inside one's own insertion it is removed for good, inside another author's it
   becomes a nested deletion, outside a plain one; all in one undo step. Tests
   `NestedRevisionTests.ADeletionThatPartlyCoversAnotherAuthorsInsertionIsSplitAtItsEdge`,
   `ADeletionThatPartlyCoversOnesOwnInsertionRemovesOnlyTheOwnPartForGood`.

Every test above failed before its correction (verified by reversing the patch, then reapplying it).

## Left
- Minor: `FolioEditor.Replace` caret end with a multi-paragraph replacement.
- Minor (round 14): column balancer atoms per page.
- Files of `Documents.UI/Editing` listed above as not read.

## Tests run
Documents.Tests 300/300, Documents.UI.Tests 287/287, Architecture.Tests 38/38, Formats.OpenXml.Tests
(nested revisions) 7/7.

## To try live
- Japanese or Chinese input in Folio: compose a word and commit it; it must appear once.
- With tracking on, as a second author select from plain text into another author's insertion and delete: two
  struck pieces, the inner one underlined in the deleter's colour; ⌘Z restores everything in one step.
- Split a table inside a three-row heading; merge two tables whose heading carries a footnote.
