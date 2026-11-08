# Deep audit round 67: Folio tables (3)

Area: the files not reached in round 66 (`round-66-folio-tables-2.md`).

## Coverage

| File | Lines read / total | Outcome |
|---|---|---|
| `src/PulseOffice.Documents/Editing/Tables/CellNumberFormatting.cs` | 119/119 | defect 3 |
| `src/PulseOffice.Documents/Editing/Tables/TableBorderFraming.cs` | 51/51 | clean |
| `src/PulseOffice.Documents/Editing/Tables/TableGeometryEditing.cs` | 80/80 | clean |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxFloatingTables.cs` | 78/78 | clean (floating tables round trip through `tblpPr`) |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxTableStyleParagraphs.cs` | 84/84 | clean |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxTablesInParagraphs.cs` | 47/47 | clean |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs` | 252/252 | defects 1, 2, 4 |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxTableCodec.cs` | 160/160 | clean |
| `src/PulseOffice.Formats.Odf/Odt/OdtBodyWriting.cs` (table writer) | 598-705/865 | defects 1, 2 |
| `src/PulseOffice.Formats.Odf/Odt/OdtBodyReading.cs` (table reader) | 737-910/~950 | defects 1, 2 |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxBlockReader.cs` (table reader) | 296-445 | defects 1, 2 |
| `src/PulseOffice.Documents.Layout/Flow/Stories.cs` (`BlockStacker`, the table composer) | 125-420 | defect 2 (layout side) |
| `src/PulseOffice.Documents.Layout/Flow/Paginator.cs` | 270-420/1282 (`VisitTable`) | not reached in full |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | serious | model, DOCX, ODT | cell text direction (`w:textDirection`, ODF `style:writing-mode` / `style:rotation-angle` on table-cell-properties) was not in the model: vertical text in cells was dropped silently on every load | `TableCell.TextDirection` (`CellTextDirection`: Horizontal, TopToBottom, BottomToTop) in the model, memento, clone and clipboard remapper; DOCX `DocxCellGeometry` writes `tbRl`/`btLr` and reads all Word values (`tbRl`, `tbRlV`, `tbLrV`, `btLr`, legacy `tb`/`rl`/`lr`); ODT `OdtCellDirections` writes `style:writing-mode="tb-rl"` and, for bottom-to-top, `style:rotation-angle="90"` plus our `pt:text-direction`, and reads `tb-*`, `bt-lr`, 90/270 rotations |
| 2 | serious | model, DOCX, ODT, layout | per-cell margins (`w:tcMar`, per-cell `fo:padding-*`) were not in the model: lost silently; the ODT writer stamped the table padding over every cell | `TableCell.Padding` (`CellPadding` record, points); DOCX `tcMar` written in schema order (after `shd`, before `vAlign`) and read with missing sides taken from the table's effective padding; ODT writes the cell's own padding and marks it with the `cell-padding` token in `pt:from-table-style`; foreign ODT cells keep their padding when it differs from the table's (`Unpadded` drops the redundant ones); the table padding is read from the first cell that has no own padding; `BlockStacker` composes, measures, aligns and draws each cell with its own insets |
| 3 | minor | `CellValueText.TryValue` | "NaN" / "Infinity" typed in a number-formatted cell were recognised as values (.NET always accepts those symbols), so the cell showed "NaN" and the ODT writer emitted `office:value="NaN"` | non-finite values are refused |
| 4 | minor | `DocxBlockWriter.Origin` | a ragged row above a covered cell (shorter row) indexed out of range while writing DOCX | the search stops at a row that does not reach the column |

## Verified, no defect

- Nested tables: written by the same visitor in both writers; the DOCX writer always ends a cell with a paragraph.
- Floating tables: `DocxFloatingTables` maps `tblpPr` to an anchored text box with a table and back, with relations and wrap distances clamped.
- Tracked rows: DOCX `w:ins`/`w:del` in `trPr` (`DocxRowChanges`), ODT `pt:change-id`.
- Non-repeated header rows: `pt:repeat-header="false"` in ODT (declared in round 66).
- Border conflicts: each cell draws its own edges; adjacent conflicting edges overlap (the later cell wins); no rule-based resolution, a rendering limit rather than a loss.

## Not done / open

- Vertical text is now kept in both formats, but the layout still sets it horizontally (no rotated line composition): rendering, not data.
- The paginator still slices rows with the table-level vertical inset; a cell with a larger own top/bottom padding can be cut a few points off when a row breaks across pages.
- `Paginator.cs` beyond `VisitTable` was not read in full.

## Tests

- `tests/PulseOffice.Formats.OpenXml.Tests/CellDirectionPaddingTests.cs`: DOCX, ODT and cross-format round trips of text direction and own padding; DOCX schema validation of `tcPr`.
- `tests/PulseOffice.Documents.Layout.Tests/CellPaddingLayoutTests.cs`: own padding moves the text and grows the row.
- `tests/PulseOffice.Documents.Tests/CellValueFiniteTests.cs`: NaN and infinities are not values.
