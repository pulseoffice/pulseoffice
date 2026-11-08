# Deep audit, round 17: the rest of the Folio editor folder and the round 16 minors

Scope: the files of `src/PulseOffice.Documents.UI/Editing` that round 16 (`folio-editor-round16.md`) did not read,
the `FolioEditor.Replace` caret minor and the column balancer minor in `Documents.Layout/Flow/Placement.cs`.
`src/PulseOffice.Sheets*` was not touched.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Documents.UI/Editing/FolioEditor.cs | 400–560, 226–245 | **minor, fixed**: defect 1; **medium, fixed**: defect 2 (`ObjectLocked`) |
| Documents.UI/Editing/FolioObjectMover.cs | 132/132 | protected through defect 2; no other defect (each move is one `ChangeObject` step) |
| Documents.UI/Editing/FolioObjectEditing.cs | 215/215 | **medium, fixed**: defect 3; **minor, fixed**: defect 4 |
| Documents.UI/Editing/CaretNavigator.cs | 97/97 | minor left: `Step` copies the story into a list for every arrow press (linear per key) |
| Documents.UI/Editing/FolioTypingAssist.cs | 112/112 | no defect: completion and auto-format go through the editor's transactions |
| Documents.UI/Editing/FolioNavigatorSource.cs | 149/149 | no defect; note: `Current` compares every heading before the caret on each refresh |
| Documents.UI/Editing/FolioReviewSource.cs | 224/224 | minors left: `Comments` looks up each root's anchor with a linear search (quadratic in the comments); `Describe` builds the whole text of a change before clipping it to 400 characters |
| Documents.Layout/Flow/Placement.cs | 155–244 | **medium, fixed**: defect 5 |

Not read in this round: the smaller services of the folder (`FolioCaretSpeech`, `FolioContextMenus`, `FolioDrawing`,
`FolioFieldRefresh`, `FolioImages`, `FolioKeyMap`, `FolioLinks`, `FolioPageStatus`, `FolioPageStyles`, `FolioProofing`,
`FolioRichTransfer`, `LayoutLines`, `ParagraphOrder`, `FolioObjectCycle`, `FolioObjectHits`, `FolioEditRefusals`,
`ContentControlPlaceholders`, `PendingCharacterFormat`, `FolioProtectedContentGuard`). They are left for the next round.

## Defects

1. **Minor** — `FolioEditor.Replace` (FolioEditor.cs:520) put the caret at `start + text.Length` in the start
   paragraph; with a line break in the replacement the caret landed inside the first paragraph instead of after the
   last inserted line. Fixed at the root: the end is a tracked position that follows the edit, like typing.
   Test `ReplacingWithALineBreakPutsTheCaretAfterTheLastLine` (failed before: `uno:1` instead of `p2:2`).
2. **Medium** — `FolioEditor.ObjectLocked` (FolioEditor.cs:236) only considered a locked document and generated
   paragraphs: an object anchored in a protected section could be moved, nudged, reanchored, reordered, captioned,
   linked and deleted. It now also asks the edit guard for the anchor paragraph, so every caller (mover, object
   editing, deletion, resize, rotation) refuses. Test `AnObjectAnchoredInAProtectedSectionDoesNotMove` (regression guard).
3. **Medium** — `FolioObjectEditing.Order`, `Caption` (on an object), `Link` and `Unlink` (FolioObjectEditing.cs:92,
   166, 187, 194) did not check `ObjectLocked` at all: z-order and chains changed in a locked document or protected
   section. Fixed. Test `AnObjectAnchoredInAProtectedSectionKeepsItsOrderAndCaption` (failed before).
4. **Minor** — `FolioObjectEditing.Rotate` (FolioObjectEditing.cs:99) accepted NaN and infinity, writing a NaN rotation
   into the model and the files. Non-finite angles are refused. Test `ANonFiniteRotationIsRefused` (failed before).
5. **Medium (performance)** — `ColumnBalancer.Atoms` (Placement.cs:207) composed every line of the rest of the
   section for every balanced page: quadratic on a long multi-column section. Gathering now stops once the atoms
   exceed the page capacity (`Available × columns`), where the answer is already `Available`; table groups are summed
   without `Skip/Take`. Tests `BalancingAPageDoesNotComposeTheWholeRestOfTheSection` (failed before: 5000
   paragraphs composed for one page) and `AShortSectionIsStillBalancedEvenly`.

No untranslated interface text was found in the files read.

## Tests run

- `PulseOffice.Documents.UI.Tests`: 291/291.
- `PulseOffice.Documents.Layout.Tests`: 200/200.
- `PulseOffice.Desktop.Tests` filtered on `Folio`: 18/18.

## To try live

- Find and replace with a replacement holding a line break: the caret ends after the inserted text.
- A picture anchored inside a protected section: dragging, arrow nudging, anchoring, arrange, caption and frame
  linking are refused.
- A long two-column balanced section (hundreds of pages): pagination stays fast and the last page is balanced.
