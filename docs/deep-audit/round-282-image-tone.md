# Round 282: image tone across the three modules (2026-11-20)

Area: the image tone items left by round 281 (Folio images and Deck images): a non-destructive way to set tone,
colour modes, the PPTX, ODP and ODS picture codecs, and transparency in the PDF path.

## Defects found

Serious
- PPTX and ODP picture codecs ignored `ImageTone`: a toned image pasted from Folio into Deck, or toned in Deck, lost
  brightness, contrast and transparency silently on save. Fixed: `DeckPictureCodec` writes and reads `a:lum` and
  `a:alphaModFix` through the shared `DrawingImageTone`; the ODP image writer emits a graphic style with
  `draw:luminance`, `draw:contrast`, `draw:image-opacity` and `draw:color-mode`, and `DeckImageCodec` reads them back.

Medium
- No command set tone non-destructively; the colour correction dialog always baked pixels. Fixed: a shared
  `ImageToneView` (picture corrections: brightness, contrast, colour mode, transparency, live preview, reset) opened
  by `IRasterColorAdjust.ToneAsync`, wired as `folio.image.tone` (ribbon object/image with its icon, context menu),
  `deck.image.tone` and `ledger.image.tone` (Format › Image, context menu); each is one undo step through the
  module's existing image edit path. The colour correction dialog now starts from the image's tone and, when only
  brightness and contrast are set, stores them as tone instead of re-encoding the picture as PNG; red, green, blue or
  gamma still bake pixels (no format carries them).
- Colour modes were not modelled: ODF `mono` and DrawingML `a:biLevel` were read as plain colour, ODF `watermark`
  came back as luminance and contrast. Fixed: `ImageTone.Mode` (`ImageColorMode`: Standard, BlackWhite, Watermark).
  ODF writes and reads `draw:color-mode` mono and watermark; DrawingML writes `a:biLevel thresh="50000"` and the
  washout as `a:lum bright="70000" contrast="-70000"` (how Word stores Washout), and reads that exact pair back as the
  watermark mode. The shared raster painter applies the watermark's effective brightness and contrast and a 50 %
  luminance threshold (`BlackWhiteFilter`, `IPixelFilterCatalog.BlackWhite`). Greyscale stays `ImageFilter.Grayscale`.
- ODS wrote Ledger images without graphic properties, declared as ODF022. Fixed: `OdfImageMirrors.StyleFor(raster)`
  writes a shared automatic graphic style with greyscale colour mode, tone and `fo:clip`, and `Look` reads them back;
  ODF022 now only declares filters other than greyscale (sepia, posterize and the other pixel filters), which have no
  ODF equivalent.
- PDF export of a semi-transparent image (Folio): `EmbeddedRasterizer` handed premultiplied colour beside the soft
  mask, so transparent pictures came out darker. Fixed: colours are un-premultiplied as `PageImageCapture` already did.

## Tests
- `Round282ImageToneTests` (Formats.OpenXml.Tests): tone with each colour mode crosses PPTX, ODP, XLSX and ODS; a
  greyscale Ledger image crosses ODS; ODF022 no longer raised for tone.
- `Round282ImageToneViewTests` (UI.Controls.Tests): the panel starts from the tone, previews, resets, answers;
  black and white threshold.
- `Round282TransparentImagePdfTests` (Documents.UI.Tests): straight colour beside the soft mask.
- Updated: `Round233FolioImageTests` (watermark reads as the mode), `XlsxImageRoundTripTests` (ODF022 not raised).

## Still open
- A watermark with extra brightness or contrast is written to DrawingML as one combined `a:lum` and reads back as a
  standard tone with the same look (no loss of appearance, the mode is not kept).
- Slide background images do not carry `ImageTone` (only their own opacity).
- Compression and resize with aspect lock were not re-audited (carried over from 281).
- The new panel has not been seen live; Word and PowerPoint have not opened files with `a:biLevel` written by us.
