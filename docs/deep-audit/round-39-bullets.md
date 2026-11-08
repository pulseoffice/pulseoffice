# Deep audit round 39: picture bullet limits and side effects

Continues [round 38](round-38-deck-picture-bullets.md), "Remaining", and checks its side effects on Ledger drawings.

## Coverage

| File | Read | Outcome |
|---|---|---|
| docs/deep-audit/round-38-deck-picture-bullets.md | all | limits to close |
| src/PulseOffice.Formats.Odf/Shapes/OdfPictureInliner.cs | all | styles.xml inlined too: outline pictures reachable |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfListPictures.cs | all | reading/writing of `text:list-level-style-image` |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextReading.cs | all | `ListPlacement.Level` returns null without a `text:style-name`: outline list style ignored |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextStyleTables.cs | all | `OdfParagraphLevels` carries outline paragraph levels, not the outline list style |
| src/PulseOffice.Formats.Odf/Presentation/Reading/StyleTables.cs | 180-300 | presentation style chain resolved for paragraphs only |
| src/PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs | 135-300, 355-390 | picture centred on the first line, never grows the line |
| src/PulseOffice.Text.Rich/BulletPictureSizing.cs | all | sides clamped to 1..144 pt: huge sizes bounded at render |
| src/PulseOffice.Documents.Layout/Inline/ParagraphShaper.cs | 140-175 | Folio: picture bottom on the baseline, ascent grows to the picture |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingBulletPictures.cs | all | 8 MB cap on embedded parts only; pending data not capped |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingShapeTextRuns.cs | all | XLSX shape text: runs only, no list model, no `a:buBlip` reached |
| src/PulseOffice.Formats.OpenXml/Shapes/SheetShapes.cs | 30-200 | XLSX shapes written from the model: no foreign `r:embed` copied |
| src/PulseOffice.Formats.OpenXml/XlsxReader.cs | 40-112 | bullets in shape text dropped silently |
| src/PulseOffice.Formats.Odf/Spreadsheet/OdsObjectLinks.cs | all | pattern for ODS loss detection |
| src/PulseOffice.Formats.Odf/Spreadsheet/OdsGroupedObjects.cs | 1-20 | same |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/LevelStyles.cs | 78-145 | `a:buSzPct` read without the schema bounds (25-400 %) |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/OoxmlPendingImages.cs | all | one image part per blip, even for identical data; placeholder id left when data is unreadable |
| src/PulseOffice.Formats.Odf/Presentation/Writing/Foundation.cs | 1-80 | `AddMedia` deduplicates by path; a bullet path holds the SHA-256 of the data, so a collision means identical bytes |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextWriting.cs | 80-100 | picture with empty data written as `a:buBlip` |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextWriting.cs | 96-145 | same for ODP |
| tests/PulseOffice.Formats.OpenXml.Tests/DeckPictureBulletTests.cs | all | round 38 tests: extended |
| tests/PulseOffice.UI.Controls.Tests/RichTextPictureBulletTests.cs | all | round 38 tests: extended |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | OdfTextReading.cs `ListPlacement.Of`, StyleTables.cs `OdfPresentationTextStyles.Levels` | a `text:list` without `text:style-name` ignored the list style of its presentation (outline) style: picture and character bullets of the master fell back to "•" | `OdfParagraphLevels.InheritedList` carries the first `style:graphic-properties/text:list-style` of the presentation style chain; the bullet symbol is read from it when the list names no style (geometry and numbering still from the list's own style, as before) |
| 2 | medium | TextTypesetting.cs `TextBlockTypesetter.Placed` | a picture taller than the line was centred with its top clamped at 0 and hung over the next paragraph; the line did not grow | as in Folio (`ParagraphShaper.Label`: ascent = max(picture, text)): a taller picture sits with its bottom on the first line's baseline, the paragraph's segments drop by the difference (`TypesetBulletPicture.Drop`) and `TypesetLine.Height` includes the picture; a picture that fits stays centred |
| 3 | medium | OoxmlPendingImages.cs `Resolve` | one image part per blip even for identical bytes: 40 bullets with one picture wrote 40 media parts per slide | image parts reused per part by SHA-256 of the data (also benefits fills and Folio pictures) |
| 4 | minor | OoxmlPendingImages.cs `Resolve` | unreadable pending data left `r:embed="rIdPulsePending"`, a dangling relationship | the embed is cleared |
| 5 | minor | LevelStyles.cs `RelativeSize` | `a:buSzPct` unbounded (2147483647 = 21474x the font) for character bullets | clamped to the schema's 25-400 % |
| 6 | minor | DrawingBulletPictures.cs `Read`, OdfListPictures.cs `Read` | `a:buSzPts` and `fo:width`/`fo:height` unbounded or negative in the model | bounded to 4000 pt (schema maximum), non-positive or non-finite ODF sides fall back to the default |
| 7 | minor | DrawingTextWriting.cs, OdfTextWriting.cs, TextTypesetting.cs | a picture with empty data was written as `a:buBlip` with a pending placeholder / an empty `Pictures/bullet-…` entry and drawn as a broken frame | a picture without data is a character bullet everywhere |
| 8 | medium (declared) | XlsxReader.cs, OdsReader.cs | Ledger drawing text has no list model: bullets (character, numbering, picture) in XLSX and ODS shape text were dropped silently | declared: XLSX191 and ODF271 (new codes); removed in [round 41](round-41-ledger-shape-lists.md), which gives shape text a list model. Verified: a Ledger shape whose paragraphs carry `a:buBlip` (resolvable, or naming a missing relationship, with `a:buSzPts` 400000) reads without errors, keeps its paragraphs, crosses ODS and back to XLSX; the XLSX writer builds shape text from the model, so no foreign `r:embed` is ever copied |

Checked and sound: the 8 MB cap holds on embedded parts (falls back to the character bullet); a `a:buBlip` naming a
missing relationship falls back too; undecodable bytes are kept, cross ODP and are painted as a frame; ODP
`Pictures/bullet-<sha256>` cannot collide with other media of different content (the name is the hash, `AddMedia`
deduplicates by path, the manifest stays unique, tested with a slide picture of the same bytes); sizes are clamped to
1-144 pt at render by `BulletPictureSizing`.

## Tests

New: `SheetShapePictureBulletTests` (Formats.OpenXml: XLSX/ODS Ledger shapes with picture bullets, XLSX191, ODF271),
nine cases in `DeckPictureBulletTests` (outline inheritance, deduplication, collision with slide media, empty data, huge
sizes in PPTX and ODP, cap, undecodable data, missing relationship), three cases in `RichTextPictureBulletTests`
(line growth on the baseline, bounded huge sizes and empty data, small picture unchanged). The eight Formats.OpenXml
tests tied to source fixes were run against the code without the fixes: all eight failed.
Green: Formats.OpenXml 2338, Formats.Odf 783, UI.Controls 359, Presentations 318, Presentations.UI 73, Sheets.UI 429.
Corpus: BulletsAsImage.odp, a corpus case, groupShape.xlsx, cell-anchored-hidden-shapes.xlsx: all OK.

## Remaining

- Ledger drawing text has no list model (declared XLSX191/ODF271); adding one is a feature, not a fix.
- A list without its own style takes only the bullet symbol from the outline style; geometry and numbering still come
  from the list's own style or the paragraph, as before.
- Live check (coordinator): a Deck slide with a tall picture bullet; an ODP whose outline style carries a picture.
