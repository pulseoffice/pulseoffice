# Round 287: image and shape leftovers

Area: the queue items Images (282, 285), Deck images (282) and Shape text areas (285).

## Defects found

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Medium | No per-object aspect ratio lock: pictures always kept proportions on corner drags and shapes never; the Size dialogs of the three modules showed a lock toggle that was not stored | Fixed |
| 2 | Medium | `a:picLocks/@noChangeAspect` always written as true and never read; Word's `a:graphicFrameLocks` and shape `a:spLocks` ignored | Fixed |
| 3 | Medium | Compress Pictures offered a maximum edge in pixels rather than a resolution at the displayed size, and worked on one picture only | Fixed |
| 4 | Medium | Slide and master background images lost their `ImageTone` in PPTX and ODP | Fixed |
| 5 | Minor | Only the first of several `draw:text-areas` rectangles was used, also for vertical text | Fixed |
| 6 | Minor | A model rounded rectangle (no preset adjust values) draws elliptic corners at `CornerRatio` of each side and its text area follows them, while `roundRect` and the ODF round rectangle use circular corners on the short side: the outline and text area differ from other applications for non-square boxes | Queued |

## Fixes

- **Model.** `EmbeddedObject.AspectLock` (nullable) and `LocksAspectRatio` (explicit value, otherwise the type's
  default: raster and vector pictures on, everything else off); `WithAspectLock` stores only a departure from the
  default, so round trips keep records equal. `ObjectManipulator.KeepsAspect` reads it, so corner drags in Folio, Deck
  and Ledger follow the flag (Shift still inverts it).
- **Size dialogs.** The shared `SizeLockField` starts from the object's flag and its answer is stored in Folio's object
  properties, Deck's position and size dialog and Ledger's position and size dialog.
- **OOXML.** `DrawingAspectLock` (DrawingML) writes `a:picLocks/@noChangeAspect` from the flag, adds
  `a:spLocks/@noChangeAspect` to locked shapes (PPTX by element, XLSX by anchor name), writes
  `wp:cNvGraphicFramePr/a:graphicFrameLocks/@noChangeAspect` for locked DOCX objects, and reads all three.
- **ODF.** Research: ODF 1.3 has no aspect ratio attribute (`style:protect` covers position and size only, not
  proportions) and the reference suite keeps the setting only in the running document. The flag is written, when it
  departs from the default, as `pulsedraw:lock-aspect-ratio` (`urn:pulseoffice:drawing`) on the frame or shape in ODT,
  ODP and ODS (by `draw:name`), which other readers ignore. No fidelity code is needed.
- **Compress Pictures.** A shared `IPictureCompression` (UI.Controls) asks for a resolution as Word does (330, 220,
  150, 96 ppi or the original), the JPEG quality, whether to delete cropped areas and, when the document holds more
  than one picture, whether to apply to all of them. The target is measured on each picture's displayed size; a kept
  crop counts the hidden part. Folio (`IFolioObjectEditing.ReplaceContents`), Deck (`DeckActions.EditImages`) and
  Ledger apply every compressed picture in one undo step.
- **Background tone.** PPTX writes and reads `a:lum`/`a:biLevel` on the slide and master background blip (transparency
  stays with the layout opacity, `a:alphaModFix`); ODP writes the tone on `style:drawing-page-properties` in the
  `pulsedraw` namespace, because `draw:luminance` and its siblings are graphic properties only.
- **Text areas.** `IEnhancedPathInterpreter.TextArea(geometry, vertical)` takes the second rectangle of
  `draw:text-areas` for vertical text when there is one, as the reference suite does.

## Tests

`Round287AspectLockTests` (six formats), `Round287AspectLockDragTests`, `Round287PictureCompressionTests`,
`Round287BackgroundToneTests`, `Round287ShapeTextAreasTests`.

## Open

- Rounded rectangle outline (defect 6): making the model draw circular corners on the short side needs the outline
  to know the box's aspect, which `IShapeGeometry.Outline` does not receive today.
- The compression flow across several pictures and the new Size dialog checkbox are covered by service and format
  tests, not by a headless walk through the three modules' commands.
- Word, PowerPoint and Excel have not opened the written lock flags.
