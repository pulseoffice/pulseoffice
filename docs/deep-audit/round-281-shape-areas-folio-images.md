# Round 281: shape autofit leftovers and Folio images

Date: 2026-11-20. Area: the shape autofit queue items of rounds 272 and 276, and a fresh audit of Folio images.

## Defects found

### Serious
1. **Image brightness, contrast and transparency were lost silently on reading.** A Word picture with `a:lum` or
   `a:alphaModFix`, or an ODF frame with `draw:luminance`, `draw:contrast` or `draw:image-opacity`, came back with
   the original look and was written without it. Fixed: `RasterImageObject.Tone` (`ImageTone`: brightness and
   contrast in percent, transparency in percent), mapped natively by `DrawingImageTone` (DOCX and XLSX) and
   `OdfImageTone` (ODT), drawn by `RasterPainter` (colour adjustment through `IPixelFilterCatalog.Adjust`, opacity
   pushed on the drawing context). ODF `color-mode="watermark"` reads as a washed-out tone (brightness 70,
   contrast -70, Word's washout). ODS images carry no graphic properties: the loss is declared by the existing
   ODF022 (text widened in the five languages). PDF export no longer embeds the untoned original bytes.

### Medium
2. **Adjusted preset shapes had no text inset.** A shape whose handles were moved becomes a computed preset
   geometry, and `ShapeTextArea` returned no margins for any computed geometry. Fixed: the preset catalog now parses
   the DrawingML `rect` of every preset, `IPresetGeometryEngine.TextBounds` evaluates it with the shape's adjust
   values, and `ShapeTextArea` uses it for every computed preset.
3. **Stars, heart, sun, cloud and pentagon used approximate text areas.** They now use the evaluated preset rectangle.
4. **The overlay shape text editor covered the whole box** (layouts not composed in place) and rotated about its own
   centre. It now covers the inner text area (`ShapeTextEditor.Inner`) and rotates about the shape centre.
5. **Inline resize-shape shapes kept their stored size.** Inline object boxes now carry the fitted height
   (`ObjectBox.FittedHeight`) and `ShapeStorySizes` settles them as it does anchored ones.

### Minor (queued)
- Remaining hand-written preset areas; ODF enhanced-geometry text areas.
- No non-destructive tone command in the UI; mono/bi-level colour modes; Deck codecs without tone.

## Audited without defects
Crop (`a:srcRect` and `fo:clip`, measured on the original image), flip and rotation (`a:xfrm` flips, ODF mirror),
alt text (`wp:docPr` descr and title, `svg:desc`/`svg:title`), linked images (`r:link`, `xlink:href` external,
dashed placeholder when not loaded), greyscale (`a:grayscl`, `draw:color-mode="greyscale"`), duotone (declared lost
in ODF).

## Research
DrawingML `a:lum` stores bright and contrast in thousandths of a percent and `a:alphaModFix amt` the opacity in the
same unit; ODF 1.3 `draw:luminance` and `draw:contrast` are signed percentages and `draw:image-opacity` is an opacity
percentage. Both map natively, so no loss is declared for DOCX, ODT or XLSX.

## Tests
`Round281PresetTextAreaTests`, `Round281ShapeEditorAreaTests`, an inline case in `Round276ShapeStoryAreaTests`, tone
round trips in `DocxRoundTripTests`, `Round233FolioImageTests` and `XlsxImageRoundTripTests`.

## Not covered
Compression and resize with aspect lock were not re-audited in this round (queued for the next Folio image pass).
