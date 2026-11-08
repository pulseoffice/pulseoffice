# Deep audit round 112: object anchoring, clipped printing, PDF export

Open points of round 110 (object anchoring mode, objects across page edges, custom paper in the page setup dialog,
first page number and header variants in the macro API), then a full read of PDF export and of the printing surfaces.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.UI.Controls/Printing/PdfExportPrompt.cs | 41/41 | ok |
| src/PulseOffice.UI.Controls/Printing/PdfExportView.cs | 411/411 | ok (A4 noted) |
| src/PulseOffice.Printing/PrintJob.cs | 319/319 | A2 |
| src/PulseOffice.UI.Controls/Printing/AvaloniaPageCanvas.cs | 243/243 | clip added; A5 noted |
| src/PulseOffice.Sheets.UI/LedgerPrintCommands.cs | 131/131 | ok |
| src/PulseOffice.Printing/PageCanvas.cs | 184/184 | clip primitive added |
| src/PulseOffice.Printing/ClippedPageCanvas.cs | 165/165 | D2 |
| src/PulseOffice.Printing/ScaledPageCanvas.cs | 83/83 | clip forwarded |
| src/PulseOffice.Formats.Pdf/PdfContentCanvas.cs | 60-160, 414-440 | clip added |
| src/PulseOffice.Documents.UI/Rendering/PageSurfaces.cs | 130-260 | A1 |
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | 190-400 | D2 |
| src/PulseOffice.Sheets.UI/Scripting/PrintFacade.cs | 207/207 | D4 |
| src/PulseOffice.Sheets.UI/PageLayout/SheetPageSetupView.cs | 170-200, 345-360 | D3 |
| src/PulseOffice.Sheets/SheetLayout.cs | 1-492 | D1 |
| src/PulseOffice.Sheets/SheetPlacement.cs, SheetObjectExtent.cs, PlacedObject.cs | full | D1 |
| src/PulseOffice.Sheets.Engine/Operations/SheetStructure.cs | 1-420 | D1 |
| src/PulseOffice.Formats.OpenXml/Drawing/XlsxAnchorLayout.cs, XlsxCellAnchors.cs | full | D1 |
| src/PulseOffice.Formats.OpenXml/XlsxFormControlMapper.cs | 70-240 | D1 |
| src/PulseOffice.Formats.Odf/Tables/AnchoredFrames.cs, Shapes/OdfCellEnd.cs | full | D1 |
| src/PulseOffice.Formats.Odf/OdsWriter.cs | 78-125, 318-380, 600-630 | D1 |
| src/PulseOffice.Formats.Odf/OdfChartMapper.cs | 40-140 | ok |

## Defects

| Id | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| D1 | grave | PlacedObject, SheetLayout, SheetStructure, XLSX/ODS anchors | No anchoring mode: every object kept its size when rows or columns were resized, hidden, inserted or deleted; `twoCellAnchor` without `editAs` (Excel's default, move and size) was read as move-only and every object was written `editAs="oneCell"`; ODS always wrote `table:end-cell-address` (which the reference suite reads as «resize with cell») and never `table:shapes`. | `ObjectAnchoring` on `PlacedObject`; `SheetLayout` raises `GeometryChanging/Changed` around every size and visibility change (nested, suspendable), `SheetObjectFollower` settles the objects through `ISheetObjectAnchoring` (move and size: start and end markers; move: anchor kept; fixed: sheet position kept). Structure edits suspend the notifications and `ObjectsPart` settles from markers captured before the edit: rows inserted inside stretch, deleted inside shrink, fixed objects never move nor go. XLSX `editAs` twoCell/oneCell/absolute, `oneCellAnchor`, `absoluteAnchor`, form controls `moveWithCells/sizeWithCells`; ODS end cell only for move and size, fixed objects in `table:shapes` with sheet coordinates (`OdsFrameAnchoring`), read back by `AnchoringPartReader`. Position and size dialog: «Anchoring» choice, five languages. Hidden rows shrink move-and-size objects on screen and in print, as in Excel. |
| D2 | medium | ClippedPageCanvas, SheetPrintDocument | An object across a page edge printed whole on its first page, spilling into the margin, and nothing on the next page; `ClippedPageCanvas` dropped or kept whole any image or path that crossed its edge (also Folio and Deck replays and multi-page sheets). | `IPageCanvas.Clip` (default no-op, `PageClipScope`); Avalonia pushes a clip, PDF writes `q … re W n … Q`, scaled, filtered, clipped and pathless canvases forward it. `ClippedPageCanvas` draws inside paths and images directly and clips those across the edge (stroke and shadow reach counted, rotated paths always clipped). Sheet printing paints every object meeting the page body through a clip, so it prints in parts. |
| D3 | minor | SheetPageSetupView | A paper not in the list (custom size from ODS) showed the first list paper as selected. | A «Custom · W × H mm» entry is added and selected; the setup keeps that paper. |
| D4 | minor | PrintFacade | First page number and first/even headers were not reachable from macros. | `FirstPageNumber` (0 = automatic, 1–32767), `DifferentFirstPage`, `DifferentOddEven`, `FirstHeader/FirstFooter/EvenHeader/EvenFooter`, undoable, titles and summaries in five languages. |
| A1 | medium | PathlessCanvas (Folio object text) | Did not forward glyph runs, links, notes, headings or clips: shape text in Folio print/PDF fell back to one `DrawText` per glyph and lost its hyperlinks. | Forwards them. |
| A2 | minor | PrintImposition | Copies were not capped: a huge count from preferences built a list of that many sheets. | Capped at 999, the dialog's limit. |

## Noted, not changed
- A4 (PdfExportView): a range with repeated pages (`1,1`) exports the page twice and reports «2 of 1»; printing allows the
  same, kept consistent with print.
- A5 (AvaloniaPageCanvas): a brush is allocated per call; harmless outside very large previews.
- Move-and-size objects whose rows are hidden and shown again come back on whole rows (the offset inside a hidden row is
  not remembered), unlike Excel, which keeps the exact offset.

## Tests
- tests/PulseOffice.Sheets.Tests/ObjectAnchoringRound112Tests.cs (resize, hide/show, columns, suspension)
- tests/PulseOffice.Sheets.Engine.Tests/ObjectAnchoringStructureRound112Tests.cs (insert/delete per mode)
- tests/PulseOffice.Printing.Tests/CanvasClipRound112Tests.cs, PrintImpositionTests.cs (clip, copies cap)
- tests/PulseOffice.Sheets.Printing.Tests/ObjectSplitRound112Tests.cs (object across a break, hidden rows)
- tests/PulseOffice.Formats.Pdf.Tests/PdfClipRound112Tests.cs
- tests/PulseOffice.Formats.OpenXml.Tests/ObjectAnchoringRound112Tests.cs (XLSX, ODS, XLSX→ODS)
- tests/PulseOffice.Sheets.UI.Tests/LedgerRound112AnchoringTests.cs (dialog anchoring with undo, custom paper), LedgerFacadeContractTests.cs (seven new print members)
- tests/PulseOffice.Documents.UI.Tests/PathlessCanvasRound112Tests.cs

Runs: Sheets 365, Sheets.Engine 1838, Sheets.Printing 37, Printing 67, Formats.Pdf 79, Formats.OpenXml 2465,
Formats.Odf 829, Sheets.UI 481, Scripting 158, Architecture 43, Documents.UI 369, Presentations.UI 106: all green.

## To try live
- Resize, hide and insert rows under a picture of each mode; print preview of an object across a page break.
- Open in Excel an XLSX written with the three modes; open in the reference suite the ODS (fixed object «To page»).
