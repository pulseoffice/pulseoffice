# Deep audit round 64: complex scripts, third-party signing, visible signatures, CMYK images, large exports

Follows the limits left by [round 63](round-63-pdf.md).

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Security/PdfSigning.cs | 529/529 | xref streams, object streams and visible widget missing (items 2, 3) |
| src/PulseOffice.Formats.Pdf/PdfImages.cs | 130/130 | RGB only (item 4) |
| src/PulseOffice.Formats.Pdf/PdfContentCanvas.cs | 478/478 | per-code-point glyphs, no shaping (item 1) |
| src/PulseOffice.Formats.Pdf/Fonts/PdfFontSet.cs | 245/245 | per-code-point cmap lookup (item 1) |
| src/PulseOffice.Formats.Pdf/Fonts/PdfFontResources.cs | 239/239 | glyph use only by code point (item 1) |
| src/PulseOffice.Documents.UI/Rendering/GlyphRunPainters.cs | 140/~200 | screen also maps per code point: no shaping on screen either |
| src/PulseOffice.Documents.UI/FolioServiceCollectionExtensions.cs | 25-40 | shaping services registered |
| src/PulseOffice.Desktop/Composition/ShellSignatureCommands.cs | 120-180 | signing dialog: no visible option (item 3) |
| src/PulseOffice.UI.Controls/FormPromptView.cs | 280-312 | `CheckField` returns a boxed bool |
| tests/PulseOffice.Formats.Pdf.Tests/PdfProbe.cs | 140/140 | did not honour `/ActualText` |
| tests/PulseOffice.Security.Tests/PdfSignerTests.cs | 333/333 | classic tables only |
| src/PulseOffice.Formats.Pdf/PdfExporter.cs | 205/205 | images deduplicated by array reference: a repeated picture is written once per page (item 5) |
| src/PulseOffice.Formats.Pdf/PdfFile.cs | 133/133 | streams objects straight to the output: no buffering growth |
| src/PulseOffice.Documents.UI/Rendering/EmbeddedRasterizer.cs | 63/63 | pictures always re-rasterized to RGB: CMYK and ICC lost (item 4) |
| src/PulseOffice.Sheets.UI/SheetObjectRasterizer.cs | 82/82 | same for Ledger pictures |
| src/PulseOffice.Drawing/Assets/GraphicObjects.cs | 29-52 | `RasterImageObject` keeps the original bytes and media type |

## Findings and fixes

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | serious | Formats.Pdf `PdfFontSet`/`PdfContentCanvas`; Documents.UI `FaceGlyphMapping` | No shaping anywhere (screen nor PDF): Arabic in isolated forms, Indic conjuncts and vowel signs unordered, ZWJ emoji split | New project `PulseOffice.Typography.Shaping` (HarfBuzzSharp 8.3.1.3, the version Avalonia already ships): `IComplexScriptSegmenter` finds complex spans (joining/Indic/SE-Asian scripts across spaces and punctuation, digits break them; emoji ZWJ, flag, keycap, modifier sequences), `ITextShaper` (`HarfBuzzTextShaper`, LRU of 12 faces) shapes them. PDF: shaped glyph ids with positions (`PdfGlyph.Advance/OffsetX/Rise`, TJ adjustments, `Ts` for vertical offsets), wrapped in `/Span << /ActualText >> BDC … EMC` so search and copy give the logical text; ToUnicode maps each cluster's first glyph. Screen: `FontFileRunPainter` draws the same shaped glyphs; the span keeps the layout width (difference absorbed by the last glyph / a TJ adjustment). `DrawText` now writes `TJ` too. |
| 2 | serious | Security `PdfSigner` | Signing a file ending with an xref stream wrote a classic section; catalogue inside object streams not found | Split into `PdfFileStructure.cs` (trailer reader that tells xref streams apart and reads `/Size`, `/Root`, `/Info`, `/ID` from the right section; `PdfObjectIndex` with direct objects and FlateDecode object streams, 64 MB and 4096-stream caps), `PdfIncrementalUpdate.cs` (Strategy: classic table or `/Type /XRef` stream with `/W [1 4 2]`, `/Index`, `/Prev`) |
| 3 | medium | Security, Desktop signing dialog | Signature widget invisible, not in the page `/Annots` | `PdfSignatureWidget`: optional visible widget on the first page (`/P`, `/Rect` inside the MediaBox, inherited boxes followed), appended to inline or indirect `/Annots`, Form XObject appearance with translated labels; dialog checkbox `signPdf.visible` in five languages |
| 4 | medium | Formats.Pdf `PdfImageWriter`; Folio and Ledger rasterizers | Pictures always re-rasterized to RGB: CMYK JPEGs converted, ICC profiles dropped | `PageImage.Source` carries the original JPEG of a plain picture (no crop, filter, duotone, rotation, flip, opaque) chosen by the shared `IOriginalImageSources` (Drawing.Printing, wired in Folio and Ledger); `JpegInspector` reads SOF, Adobe APP14 and the APP2 ICC chunks; the writer passes the JPEG through as DCTDecode with DeviceCMYK and `/Decode [1 0 1 0 1 0 1 0]` for Adobe CMYK, or `[/ICCBased]` when the profile matches the components; PDF/A refuses DeviceCMYK without a profile and falls back to pixels; downsampling falls back to pixels |
| 5 | serious | Formats.Pdf `PdfExporter` | Images deduplicated by array reference: a header logo is written once per page (40 pages, 40 copies) | Deduplication by SHA-256 of size, pixels, alpha and source |
| 6 | minor | Formats.Pdf `PdfFontSet` | Per-cluster allocations dominate large exports | Cache of shown clusters (up to 8 UTF-16 units, 65536 entries) |

## Measurements

- Exporter alone, 500 pages of 48 runs and a logo: 513 KB, 2.4 s, about 2.8 MB transient allocation per page, nothing retained per page; one image object for the logo.
- Folio, 160 paragraphs (14 pages): 1 KB per page, 83 ms export, linear.
- Open: building a Folio editor scene of 300+ paragraphs in the test harness exceeds 5 minutes (before any export). The growth is in the editor scene/layout, not in the PDF writer; it needs its own investigation.

## Limits left

- Colour emoji fonts without outlines (sbix/CBDT) are still replaced by another font; images for them need a rasterizer in Formats.Pdf (Skia lives in Formats.Rasterizing) and shaping of the colour font file.
- No bidi reordering in Folio layout: an RTL span is reversed within itself, mixed-direction lines keep logical order between spans.
- Shaped widths differ from the layout's per-character widths; the span keeps the layout width.
- Object index: an older direct object can shadow a newer packed one with the same number (xref not followed).
- Visible signature uses non-embedded Helvetica: a signed PDF/A loses conformance when the visible option is chosen.
- Deck slides are captured as whole bitmaps: no JPEG pass-through there.

## Tests

Typography.Tests `ShapingTests` (6), Formats.Pdf.Tests `PdfShapingTests` (5), `PdfJpegPassThroughTests` (8), `PdfLargeExportTests` (1), Security.Tests `PdfSignerStreamTests` (5), Documents.UI.Tests `FolioLargeExportTests` (1). Runs: Formats.Pdf 76/76, Typography 110/110, Security 80/80, Architecture 40/40, Documents.UI PDF/glyph/print subset 11/11, Sheets.UI print subset 2/2; Desktop builds.

## Live checks

Preview and Acrobat: Folio with Arabic, Hindi and a family emoji (shaping, search and copy in logical order); sign a third-party PDF saved with compressed object streams (Acrobat, browsers), with and without the visible option (widget on page 1, signature panel valid); a CMYK JPEG from Photoshop and an Adobe RGB JPEG in Folio and Ledger exported to PDF (colours, Acrobat Output Preview shows DeviceCMYK/ICCBased); a 500-page Folio export (time, memory).
