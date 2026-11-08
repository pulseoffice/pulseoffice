# Deep audit round 35: Folio model, layout and UI of the declared-loss blocks

## Coverage

| File | Outcome |
|---|---|
| src/PulseOffice.Documents/Revisions/TrackedRows.cs | read in full; resolution runs inside the resolver's transaction, one undo step |
| src/PulseOffice.Documents/Blocks/TableBlock.cs (TableRow) | `Clone` keeps `ChangeId` (snapshot/undo need it) |
| src/PulseOffice.Documents/Editing/DocumentEdit.cs (RowState) | `ChangeId` captured and restored on undo |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | pasted rows drop `ChangeId`, as pasted text drops change marks: correct |
| src/PulseOffice.Documents/Editing/TableEditing.cs (InsertRow/DeleteRow) | new rows do not inherit `ChangeId`: correct |
| src/PulseOffice.Documents.UI/Editing/FolioReviewSource.cs | defect 1 |
| src/PulseOffice.Documents.Layout/Flow/Paginator.cs (deferred page style) | break, empty page and restart paths checked: no defect |

## Defects

1. **Medium** - `FolioReviewSource.Go` used the list pattern `[var first, .., var last]`, which needs at least two
   paragraphs: Go to on a tracked table row with a single paragraph (a one-cell row) did nothing. Fixed with a count
   check; test `FolioReviewTests.GoingToATrackedRowWithASingleParagraphSelectsIt`.

## Not covered in this round

FieldCodeMark editing, ListLevel.Picture drawing, TextBoxContent.Look, GroupChild.Hyperlink, DimensionLine and
Scene3DObject UI paths remain for a later round.
