# Round 276: Folio shape autofit and shape text keys

Area: the "Shape autofit (272)" and "Shape text keys (272)" queue items.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | A Folio shape with "resize shape to fit text" was fitted in layout only; the stored size kept the saved height, so DOCX and ODT were written with a stale size. | `IShapeStorySizes` (Documents.Layout) sets the object's stored height to the laid-out height of every non-inline resize-shape shape whose story has content. `FolioEditor` calls it after each fresh layout (opening, every edit, undo and redo) unless the document is read-only. The size is written straight to the object, not as its own history step: it follows the text edit that caused it, undo of that edit gives back the old text and the next layout settles the size again, so the edit and its fit act as one step, and opening does not mark the document modified. |
| Medium | Folio shape stories ignored the preset inner text rectangle (text ran to the edges of an ellipse or diamond). | The layout engine takes `IShapeTextArea`: the story is composed on the inner width and placed inside the inner rectangle (`PaginationRequest.FrameMargins`); a resize-shape shape grows by the inner area's share of its height. |
| Medium | Folio shape stories never shrank text on overflow. | With `ShrinkText`, the story is recomposed with run sizes scaled (resolved run size times the factor, at most six passes, not below 25 %) until it fits the inner height. |
| Medium | Vertical shape text was never resized: the shared fit returned nothing for vertical flows. | `IShapeTextFit.Size` measures vertical text along the shape's height and grows the width (as PowerPoint does); Deck's `TextFrameFitting` and Ledger's `LedgerShapeFitting` use it. Stacked text keeps its size. |
| Minor | The preset text area table covered nine presets; the rest used the whole box. | Added parallelogram, trapezoid, pentagon, hexagon, the stars, the block arrows, chevron, cross, heart, sun, cloud, smiley, no symbol, donut, folded corner, bevel, frame, plaque and the remaining flowchart shapes. |
| Minor | Shift+Tab on a plain paragraph of shape text moved focus out of the editor. | The shared editor keeps the key and removes a tab right before the caret, if there is one. |

## Research

The DrawingML preset definitions give each preset a text rectangle from guides that depend on the adjust values.
For the presets added here the default adjust values were used, and where the guide formulas involve the shorter side
(`ss`) the margins are expressed in it (block arrows, hexagon, parallelogram, trapezoid, folded corner, bevel, frame,
plaque); the stars, heart, sun, cloud and pentagon are close approximations of the inner polygon, not the exact guide
values. Word recomputes a resize-shape text box's height on opening; that is what the settlement reproduces.

## Open

- Folio shape stories have no vertical text layout, so vertical text in a Folio shape is not resized.
- The stored size settles only when an editor lays the document out; a headless conversion keeps the saved size.
- Inline (as-character) resize-shape shapes keep their stored size.
- The preset text areas ignore non-default adjust values except for the rounded rectangle.
- The fallback TextBox editor and text rotation about the inner area's centre, from round 272, are still open.

Queued in docs/audit-queue.md with "(276)".
