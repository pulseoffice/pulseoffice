# Deep audit: Folio, second round

Area: `PulseOffice.Documents`, `PulseOffice.Documents.Layout`, `PulseOffice.Documents.UI` and the word-processing formats
(`Formats.Odf/Odt`, `Formats.OpenXml/Wordprocessing`). Starting point: [folio.md](folio.md) (first round).

Method: first the open points left by the first round that are minor on paper but risky (input read from files), then a
full re-reading of the files changed by the first round's fixes, looking for regressions (quadratic costs, behaviour
changed silently, swallowed exceptions), then the largest and most central files of the editor and the layout.

## Open points of the first round closed

| Point | Severity now | Outcome |
|---|---|---|
| `Documents/TextDocument.cs` `IdSequence.Reserve` with `p2147483647` | medium | **fixed**: counters are `long`; an identifier beyond `long.MaxValue / 2` is ignored (it can never collide with generated ones). Test `RoundTwoLimitsTests` |
| `Documents.Layout/Flow/ListLabeling.cs` `LetterNumbers` for huge values | **severe** | **fixed**: the ODT reader did not bound `text:start-value` of the notes configuration (`OdtStylesReading.Notes`, unlike lists and DOCX), so a footnote numbered with letters starting at 2,147,483,647 built an 82-million-character label for every note on every layout. Now the reader clamps to 0..65535 like the lists, and the formatter falls back to digits beyond 30 repeated letters. Tests `LetterNumberLimitsTests`, `OdtNoteStartLimitTests` |
| `Documents/Styles/Styles.cs` `ListStyle.Level` with no levels | minor | **fixed**: answers with a default arabic level instead of `Math.Clamp(level, 0, -1)` throwing |
| `Documents/Editing/Tables/TableGrid.cs` `Whole` on a table without rows | minor | **fixed**: `Max` on an empty sequence no longer throws |

## Files changed by the first round, re-read in full

| File | Lines | Outcome |
|---|---|---|
| Documents/Editing/TextSearch.cs | 98/98 | no defect (match length from `IndexOf`, progress guaranteed by `Math.Max(end, at + 1)`) |
| Documents/Annotations/Comments.cs | 74/74 | no defect (thread walk iterative with a visited set) |
| Documents/Editing/Tables/TableSorting.cs | 61/61 | no defect |
| Documents/Editing/Tables/CellNumberFormatting.cs | 119/119 | no defect |
| Documents/Clipboard/FragmentRemapper.cs | 198/198 | no defect |
| Documents/Indexes/IndexEntries.cs | 175/176 | minor, **fixed**: the bibliography collected citations with `List.Contains` (quadratic in the number of citations); now a set |
| Documents/Editing/Tables/TableConversion.cs | 235/235 | minor, left: `Pieces` (introduced by the first round to skip separators inside fields) splits and restarts the scan from the start for every separator, O(separators × length) per paragraph |
| Documents/Fields/FieldSources.cs | 243/243 | no defect |
| Documents/Fields/FieldCondition.cs | 282/289 | **severe, fixed**: a variable set field `a + a` repeated N times in a document (or a chain of variables each doubling the previous one) doubled the text at every field: 40 fields were 10 TB. Text concatenation is now limited to 65,536 characters; beyond it the expression is invalid (the field keeps its text, as for any invalid expression). Test `ARepeatedlyDoubledVariableDoesNotGrowWithoutLimit` |
| Documents/Editing/DocumentEditor.cs | 418/418 | minor, left: `InsertText` with many lines looks the paragraph up again for every line (`stories.Find`), costly only for very large pastes |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 427/427 | minor, left: `Link` rebuilds the whole object list for every member of a text-box chain (quadratic in chain length × objects) |
| Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 722/727 | **medium, fixed**: `Merge` joined equal runs by repeated string concatenation; a paragraph split by Word into tens of thousands of runs with the same formatting (common with revision-id splits) cost quadratic time: 60,000 runs took 5.6 s. Now one `StringBuilder` per merged run (0.8 s, mostly parsing). Test `DocxManyRunsTests` |

### Same root found elsewhere
- `Documents/Editing/ParagraphText.cs` `Normalize`, called by every edit: the same repeated concatenation; clearing the
  style of a paragraph of 80,000 runs took 10.5 s. **Medium, fixed** the same way; test
  `ClearingTheStyleOfVeryManyRunsMergesThemInLinearTime`.
- `Text.Rich/RichTextInsertion.cs` `Merge` has the same pattern; outside this area (shared with Deck), noted for its owner.

## Round-2 reading continued

| File | Lines | Outcome |
|---|---|---|
| Formats.Odf/Odt/OdtBodyReading.cs | 962/962 | **severe, fixed**: `<text:s text:c="100000"/>` was clamped per element only; a few kilobytes of repeated elements expanded to tens of gigabytes of spaces (300 elements = 30 million characters). Now a per-document budget of 10 million expanded spaces in `OdtReadContext.TakeSpaces`, like the existing repetition budget. Test `RepeatedSpaceElementsCannotExpandWithoutLimit`. Minor, left: an index-mark end rebuilds the whole paragraph text (`Collected`) for every end mark |
| Formats.Odf/Odt/OdtBodyWriting.cs | 841/841 | **severe, fixed** (in `OdtColumnsCodec.Read`): `fo:column-count` was not bounded, unlike DOCX (45); a page or section with two billion columns made the paginator allocate one slot per column and crash. The bound is now `SectionColumns.LargestCount`, shared by both readers. Test `AHugeColumnCountIsBoundedLikeInDocx`. Minor, left: `decimal-places` of cell data styles is not bounded when read back (display clamps it to 10) |
| Formats.Odf/Odt/OdtStylesWriting.cs | 268/268 | no defect |
| Formats.OpenXml/Wordprocessing/DocxEmbeddedFonts.cs | 154/154 | minor, left: the traits list is searched linearly for every font of the table |
| Documents/Objects/AnchoredObject.cs (collection) | 256/256 | **medium, fixed**: `AnchoredTo` and `Find` scanned every object (with a visitor allocation each) and are called once per paragraph by the layout (`FlowBuilder`), the ODT and DOCX writers and the readers: O(paragraphs × objects) on every layout and save. The collection now keeps lazy indexes by id and by anchor paragraph, invalidated on add, remove, replace and on every anchor change (`AnchorChanged`, unhooked on removal). Tests `AnchoredObjectIndexTests` |
| Documents.Layout/Flow/ListLabeling.cs | 140/143 | **severe, fixed**: the counters per list have ten slots (`ListStyle.LevelCount`), but the level was clamped only to the number of levels of the style; an ODT list style declaring more than ten levels with a paragraph nested deeper than ten made the layout throw `IndexOutOfRangeException` (the document could not be shown). Now the level is clamped to both; the ODT reader also keeps only levels 1–10 (ODF's range) and clamps the nesting depth of list items to the last level, as the writer already did. Test `AListStyleWithMoreThanTenLevelsDoesNotBreakTheLayout` |
| Documents.UI/Editing/FolioEditor.cs | 893/893 | no new defect (the first round's guards hold: typing, deleting, pasting and cutting check `Shielded` on the whole selection). Minor, left: `MoveToCell` would index an empty row of an irregular table |
| Documents.Layout/Flow/FlowBuilder.cs | 338/338 | **medium, fixed** (in `Documents/Annotations/Notes.cs`): note numbering and every paragraph with a note mark called `Notes.Find`, a linear scan, so a book with thousands of footnotes cost O(notes²) on every layout (30,000 lookups: 1.5 s). The collection now keeps a lazy index by id (ids are immutable), invalidated on add, remove, replace and copy. Test `FindingEveryNoteOfALongBookCostsLinearTime` |
| Documents/Annotations/Comments.cs (second pass) | 99/99 | **medium, fixed**: same linear `Find` as notes, called per comment range by the DOCX reader and per thread; indexed the same way. Test `FindingEveryCommentOfAReviewedDocumentCostsLinearTime` |

## Summary of the second round

- Files read in full: 21 (about 9,600 lines), plus the targeted checks on the readers' numeric attributes.
- Defects fixed with a test that failed first:
  - severe (6): field variable concatenation doubling without limit (`FieldCondition`), ODT note numbering start
    unbounded with letter labels (`OdtStylesReading`, `ListLabeling`), ODT `text:s` expansion without a budget
    (`OdtBodyReading`), ODT column count unbounded (`OdtColumnsCodec`), list levels beyond the tenth crashing the layout
    (`ListLabeling`, ODT list reading);
  - medium (6): `IdSequence` overflow, quadratic run merging in the DOCX reader and in every paragraph edit
    (`DocxInlineReader`, `ParagraphText`), linear object, note and comment lookups called once per paragraph
    (`AnchoredObjectCollection`, `NoteCollection`, `CommentCollection`);
  - minor (3): empty list styles, empty tables in `TableGrid.Whole`, quadratic bibliography collection.
- Minor left and noted: `TableConversion.Pieces`, `DocumentEditor.InsertText` per-line lookups, `DocxReader.Link`,
  index-mark end rebuilding the paragraph text, unbounded data-style decimals, `DocxEmbeddedFonts` traits scan,
  `MoveToCell` on an empty row, `RichTextInsertion.Merge` (outside the area).
- Regressions of the first round: none found in the re-read files; the one new quadratic cost it introduced
  (`TableConversion.Pieces`) is minor.

## To try live
- Open a large DOCX converted from a long book (thousands of footnotes, images and comments): opening, scrolling and
  saving as ODT and DOCX should be noticeably faster.
- Clear the formatting of a very long paragraph pasted from the web: no freeze.
- An ODT with a list nested twelve levels deep opens and shows the labels.
