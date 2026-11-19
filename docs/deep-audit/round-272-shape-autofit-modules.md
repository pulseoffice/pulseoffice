# Round 272: shape autofit across modules and shape text keys

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | "Resize shape to fit text" grew Deck shapes only; Ledger shapes kept their size. | Shared `IShapeTextFit` (UI.Controls) measures the shape's composed text; Deck's `TextFrameFitting` and Ledger's `LedgerShapeFitting` (shape text edits, format dialog, colour and style commands) use it. Folio shapes with that autofit take the height of their story in layout (grow and shrink), where before they only grew. |
| Medium | The resize and the drawing assumed the text area is the whole bounding box. | `IShapeTextArea` (Drawing) gives the preset inner text rectangle (ellipse, diamond, triangles, rounded rectangle, octagon, cylinder, flowchart document and terminator, from the DrawingML preset definitions); the shape renderer, the live editor and the fit all use it. |
| Medium | Shrink on overflow was not applied to stacked text and measured multi-column text against one column. | The composer shrinks stacked text by its stacked extent and compares multi-column text with the height of all columns. |
| Medium | A shrinking text box kept the font scale read from the file, so a saved PPTX carried a stale `fontScale`. | The composer reports the factor it used (`ComposedTextFrame.FontFactor`); Deck's fitting stores it in `TextFrame.FontScale` (line reduction cleared) on every element update. |
| Medium | Tab on a plain (non-list) paragraph of shape text moved focus. | The shared editor inserts a tab character. |
| Minor | List keys were tested only on the model service. | Headless test walks Tab, Shift+Tab, Enter and Backspace through the live `ShapeTextEditor`. |

## Research

DrawingML preset definitions give each preset a text rectangle (`a:rect` with guides): the ellipse uses the inscribed
square (inset 1 - cos 45 degrees over 2 of each side), the diamond a quarter of each side, the isosceles triangle the
lower middle half, the flowchart document stops above the wave. ODF custom shapes carry `draw:text-areas`; for
computed geometry the whole box is kept.

## Open

Queued in docs/audit-queue.md with "(272)".
