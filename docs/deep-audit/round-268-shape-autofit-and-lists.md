# Round 268: Deck shape autofit, shape text lists, tagged shape text

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Serious | Shapes had no autofit in the model: PPTX `a:normAutofit`/`a:spAutoFit` and ODP `draw:fit-to-size`/`draw:auto-grow-height` on shapes were dropped silently on read, and the writers always wrote "no autofit". | `ShapeTextStyle.Autofit` (the shared `TextAutofit`); DrawingML writes `noAutofit`/`normAutofit`/`spAutoFit` in the shape body (PPTX, XLSX, and DOCX through the cloned body children) and reads them by local name; ODF writes `draw:auto-grow-height` and, for shrink, `draw:fit-to-size="shrink-to-fit"` with `style:shrink-to-fit="true"`, and reads either. |
| Medium | No editor command for shape autofit. | An Autofit choice in the shared shape style view (all three modules), texts in the five languages. |
| Medium | Shrink text on overflow and resize shape to text were not rendered. | `IShapeTextShrink` scales single-column plain shape text to fit (bisection down to 25%); `TextFrameFitting` resizes Deck shapes whose autofit is "resize shape" on every element update, as for text boxes. |
| Medium | Shape text lists had no list keys: Tab moved focus, Enter on an empty item added another empty item, Backspace at item start joined paragraphs. | `IShapeListKeys` (Drawing): Tab/Shift+Tab change the level of the items in the selection; Enter on an empty item outdents it, then ends the list; Backspace at the start of an item removes its bullet. Wired in the shared `ShapeTextEditor`. |
| Medium | Tagged PDF: shape text in columns reported no painted lines and fell back to text matching. | Column painting reports the line boxes cut to each column clip. |
| Medium | Tagged PDF: line boxes were reduced to axis-aligned boxes of the rotated text, so a shape rotated inside its element was located by overlapping boxes. | `RichTextLineBox.Placement` keeps the local box and its transform; the locator maps the point back into the line's frame. |
| Minor | `SlideTextLines` unused since round 265. | Emptied; the file is deleted at the join. |

## Research

PowerPoint stores shape autofit in `a:bodyPr` as one of `a:noAutofit`, `a:normAutofit` (with optional font scale and
line reduction) and `a:spAutoFit`. ODF 1.3 has `draw:auto-grow-height` and `draw:fit-to-size` (value
`shrink-to-fit` since 1.3) plus the older `style:shrink-to-fit`; writing both keeps older readers working.

## Open

Queued in docs/audit-queue.md with "(268)".
