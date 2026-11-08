# Declared losses block B3: stand-alone VML shapes (DOCX102)

Block B3 of [../declared-losses-analysis.md](../declared-losses-analysis.md).

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxVmlReader.cs` | 84/84 | `Boxed` gate: only members with `v:textbox` reach the shape path; the rest falls to the picture path and DOCX102 |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxVmlShapes.cs` | 214/214 | rect/roundrect/oval/line/spt 1-6 mapped; `path`, `v:polyline`, `v:curve`, `v:textpath` not read |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxVmlPictures.cs` | 34/34 | image fill path, unchanged |
| `src/PulseOffice.Drawing/Freeform/FreeformPath.cs` | 126/126 | `FreeformObject` (normalised 0..1 nodes) already crosses DOCX (`DocxFreeformCodec`) and ODT (`OdtDrawings`) |
| `src/PulseOffice.Formats.OpenXml/OoxmlVectorPaths.cs` | 80/~120 | custom geometry is normalised to 0..1 |
| `src/PulseOffice.Formats.OpenXml/Drawing/VmlStyle.cs` | 34/34 | unitless lengths read as EMU; point lists now append `px` (CSS default) before measuring |
| `src/PulseOffice.Formats/FontworkWarps.cs` | 26/26 | OOXML warp tokens reused for VML text presets |
| `src/PulseOffice.Drawing/Fontwork/Fontwork.cs` | 40/~120 | `FontworkObject` already crosses DOCX and ODT |
| `src/PulseOffice.Drawing/Shapes/ShapeDefinition.cs` | 60/60 | `ShapeKind` catalogue used for the spt table |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxInlineReader.cs` | 290-330, 480-510, 590-600 | VML entry points; DOCX102 from `w:object` is the OLE path (out of scope) |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs` | 290-400 | `DocxFreeformCodec`, `DocxFontworkCodec`: writers used on the way back |
| `src/PulseOffice.Drawing/Shapes/ArcFlattener.cs` | 1-60 | arc sweep convention (clockwise = positive sweep, screen axes) |

## Corpus probe

59 files carry DOCX102. Dominant VML members: `v:shape` with a `v:shapetype` preset (spt) and formula paths,
`v:rect`, `v:oval`, `v:roundrect`, `v:line` without text boxes, WordArt (`v:textpath`), ink (`v:shape` with a plain
`path`). Not VML: `FileWithInvalidImageLink.docx`, a corpus file (broken image links), a corpus file,
`empty_group.docx`, a corpus file (`w:object`).

## Spec check

ISO/IEC 29500-4 (ECMA-376 Part 4) clause 19.1.2: `path` is 19.1.2.14, `polyline` 19.1.2.15, `shape` 19.1.2.19,
`shapetype` 19.1.2.20, `textbox` 19.1.2.22, `textpath` 19.1.2.23 (the analysis cited 19.1.2.22 for `textpath`:
corrected). The path grammar (`m l c x e t r v nf ns wa wr at ar qx qy`) is given with the `path` attribute of
`shape` (19.1.2.19); coordinates are in the `coordsize`/`coordorigin` space.

## Changes

- `DocxVmlShapes.Boxed` became `Drawable`: any VML member (`rect`, `roundrect`, `oval`, `line`, `shape`,
  `polyline`, `curve`) that does not hold an image is read, with or without a text box. Members with a text box that
  are neither filled nor stroked keep the plain text-box path (they are frames, and their stories may hold tables).
- New `VmlPathParser` (`IVmlPathParser`, `Formats.OpenXml/Drawing`): the VML path grammar `m l c x e t r v nf ns
  wa wr at ar qx qy`, omitted values as zero, normalised to the shape's `coordsize`/`coordorigin`; arcs become
  cubic quarter segments. Paths with formulas (`@n`) or other commands (`ae`, `al`, `h*`) are refused.
- New `VmlShapePresets`: spt -> `ShapeKind` for the presets the model has (rectangles, ellipses, polygons, stars,
  arrows, flowchart, symbols) and spt 136-175 -> text warp token.
- `v:shape` with an unknown spt and a numeric path, `v:polyline` and `v:curve` become `FreeformObject` (which both
  writers already handle: DrawingML `a:custGeom`, ODF `draw:path`).
- `v:textpath` with a string becomes `FontworkObject` (font family, weight, style, fill colour, warp).
- Lines, polylines and curves take size and position from their points; a line drawn bottom-left to top-right is
  flipped vertically.
- A `w:pict` holding only `v:shapetype` definitions draws nothing and no longer declares DOCX102.
- DOCX140 message widened: it now covers stand-alone shapes too (unknown preset or formula path -> rectangle).

## Still declared

- DOCX102 from VML now only for broken image links (`footer-margin-lost.docx`: the PNG part is missing;
  a corpus file: duplicate relationship ids), which are (d). The other DOCX102 rows come from DrawingML
  (`DocxDrawingCodec`: invalid picture links, `lockedCanvas`, empty `wpg:wgp`) and from `w:object` (ActiveX): out of
  this block.
- DOCX140: formula paths and presets outside the model (e.g. two corpus files).
- Text warps beyond the eight the model knows fall back to the nearest (as on the DrawingML path).

## Verification

- Tests: `PulseOffice.Formats.OpenXml.Tests` 2200/2200 (new `DocxVmlShapeTests`, `VmlPathParserTests`; the earlier text-box tests moved to `DocxVmlTextBoxShapeTests`, all passing).
  `PulseOffice.Architecture.Tests`: 39/40, the failure (`PresentationWriters.cs:_targets`) predates this block.
- First full corpus run found LOSS 3: spt 75 (picture frame without an image) had been mapped to the `Frame` shape,
  and a rectangle with both `v:textpath` and `v:textbox` became Fontwork, losing its text frame. Fixed (spt 75 removed,
  Fontwork only without a text box; stroke taken from the element as before, not from the shapetype).
- Second full run: **LOSS 0, DECL 321** (was 503); DOCX102 rows 124 -> 20.
