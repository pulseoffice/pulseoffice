# Round 186: Folio tables

Area: Folio tables (row and column editing, merge and split, sort, conversion, heading rows, row breaking, the DOCX
and ODT table round trip). Static review, no builds or test runs inside the round; the coordinator runs the tests at
the join.

## Defects fixed

1. **Medium: DOCX rows with `w:gridBefore` lost their column placement.** The reader ignored `w:trPr/w:gridBefore`,
   so a row that starts after skipped grid columns had its cells shifted left into the wrong columns (and vertical
   merges below could attach to the wrong origin). The reader now puts that many borderless empty cells at the start
   of the row. Test: `Round186DocxGridBeforeTests`.
2. **Medium: a non-repeating table heading could not cross DOCX.** The writer marked every heading row with
   `w:tblHeader`, so a heading set not to repeat repeated in Word and came back repeating; DOCX019 declared the loss
   although `w:tblLook` firstRow expresses heading formatting without repetition. The writer now writes `w:tblHeader`
   only for repeating headings, and the reader maps a `tblLook` firstRow over a table style with heading formatting
   (fill, ink or bold) and no `tblHeader` rows to one non-repeating heading row. DOCX019 now covers only a
   non-repeating heading of more than one row. Tests: `Round186DocxHeadingLookTests`, the updated
   `TableRoundTripTests.UnIntestazioneDiPiuRigheCheNonSiRipeteSiDichiaraPersaInDocx`.
3. **Minor: splitting a heading cell into rows pushed the last heading row into the body.** `SplitRows` inserted the
   new rows without growing `HeaderRows`, so the heading rows' other cells spanned across the heading boundary. Test:
   `Round186TableSplitHeaderTests`.
4. **Minor: sorting whole rows left row properties behind.** Sorting moved only the cells, so row height, "allow row
   to break across pages" and the tracked insertion or deletion mark of a row stayed at their old positions. When the
   sort range covers whole rows the rows themselves are reordered now; a partial-width sort still moves cells. Test:
   `Round186TableSortRowTests`.

## Checked without findings

Insert and delete of rows and columns through merged cells (span growth, heirs, heading count), merge with enclosed
selection, unmerge, column split, table split and join, text to table and back, ODT covered cells, header rows,
`fo:keep-together` and repeated rows, DOCX `vMerge`/`gridSpan`/`cantSplit`/`trHeight`, the order of `w:tcPr`
children, undo of each edit as one step through the table memento.

## Open items

- `w:tblLook` banding flags (`noHBand`) are not read: a Word table whose look turns banding off over a banded style
  shows bands. The model has no "banding off" flag apart from the unbanded style variant; mapping it needs either such
  a flag or the reader switching to the "No Bands" variant of the style.
- A table read from DOCX whose style has heading formatting and whose look has firstRow on now gets one heading row;
  fixtures produced by other suites may change their `HeaderRows` expectations (check at the join).
