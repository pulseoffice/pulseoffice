# Round 41: lists in the text of Ledger shapes

Removes the loss declared in [round 39](round-39-bullets.md), item 8 (XLSX191, ODF271): the text of Ledger shapes had no
list model, so character, numbered and picture bullets in XLSX and ODS shapes were read as plain paragraphs.

## Coverage

| File | Lines | Outcome |
|---|---|---|
| docs/deep-audit/round-39-bullets.md | all | item 8: the loss to remove |
| src/PulseOffice.Formats.OpenXml/XlsxReader.cs | 55-115 | XLSX191 raised from `XlsxShapeTextLists`: removed |
| src/PulseOffice.Formats.Odf/Spreadsheet/OdsShapeTextLists.cs | all | ODF271 detector: the file now holds the ODF shape list codec (moved to `Shapes/OdfShapeTextLists.cs`) |
| src/PulseOffice.Drawing/Shapes/ShapeDefinition.cs, ShapeTextRun.cs, ShapeTextEditing.cs | all | shape text is runs split by `\n`; no paragraph properties |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingShapeTextRuns.cs | all | the shared DrawingML shape-text codec (XLSX and PPTX shapes): paragraphs flattened |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextReading.cs, DrawingTextWriting.cs, DrawingBulletPictures.cs | all | Deck's list reading and writing to reuse |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/LevelStyles.cs | 1-145 | `LevelStyleReader` reused for shape paragraphs |
| src/PulseOffice.Text.Rich/RichText.cs, ListLabels.cs, BulletPictureSizing.cs | list parts | the shared list model |
| src/PulseOffice.Presentations.UI/Editing/ShapeRichText.cs | all | Deck-only bridge shape text <-> RichText: moved to Drawing so every module shares it |
| src/PulseOffice.Presentations.UI/DeckTextDialogCommands.cs | all | the list mapping of the bullets dialog: moved to the shared `ParagraphListFormats` |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextReading.cs, OdfTextWriting.cs, OdfListPictures.cs | list parts | `ListPlacement` ignored package pictures (`_ => null`): it now takes the package reader |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextRuns.cs, SheetShapeWriting.cs, SheetShapeReading.cs, SheetPrimitiveShapeReader.cs, OdfShapeMapper.cs, DeckObjectCodecs.cs | all / shape parts | the shared ODF shape-text codec (ODS, ODP and ODT custom shapes) |
| src/PulseOffice.Formats.Odf/Presentation/Reading/StyleTables.cs | 156-195 | drawing definitions now also carry automatic list styles |
| src/PulseOffice.UI.Controls/Shapes/ShapeRenderer.cs | all | one `FormattedText` for the whole shape: no labels, no indents |
| src/PulseOffice.UI.Controls/Text/Rich/RichTextFramePainter.cs, TextFrameComposer.cs, TextTypesetting.cs | all / 1-260 | the shared typesetting and painter that draws labels and picture bullets |
| src/PulseOffice.Drawing.Printing/ObjectPathBuilder.cs | all | vector printing of shape text is plain text |
| src/PulseOffice.Sheets.UI/LedgerShapeCommands.cs, LedgerViewParts/LedgerShapeTextSession.cs, ShapeText/ShapeTextCellStyle.cs | all | shape commands, shape text session and its facade |

## Changes

- **Model** (`PulseOffice.Drawing`, which now references `PulseOffice.Text.Rich`): `ShapeObject.Lists` holds one
  `ShapeListItem?` per paragraph (level 0-8, numbered, `BulletSymbol` with the shared `ListBulletPicture`); `null` means
  no list. `ShapeTextLists` pads, trims, clamps and applies it; `ShapeTextEditing.WithText` keeps it aligned with the
  paragraphs (a new paragraph copies the item it was split from, merged paragraphs keep the first item, empty text drops
  the list). `ShapeRichText` (moved from Deck) carries the items both ways; `ShapeTextParagraphs` edits them through
  `TextParagraph`.
- **XLSX** (shared with PPTX shapes): `DrawingShapeTextRuns` reads each `a:pPr` with `LevelStyleReader`
  (`a:buChar`, `a:buAutoNum`, `a:buBlip`, `lvl`, bullet font, colour, size) and writes per-paragraph `a:pPr` through the
  new `DrawingListMarkers`, which `ParagraphBuilder` now uses too (one writer for Deck and Ledger). Pictures go through
  `OoxmlPendingImages`, resolved into the drawing part.
- **ODS** (shared with ODP and ODT custom shapes): `OdfShapeTextRuns` wraps listed paragraphs in `text:list` with an
  automatic `text:list-style` built by the shared `ListStyleBuilder` and registered by `IOdfTextStyleSink.RegisterList`
  (deduplicated); picture levels embed the image as `office:binary-data`, and font-relative pictures are fixed at the
  shape's text size. Reading uses `ListPlacement` with the list styles of content and styles and the package reader.
- **Rendering**: `ShapeRenderer` hands listed shape text to `ShapeListText`, which paints it with the shared
  `RichTextFramePainter` (labels, hanging indents, picture bullets, alignment and anchor of the shape). Printing and PDF:
  `ObjectPathBuilder.IsExact` is false for listed shapes, so Ledger prints them through the rasterizer with the same
  renderer.
- **Ledger UI**: `ledger.shapeText.bulletsNumbering` (Format › Text and the object context menu) opens the shared
  bullets dialog (`IListFormatDialog`, symbol and picture pages) on the selected shape, or on the selected paragraphs
  while its text is edited; one undo step ("Shape text list"); disabled on a protected sheet and without a shape.
  `IParagraphListFormats` is the shared mapping, used by Deck as well.
- **Declarations removed**: XLSX191, ODF271, their five translations and tests.

## Limits

- The in-place shape text editor still draws plain text while typing; the list appears when editing ends.
- Ledger freeform and fontwork text has no runs model at all, lists included (unchanged).
- Margins and indents of list paragraphs are not kept: they are regenerated per level on writing.

## Corpus

No XLSX or ODS file of `tools/corpus-audit/corpus.txt` carries list markers in drawing text (scan of `xl/drawings` for
`buChar`/`buAutoNum`/`buBlip` and of ODS drawings for `text:list`). `groupShape.xlsx` and
`cell-anchored-hidden-shapes.xlsx` were run with `corpus-audit one`.

## Tests

New: `SheetShapePictureBulletTests` (rewritten: picture, character, numbered and nested lists across XLSX, ODS and
back; DrawingML markers written; missing ODS picture keeps the bullet), `ShapeListTextTests` (UI.Controls: shared
painter used only for listed text, editing keeps items aligned, bridge round trip), `LedgerShapeListTests` (Sheets.UI:
shared dialog, picture bullets, undo/redo, numbering removed, protected sheet).
