# Deep audit round 38: picture bullets in Deck

Continues [round 36](round-36-bullets-links.md), "Remaining": Deck picture bullets (ODP103).

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Formats.Odf/OdpReader.cs | 1-230 | ODP103 declared for any `text:list-level-style-image` |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/LevelStyles.cs | all | `a:buBlip` counted as a bullet, picture dropped |
| src/PulseOffice.Text.Rich/RichText.cs | 130-200 | `BulletSymbol` carries character, font, colour, size: no picture |
| src/PulseOffice.Documents/Styles/Styles.cs | 100-135 | `ListBulletPicture` lives in Folio's model: not reachable from Deck |
| src/PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs | 1-80, 130-300 | hanging label typeset as text only |
| src/PulseOffice.UI.Controls/Text/Rich/RichTextFramePainter.cs | all | draws the label as text: slide, thumbnails, show and export share it |
| src/PulseOffice.Documents.Layout/Inline/BulletPictureSizing.cs | all | sizing usable by Deck once moved to Text.Rich |
| src/PulseOffice.Documents.UI/Rendering/BulletPictureObjects.cs | all | Folio-side cache, unchanged |
| src/PulseOffice.Formats.Odf/Odt/OdtListPictures.cs | all | bound to ODT sessions: generalised to delegates for ODP |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextReading.cs | 1-60 | `ListPlacement.Symbol` reads only `list-level-style-bullet` |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextWriting.cs | 100-290 | automatic list styles write character bullets only |
| src/PulseOffice.Formats.Odf/Shapes/OdfFillImageInliner.cs | all | inlines package pictures before parsing: extended to list images |
| src/PulseOffice.Formats.Odf/Presentation/Writing/Foundation.cs | 1-60 | session adds media: exposed through `IOdfStyleSink` |
| src/PulseOffice.Presentations.UI/Rendering/ElementPainters.cs | 1-120 | text painted through `IRichTextFramePainter` |
| src/PulseOffice.UI.Controls/TextFormat/ListFormat.cs | 1-80 | `ListBulletImage` in the shared dialog |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextWriting.cs | all | `Marker` writes character bullets only |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextReading.cs | all | symbol comes from `LevelStyle`: picture flows once `LevelStyleReader` reads it |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/OoxmlPendingImages.cs | all | pending blips resolved per part at save: reused for `a:buBlip` |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/Reading.cs | 86-120 | owning part found from the element's root: reused for `a:buBlip` reading |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PlaceholderInheritance.cs | 1-30, 95-140 | master/layout list styles read through `LevelStyleReader`: pictures there resolve against their own part |
| src/PulseOffice.Formats.OpenXml/Presentation/PptxRunLinks.cs | all | not needed for pictures |
| src/PulseOffice.Presentations.UI/DeckTextDialogCommands.cs | 90-210 | list mapping drops the picture; dialog offers no picture row |
| src/PulseOffice.UI.Controls/TextFormat/ListPictureChooser.cs | all | dialog type `ListBulletImage` duplicated the model's picture |
| src/PulseOffice.Documents.UI/Commands/FolioListCommands.cs | 50-80 | converts between the two duplicate types |

## Changes

- Model: `ListBulletPicture` moved from Folio's styles to `PulseOffice.Text.Rich` (one type for Folio, Deck and the shared
  dialog; the dialog's duplicate `ListBulletImage` removed). `BulletSymbol.Picture` carries a Deck picture bullet.
  `IBulletPictureSizing` moved to Text.Rich and takes a font size, shared by Folio layout and Deck typesetting.
- ODP: `OdfListPictures` (was `OdtListPictures`, now delegate-based, used by ODT and ODP). Reading: `OdfPictureInliner`
  (was `OdfFillImageInliner`) inlines `text:list-level-style-image` pictures for ODP; `ListPlacement` reads them with
  `fo:width`/`fo:height`. Writing: automatic list styles write `text:list-level-style-image` with the picture in
  `Pictures/bullet-<sha256>`; `IOdfStyleSink.AddMedia`, the ODP session deduplicates paths. A font-relative picture is
  written at its size for the paragraph's font (ODF has no relative picture size).
- PPTX/XLSX drawing text: `DrawingBulletPictures` reads `a:buBlip` (owning part found from the element, 8 MB cap, works in
  slides, layouts and masters) with `a:buSzPts` (fixed) or `a:buSzPct` (font-relative); writes `a:buSzPts`/`a:buSzPct` and
  `a:buBlip` through `OoxmlPendingImages`, resolved into image parts at save.
- Rendering: `TextBlockTypesetter` places a `TypesetBulletPicture` instead of the hanging label (inline labels reserve an
  em space); `RichTextFramePainter` draws it through `IBulletPictureBitmaps` (weak cache per data), a frame when the
  picture cannot be decoded. Slide view, thumbnails, slide show and PNG/PDF/HTML export share the painter.
- Deck dialog: `ListFeatures.Picture` offered; `DeckTextFormats` maps the picture both ways; the change goes through
  `ApplyText` (one undo step).
- ODP103 narrowed: only pictures that are linked or missing from the package.

## Tests

New: `DeckPictureBulletTests` (Formats.OpenXml, ODP/PPTX parity), `RichTextPictureBulletTests` (UI.Controls),
`DeckPictureBulletDialogTests` (Presentations.UI), one case in `OdfTextCodecsAuditTests` (Formats.Odf).
Green: Formats.OpenXml 2304, Formats.Odf 783, UI.Controls 356, Presentations.UI 69, Presentations 318, Documents.Layout 208.
Corpus: a corpus case, BulletsAsImage.odp, two corpus cases/.pptx, two corpus cases,
two corpus cases: all OK, no ODP103.

## Remaining

- Lists that take their picture from the presentation outline style without a `text:list` style name keep the character.
- A picture taller than the line does not grow the line height in Deck.
