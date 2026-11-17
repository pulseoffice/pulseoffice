# Round 155: plain-text follow-up (2026-11-17)

Scope: the items left open by round 152 (fontwork, equation shapes, ODT tracked-change notes and watermark, inline
chart data cells), then a sweep for other codec paths that still bypass `IOdfParagraphText` or `IDrawingPlainText`.
Tests: `Round155PlainTextTests` in the Odf and OpenXml test projects.

## Defects fixed
- **Fontwork text, ODS/ODP/ODT** (medium): written as raw `text:p`, read by paragraph value, so `text:s`, `text:tab`
  and `text:line-break` from other suites were dropped ("A  B" read as "AB"). New `OdfFontworkText` (over
  `IOdfParagraphText`) is used by the sheet and deck codecs; text moved out of `OdfFontworkGeometry`. Breaks become
  spaces, since the model is single-line.
- **Fontwork text, XLSX/PPTX** (medium): runs were joined and `a:br` was dropped, so the words on either side ran
  together. Now `IDrawingPlainText.Paragraph` with breaks turned into spaces.
- **Equation source** (minor): ODP and ODS wrote the fallback source into `draw:image` as one raw paragraph. PPTX wrote
  one `a:t` that held a raw newline and joined the `a:t` runs on read. Now one paragraph per line, through the shared
  services, both ways.
- **ODT watermark** (minor): raw text on write, and paragraphs joined with no separator on read. Now `OdfFontworkText`.
- **ODT tracked-change note** (minor): written as one raw `text:p` and read by value. Now one paragraph per line on
  write and `IOdfParagraphText.Line` on read.
- **Inline chart table cells, ODF** (medium): category names and series labels were written as raw `text:p` and read
  by value. Now `IOdfParagraphText` both ways.
- **Widening, ODS validation help and error messages** (medium): tabs and repeated spaces were written raw, and the
  markup from other suites was dropped on read. Now `IOdfParagraphText`.
- **Widening, dimension line labels in ODF** (minor): written raw, read by value. Now `IOdfParagraphText`.

## Checked without defects
- Plain `ShapeObject` text in PPTX (`DeckPresetShapeCodec`): multi-line text round-trips, because the shape text-run
  layer rewrites the paragraphs (`Pptx_drawn_shape_plain_text_keeps_its_lines` passes).
- Inline chart data in PPTX (category and series-name caches in `c:v`): tabs, repeated and leading spaces and breaks
  round-trip (test passed before any change).

## Open
- `OdtDrawings` trims the formula annotation (leading and trailing spaces of the source are lost, low).

## Not verified
These fixes were not cross-checked in the reference suite, OnlyOffice or the Microsoft applications. The ODP equation,
validation-message and dimension-label tests were written together with their fixes. Their old-code failure was
inferred from the code, not run.

## Status
Closed. `tools/run-tests.py --changed main` passed with no failures (10 projects; the command sweeps were skipped by
`SweepRule`).
