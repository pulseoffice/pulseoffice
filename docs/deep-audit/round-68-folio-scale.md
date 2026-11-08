# Round 68: Folio at scale, embedded font for the visible PDF signature

Two items left open by round 64 ([round-64-pdf-scripts.md](round-64-pdf-scripts.md)).

## 1. Building a large Folio scene was superlinear (severe)

### Method
No `dotnet-trace`/`dotnet-counters` on this machine. A Stopwatch probe built the editor scene
(`FolioScene`, headless Avalonia) at 40/80/120/160/240/320 paragraphs; while the 240-paragraph case was stuck the
test host was sampled with macOS `sample` and the managed frames were resolved through the runtime perf map
(`DOTNET_PerfMapEnabled=1`, `/tmp/perf-<pid>.map`).

### Root cause
Of 3,612 samples on the UI thread, 3,185 were in `FolioCanvas.Render` → `PageRenderer.Paint` → every glyph run of
**every page**. The page cull in `FolioCanvas.Render` computed the bottom of the visible band as
`offset + max(viewport height, Bounds.Height)`; the canvas `Bounds` is the whole scrollable extent of the document,
so the band always covered all pages. Every frame (layout changes, scrolling, and the caret blink twice a second)
repainted the whole document: O(pages) per frame. Once a frame took longer than the render timer period, the
dispatcher always found a new render job queued and `RunJobs` never drained, which is the cliff between 160 and 240
paragraphs. In the app the same defect made typing and the caret blink cost a full-document paint.

Not the cause (checked): layout (`DocumentLayoutEngine` relayout is incremental with prefix/suffix probes), undo
snapshots, spelling (none in the harness), HarfBuzz (the harness uses `FixedPitchMeasurer` and `FormattedRunPainter`).

### Fix
`FolioCanvas.Render` culls to `offset .. offset + viewport height` (+50 px margin); only before the first arrange,
when the viewport is still empty, does it fall back to the canvas bounds.

### Measurements (headless scene build, this Mac)

| Paragraphs | Pages | Before | After |
|---|---|---|---|
| 40 | 4 | 1,055 ms | 755 ms (first case, includes warm-up) |
| 80 | 7 | 939 ms | 267 ms |
| 160 | 14 | 870 ms | 264 ms |
| 240 | ~20 | did not finish within the 300 s timeout | — |
| 320 | 27 | — | 281 ms |

Scale tests (serial collection `Folio performance`, `FolioScaleTests`) record their numbers in
`$TMPDIR/pulseoffice-folio-scale.txt`; see «Results» below.

## 2. HarfBuzz shaping caches

`HarfBuzzTextShaper` already kept an LRU of parsed faces (blob, face, font; 12 by default). Shaped runs were not
cached: `FaceGlyphMapping` (screen) reshaped every complex-script span on every paint. New decorator
`CachedTextShaper` (bounded dictionary keyed by face identity, text and span; cleared when full, 8,192 runs by
default) wraps the HarfBuzz shaper in the Folio and desktop compositions. The PDF font set keeps its own cluster
cache.

## 3. Visible PDF signature with an embedded font (PDF/A)

The widget appearance used the non-embedded standard Helvetica, so a visibly signed PDF/A lost conformance.

- `PulseOffice.Security`: `IPdfSignatureFont` (Strategy) typesets the appearance lines into objects appended through
  `IPdfAppendedObjects` (the incremental update); `StandardPdfSignatureFont` keeps the previous Helvetica behaviour
  for callers without fonts. `PdfSignatureWidget` no longer knows any font.
- `PulseOffice.Formats.Pdf`: `EmbeddedPdfSignatureFont` reuses the export font machinery (`IPdfFontSetFactory`,
  fallback families, TrueType subsetting, ToUnicode) and writes the subset fonts through `IPdfObjectSink`, the
  interface extracted from `PdfFile` that `IPdfFontResource.Write` now takes. Text is shown with `TJ` and Identity-H
  codes, so names outside WinAnsi (Greek, Cyrillic, accents) are drawn instead of `?`.
- Desktop composition registers `EmbeddedPdfSignatureFont`; `PdfSigner` gets the font through its constructor.
- `PulseOffice.Formats.Pdf` now references `PulseOffice.Security` (no cycle: Security references only Foundation).

Limit: if no installed font with TrueType outlines is found, the font set falls back to the standard fonts and the
appearance is again non-embedded (same rule as the export).

## Tests

- `tests/PulseOffice.Documents.UI.Tests/FolioScaleTests.cs` (serial collection): 2,000 paragraphs build and export to
  PDF within bounds; scene time grows linearly (250 vs 1,000 paragraphs); typing latency in a 500+ page document.
- `tests/PulseOffice.Typography.Tests/CachedShapingTests.cs`: repeated runs shaped once; bounded cache.
- `tests/PulseOffice.Formats.Pdf.Tests/PdfVisibleSignatureFontTests.cs`: visibly signed PDF/A appends a subset
  `FontFile2` with ToUnicode, no Helvetica, signature reads back unbroken.

## Results

After the fix (Debug build, headless, this Mac):

- 2,000 paragraphs, 167 pages: scene built in 0.72 s, built and exported to PDF in 1.38 s (bounds: 30 s / 90 s).
- Scene with 250 paragraphs 119 ms, 1,000 paragraphs 158 ms (linear bound: less than 12x).
- 6,000 paragraphs, 500 pages: 80 ms per typed key including relayout and repaint (bound: 250 ms).
- Before the fix the 240-paragraph scene did not finish within the 300 s test timeout.

Test projects run (one at a time): Documents.UI 339/339, Documents.Layout 229/229, Typography 112/112,
Formats.Pdf 77/77, Security 80/80, Architecture 40/40. Desktop builds.
