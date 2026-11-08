# Deep audit round 32, part 2: word-processing format carriers

Continues [round-32-folio-formats.md](round-32-folio-formats.md): the carriers part 1 did not reach and its three open
points.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Formats.OpenXml/Wordprocessing/DocxIndexSettings.cs | 82/82 | clean (size cap, enum checks) |
| Formats.OpenXml/Wordprocessing/DocxIndexRanges.cs | 39/39 | clean |
| Formats.OpenXml/Wordprocessing/DocxPictureBullets.cs | 61/61 | clean |
| Formats.OpenXml/Wordprocessing/DocxTextBoxLook.cs | 43/43 | defect L1 |
| Formats.Odf/Odt/OdtListPictures.cs | 43/43 | defect P1 |
| Formats.OpenXml/Wordprocessing/DocxGroupMemberLinks.cs | 37/37 | clean |
| Formats.Odf/Odt/OdtProperties.cs (OdtFrameCodec, look) | 305-391 | clean |
| Documents.Layout/Flow/Paginator.cs (deferral, NewPage) | 170-240, 1248-1276 | clean |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs (sections) | 215-430 | clean |
| Formats.OpenXml/Wordprocessing/DocxFieldRanges.cs, DocxFieldInstructions.cs (w:fldData) | carriers | defect D1 (open point a) |
| Formats.OpenXml/Wordprocessing/DocxParagraphMarkSplits.cs, DocxParagraphMarkJoins.cs | 102/102, 87/87 | defect Q1 (open point b) |
| Documents/Revisions/TrackedRows.cs, Documents/Editing/TableEditing.cs | 66/66, 219/219 | defect R1 (open point c) |

## Defects

- **R1 (medium)** `TrackedRows.Location` found the row through the first top-level paragraph of its cells; a row whose
  cells hold only a nested table had none, so rejecting its insertion (or accepting its deletion) dropped the revision
  and left the row. Fix: `ITableEditing.LocateRow` finds the owner container of the table itself.
- **D1 (medium)** `FieldCodeMark.Data` (w:fldData of a field range, `pt:data` in ODT) had no size cap nor validation:
  any text, of any length, was kept and written back as `w:fldData`. Fix: the model keeps only base64 up to
  `FieldCodeMark.DataLimit` (128 KiB), like the native-data carrier of preserved fields.
- **Q1 (grave, DoS)** `DocxParagraphMarkSplits.AddMarks` counted joins for every mark of every segment (cubic): a
  paragraph joined from 6,000 deleted paragraph marks took 177 s to write. `DocxParagraphMarkJoins.Join` concatenated
  the growing paragraph once per piece (quadratic). Fix: marks are bucketed by segment once (binary search over the
  sorted joins), the deletion of each join is resolved once, and the joined parts are concatenated pairwise. Now
  0.3 s to write, 0.25 s to read (was 1.6 s). Test in the large-document collection.
- **P1 (medium, silent loss)** `OdtListPictures` named bullet pictures after the list style with non-ASCII characters
  folded to `_`; two list styles such as "Liste ü" and "Liste ö" got the same media path, and the session kept only
  the first picture, so the second style read back with the other's image. Fix: the media path is the SHA-256 of the
  picture data (identical pictures share one entry by design).
- **L1 (medium)** `DocxTextBoxLook.Write` passed any border width to `a:ln w`: a huge width (from an ODT `fo:border`)
  overflowed to 2147483013 EMU and a negative one was written negative, both invalid OOXML. Fix: clamped to the
  OOXML range 0-1584 pt.
- **S1 (medium, silent loss)** `DocxWriter` ignored the deferred page style of a paragraph that opens the document
  (e.g. after deleting the paragraphs before a continuous section): the layout shows it from page one, the DOCX lost
  it. Fix: the first section takes the deferred style.
- **S2 (medium, silent loss)** `DocxStoredPageStyles.UsedNames` ignored deferred page styles and `NextStyleName`
  chains; on reading, a stored unused style whose name equals the one given to a deferred section ("ConvertedN")
  replaced that section's style. Fix: used names include deferred styles and the follow-on chain.
- **Q2 (medium, slow write)** `DocxInlineWriter.Referenced` rescanned every paragraph of the document for each
  identifier of each conditional-text field: 8,000 conditional fields took 9.7 s to write. Fix: the referenced names
  (bookmarks and variable sets) are collected once per write in `DocxWriteContext.ReferencedNames`.

## Checked and clean

DocxIndexSettings (4 KiB cap, enum validation), DocxIndexRanges, DocxPictureBullets, DocxGroupMemberLinks, the frame
look of OdtFrameCodec, the deferral in Paginator (`_deferred` consumed by `NewPage`, dropped when an explicit style
follows), the `w:fldData` of simple and complex fields in DocxInlineReader/Writer (decoded only through the 128 KiB
capped native-data carrier).

## Tests

Documents: `ParagraphMarkRevisionTests.RejectingAnInsertedRowWhoseCellHoldsOnlyANestedTableDeletesTheRow`.
Formats.OpenXml: `DocxFieldRangeTests` (two), `DocxParagraphMarkChainPerformanceTests` (two, large-document
collection), `DocxTextBoxLookTests` (theory), `DocxDeferredSectionHeaderTests` (two).
Formats.Odf: `OdtPictureBulletTests.List_styles_whose_names_differ_only_in_punctuation_keep_their_own_pictures`.
Every test failed before its fix.

## Verification

Formats.OpenXml 2277+1, Formats.Odf 778, Documents 305, Documents.Layout 203, Architecture 40: all green. Corpus audit
(redline-bullet.docx, a corpus case, linked-bullet.odt, dml-picture-in-textframe.docx): LOSS 0, declared codes unchanged.
