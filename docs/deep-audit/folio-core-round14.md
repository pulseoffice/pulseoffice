# Deep audit round 14: Folio core

Area: `PulseOffice.Documents` (model, editing, fields, sections, notes, revisions, tables) and `PulseOffice.Text*` plus
`PulseOffice.Documents.Layout` (rich-text pagination, line breaking, hyphenation, page layout). Starting point:
[folio.md](folio.md) and [folio-round2.md](folio-round2.md), which read the editor, `FlowBuilder`, `ListLabeling`, the
note, comment and object collections and the main editing files; this round concentrates on the large central files
those rounds did not read in full, above all the paginator and the line machinery.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| Documents.Layout/Flow/Paginator.cs | 1265/1265 | **severe, fixed** (in `ParagraphEntry`): see defect 1. Loops checked for progress: forced page breaks on an empty page, style reopening (geometry keeps the requested name), pending notes (always at least one atom on a fresh page), split rows (forced one atom when the column is empty), wrapped lines (`_y` advances by at least 1 pt) |
| Documents.Layout/Flow/ParagraphEntry.cs | 119/119 | **severe, fixed**: defect 1 |
| Documents.Layout/Lines/LineBreaker.cs | 153/153 | no defect (greedy, so a suffix composition equals the suffix of the whole composition) |
| Documents.Layout/Lines/LineComposer.cs | 153/153 | **severe, fixed**: defect 2 |
| Documents.Layout/Inline/ShapedParagraph.cs | 102/102 | no defect |
| Documents.Layout/Inline/ParagraphShaper.cs | 147/147 | no defect |
| Documents.Layout/Inline/RunTypesetting.cs | 120/120 | no defect (case mapping is per character, lengths kept) |
| Documents.Layout/Inline/InlineInserter.cs | 75/75 | no defect (runs stay ordered, which defect 2's binary search relies on) |
| Documents.Layout/Flow/PageFrames.cs | 274/274 | no defect (geometry keeps the requested style name, so `Reopen` cannot loop) |
| Documents.Layout/Flow/LayoutEngine.cs | 347/347 | no defect (page-count and note-position passes capped at four) |
| Documents.Layout/Flow/Stories.cs | 394/394 | **medium, fixed**: defect 3 |
| Documents.Layout/Flow/Placement.cs | 244/244 | minor, left: the column balancer collects every remaining atom of the section on each new page; measured 1 s for a 400-page balanced section, kept as a regression test (`ALongBalancedSectionIsLaidOutInLinearTime`) |
| Documents/Editing/RangeDeletion.cs | 235/235 | **medium, fixed**: defect 4 |
| Documents/Revisions/TrackedEditing.cs | 195/195 | medium, left: defect 7 |
| Documents/Controls/ContentControlEditing.cs | 245/245 | minor, left: `Replace` looks every mark up again in the ordered list (`IndexOf` inside `Select`), quadratic in the marks of one paragraph; `Refill` finds every control again by id |
| Text.Rich/RichTextInsertion.cs | 79/79 | no defect (the merge noted by round 2 now goes through the linear `RunMerger`) |
| Text.Rich/RichText.cs | 316/316 | no defect |
| Documents/Editing/ParagraphText.cs | 260/260 | minor, left: `Delete` filters the removed marks with `List.Contains` and `MarksRemovedBy` looks up each control's closing mark linearly, quadratic only in the marks of a single paragraph |
| Documents/Editing/DocumentEdit.cs | 115/264 (lines 150–264, the recorder and the edit) | no defect |
| Documents/Editing/TableEditing.cs | 197/197 | **medium, fixed**: defects 5 and 6 |
| Documents/Editing/Tables/TableCellEditing.cs | 60/150 (merge) | no defect (merged cells move their blocks into the origin, nothing is orphaned) |
| Documents/Editing/Tables/TableConversion.cs | 80/235 (lines 100–179, split and merge of tables) | minor, left: splitting a table inside its heading keeps `HeaderRows`; merging two tables with the same heading drops the second heading's rows without discarding their notes or objects |
| Documents.Layout/Lines/LineSpacing.cs | 44/44 | no defect (non-finite and non-positive factors fall back) |
| Text.Rich/TextEditSplice.cs | 145/145 | no defect |
| Text.Rich/ParagraphLines.cs | 157/157 | **medium, fixed**: defect 8 |
| Text.Rich/FirstLineBreaker.cs | 80/80 | **medium, fixed**: defect 8 |
| Text/TextCase.cs | 206/206 | no defect (case mappings keep the length; `DocumentEditor.TransformText` refuses any transform that changes it) |

## Defects

### Severe (fixed)
1. `Documents.Layout/Flow/ParagraphEntry.cs:109` (`LinesFrom`): a paragraph split across pages was recomposed from
   the split point on every page, although the whole paragraph had already been composed at the same width
   (`Paginator.VisitParagraph` asks for `Lines(width)` on every split). A single paragraph of 120,000 words over
   267 pages took 11.2 s. Since breaking is greedy, the lines from a split point are exactly the suffix of the whole
   composition: `LinesFrom` now returns a view on that suffix when the split point is a line start of the whole
   composition, and composes only otherwise (lines placed around obstacles). 0.1 s. Test
   `RoundFourteenLayoutTests.AParagraphSpanningHundredsOfPagesIsComposedInLinearTime`.
2. `Documents.Layout/Lines/LineComposer.cs:127` (`Extent`): every line scanned every run and every inline glyph of the
   paragraph, O(lines × runs). A paragraph of 40,000 alternating bold runs (100 pages) took 68.9 s to lay out, and the
   editor relayouts on every keystroke. Now the runs touching the line are found by binary search (runs are ordered)
   and the inline glyphs by position within the line. 0.2 s. Test
   `RoundFourteenLayoutTests.AParagraphWithManyRunsIsComposedInLinearTime`.

### Medium (fixed)
3. `Documents.Layout/Flow/Stories.cs:199` (`ComposeFrom`): the cursor handed to the next frame of a chain was the
   source offset of the first line that did not fit (`ComposedLine.Start`, mapped back to the paragraph text), but it
   is read back as an offset in the shaped text, which also holds one placeholder per inline object, note citation
   and tracked deletion. With an image as character before the break the next frame repeated the last character
   (one per placeholder). The cursor is now the shaped end of the last placed line. Test
   `RoundFourteenLayoutTests.ChainedFramesResumeAfterTheLastCharacterEvenWithAnInlineObjectBeforeIt`.
4. `Documents/Editing/RangeDeletion.cs:170` (`Purge`): every comment or content-control mark removed by a deletion
   swept all the paragraphs of the document to drop the other edge of the range, O(comments × paragraphs): deleting a
   20,000-paragraph range with 5,000 comments took 4.6 s. The deletion now collects the removed comments (with their
   threads) and controls in a `MarkPurge` and sweeps the paragraphs once at the end; `RemoveText` and `Discard` keep
   their public behaviour. Tests `DeletingALongCommentedRangeTakesLinearTime`,
   `UndoingADeletionAcrossCommentsRestoresCommentsAndTheirMarks`.
5. `Documents/Editing/TableEditing.cs:84` (`DeleteRow`, `DeleteColumn`, `DeleteTable`): the cells removed with a row,
   a column or the whole table took their footnotes, comments, anchored objects and content controls with them only
   as marks: the notes, comment threads and objects stayed in the document, orphaned (saved into the file, counted by
   statistics, listed in the navigator). The cells are now discarded through `IRangeDeletion.Discard`, inside the same
   undo step. Tests `DeletingARowRemovesTheNotesOfItsCellsAndUndoBringsThemBack`,
   `DeletingAColumnOrTheTableRemovesTheNotesOfItsCells`.
6. `Documents/Editing/TableEditing.cs:103`: deleting a header row left `HeaderRows` unchanged, so the first body row
   became a repeated heading; inserting a row inside the heading pushed the last heading row into the body. The count
   now follows the rows. Test `DeletingAHeaderRowDoesNotTurnABodyRowIntoAHeader`.

8. `Text.Rich/ParagraphLines.cs:37` and `Text.Rich/FirstLineBreaker.cs:21` (shared with Deck and Ledger text boxes,
   justified and tabbed paragraphs): every line copied the rest of the paragraph (`content[from..]`), listed every
   break opportunity of the rest and measured the whole rest, then binary-searched over all of it, so each line cost
   time proportional to the remaining text: a justified paragraph of 30,000 words took 16.5 s to break. The breaker
   now works at an offset in the original string, enumerates break opportunities lazily and gallops from the line
   start (1, 3, 7, … candidates) before the binary search, measuring the whole rest only when every opportunity
   fits. The result is the same greedy choice. Tests `RoundFourteenLineTests` (timing, equality of offset and
   substring breaking over every start, greedy property).

### Medium (left, decision needed)
7. `Documents/Revisions/TrackedEditing.cs:86`: with change tracking on, deleting text that lies inside a pending
   insertion removes it outright, whoever the author of the insertion is. For one's own insertion this is right; for
   another reviewer's insertion it erases their proposal without a trace. Fixing it needs a deletion nested inside an
   insertion, which the change model, the change map and both formats would have to carry: left for a decision.

## Summary

- Files read: 27 (about 6,900 lines), 24 of them in full; partial reads are marked in the coverage table.
- Fixed with a test that failed first: severe 2 (pagination of long paragraphs, per-line run scan), medium 6
  (chained frame cursor, comment purge on deletion, orphaned notes and objects when deleting table rows, columns and
  tables, header-row count, quadratic line breaking of shared justified text). Measured: 11.2 s → 0.1 s, 68.9 s →
  0.2 s, 4.6 s → under 0.1 s, 16.5 s → under 0.1 s.
- Left: medium 1 (deleting another reviewer's insertion while tracking, defect 7, needs a model decision); minor 5
  (balancer atoms per page, content-control mark lookups, paragraph mark lookups, table split and merge heading
  corner cases).
- Constructor change declared: `TableEditing` now takes `IRangeDeletion` (DI already registers it); the five test
  sites that built it by hand were updated.
- Tests run, one project at a time: Documents.Layout.Tests 197, Documents.Tests 278, Text.Rich.Tests 103,
  Text.Tests 32, Documents.UI.Tests 282, Architecture.Tests 38: all green.

## To try live
- Paste a novel as a single paragraph (or open a DOCX whose chapter is one paragraph with many formatting changes):
  the pages appear at once and typing does not lag.
- Two chained text frames, the first holding an image as character: the second frame continues with the next
  character, no letter repeated.
- Select a long reviewed passage with many comments and delete it; undo brings everything back.
- In a table, put a footnote in a cell and delete the row (then the column, then the table): the note disappears
  from the notes and from the navigator; undo restores it. Delete a repeated header row: the next row is not
  repeated on the following pages.
- In Deck, a justified text box with a very long paragraph: editing stays fluid.
