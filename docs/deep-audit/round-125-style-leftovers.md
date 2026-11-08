# Deep audit round 125: leftovers of round 123 and the Ledger cell-style resolution path

Scope: the four residuals of round 123 (docs/deep-audit/round-123-cell-styles.md), then a full read of
`NamedStyles.cs` and of the path that resolves a cell's style for the grid and for printing.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/NamedStyles.cs | 181/181 | defect 1 fixed; notes below |
| src/PulseOffice.Sheets/Styling/CellStyle.cs | 83/83 | clean |
| src/PulseOffice.Sheets/Styling/StyleCatalog.cs | 43/43 | clean |
| src/PulseOffice.Sheets/Worksheet.cs | 180-260 (`Store`, `StyleIdAt`, `SetStyle`, `SetOwnStyle`) | clean |
| src/PulseOffice.Sheets.UI/GridParts/Painting/GridCellPainter.cs | 120-210 (background and text passes) | clean |
| src/PulseOffice.Sheets.UI/GridParts/Painting/CellValuePainter.cs | 15-60 (padding, indent, wrap, rotation) | reference for defect 6 |
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | 420-560 (`PaintCell` and helpers) | defect 6 fixed; residuals below |
| src/PulseOffice.Formats.OpenXml/XlsxShapeStyleMapper.cs | 174/174 | defects 3, 4 fixed |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/Reading.cs | 270-300 (text reader) | defect 3 fixed |
| src/PulseOffice.Formats.Odf/ShapeStyles/Reading.cs, Writing.cs | text property reader and builder | defect 3 fixed (ODF side) |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs | 50-90 (`SectionsExtension`) | defect 5 fixed |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PresentationParts.cs | 30-60 (`SectionReader`) | defect 5 fixed |
| src/PulseOffice.Formats.Odf/Presentation/OdpSectionXml.cs | 20/20 | defect 5 (ODP side) |
| src/PulseOffice.Formats.Odf/Presentation/Writing/DocumentWriters.cs | 110-135, 259-340 | defect 2 fixed |
| src/PulseOffice.Formats.Odf/Presentation/Reading/SlideReading.cs | 252-292 | defect 2 fixed |
| behaviour of the reference suite, checked in its sources | | the reference suite writes page names joined by commas with no escaping and splits on every comma |

## Defects

Medium

1. `NamedStyleCatalog.Merge` let a child named style inherit only font, background, borders, horizontal alignment and
   number format: language, wrap, vertical alignment, rotation, indent and protection of the parent were dropped
   (`src/PulseOffice.Sheets/NamedStyles.cs:85`). Applying "Child of a German style" wrote no language, no wrap, etc.
   They are now inherited when the child leaves them at the default. Test:
   `NamedStyleRound125Tests.A_child_style_inherits_every_property_its_parent_sets` (run red).
2. ODP: a slide whose name contains a comma could not be part of a custom show. The reference suite has no
   representation either (it joins names with commas, unescaped, and splits on every comma), and our reader split
   `A,B` into slides `A` and `B`, putting the wrong slides in the show. The standard `presentation:pages` now leaves out
   names that contain a comma (so other applications never pick wrong slides), and when one is left out the full list
   is written in `pt:pages` (namespace `urn:pulseoffice:presentation-show`, backslash escaping), which our reader
   prefers. New service `OdpShowPagesXml`. The loss for other applications is declared as `ODF286` (five languages).
   Tests: `OdpCustomShowRound125Tests` (three, run red).
3. XLSX/PPTX: shape text always carried `lang="en-US"` (`XlsxShapeStyleMapper.cs:155`), labelling every shape's text as
   English for spelling and hyphenation in other applications. `ShapeTextStyle` now has `Language`; DrawingML writes
   and reads `a:rPr/@lang` from it (omitted when unknown), ODF writes and reads `fo:language`/`fo:script`/`fo:country`
   on the graphic style's text properties. The property audit (`ShapeStylePropertyAuditTests`) covers it in ODP and
   PPTX. Tests: `XlsxShapeStyleRound125Tests` (two, run red).

Minor

4. `XlsxShapeStyleMapper.Line` cast the width in EMU to `int` with no cap: a large width overflowed to a negative
   value, an invalid `a:ln/@w`. Now clamped to 0..20116800 (the schema's `ST_LineWidth`). Test:
   `A_huge_line_width_is_capped_instead_of_overflowing` (run red).
5. PPTX section ids were new GUIDs on every save, so two saves of the same deck differed. Read ids are now kept
   (`Slide.SectionId`, also written in ODP next to the section name), and sections without one get a deterministic
   name-based GUID (SHA-256 of the first slide id, name and ordinal, version 5 layout); duplicates (a duplicated slide
   carrying the same id) are resolved deterministically. Tests: `PptxSectionRound125Tests` (two, run red).
6. Printing ignored the cell indent: an indented cell printed flush with the cell edge while the grid indents it
   (`SheetPrintDocument.cs` `PaintCell`). The text box now keeps 6 pt per indent level on both sides, matching the grid's
   8 px. Tests: `CellStylePrintRound125Tests` (two, run red).

## Residual (not changed)

- `NamedStyleCatalog.Merge` treats default values as "not set": a child cannot switch off wrap, protection or a font
  attribute that its parent sets, nor go back to bottom alignment. The model has no "unset" marker; files that need it
  are rare.
- Font and borders are still inherited as a whole (a child that only changes the font colour loses the parent's bold).
- `NamedStyleCatalog.Restore` appends a restored style at the end, so undoing a deletion changes the style order shown
  in the panel (files are unaffected since round 123 writes `Default` first).
- `NamedStyleCatalog.Remove` leaves children naming the removed style as parent; cells keep their flattened look, and a
  child re-applied afterwards loses the inherited part.
- Printing does not wrap text, rotate it, or draw underline and strikethrough: `IPageCanvas.DrawText` is single-line and
  `PrintFont` has no decoration fields. This needs a print-canvas change across the three modules.

## Tests run

- `tests/PulseOffice.Sheets.Tests`: 386 passed.
- `tests/PulseOffice.Formats.OpenXml.Tests`: 2496 passed.
- `tests/PulseOffice.Formats.Odf.Tests`: 858 passed.
- `tests/PulseOffice.Sheets.Printing.Tests`: 39 passed.
- `tests/PulseOffice.Presentations.Tests`: 332 passed; `tests/PulseOffice.Architecture.Tests`: 43 passed;
  `tests/PulseOffice.Localization.Tests`: 32 passed.

## To test live

- Ledger: a style derived from a German, wrapped, top-aligned, unlocked style; apply it to a cell: the cell is German,
  wrapped, top-aligned and unlocked.
- Deck: a custom show with a slide named "Intro, part 1", saved as ODP: reopened in Deck it keeps the slide; in the
  reference suite the show lacks that slide (declared).
- Deck: save the same deck with sections twice as PPTX: `ppt/presentation.xml` is identical; open in PowerPoint.
- Ledger/Deck: a shape whose text is German saved as XLSX/PPTX and ODS/ODP: the language comes back.
- Ledger: print preview of an indented cell.
