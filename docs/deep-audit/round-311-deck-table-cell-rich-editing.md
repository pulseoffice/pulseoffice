# Round 311: Deck table cell rich editing and paragraph alignment

Area: Deck table cell editing (queue items 302, 306): the plain text box cell editor and the per-cell-only paragraph
alignment.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | Paragraphs of one table cell could not carry their own alignment: PPTX `a:pPr/@algn` and ODP paragraph alignment differing between paragraphs collapsed to the cell look; in PPTX tables without their own look the alignment was dropped entirely. | `TableCellParagraph.Alignment` (null = the cell look's), `AlignedOver`, `AlignedBeyond`; part of equality and `IsPlain`; `Rebased` keeps it by paragraph index. PPTX writes `algn` per paragraph (paragraph over look) and reads it per paragraph, dropping what equals the look (no `algn` = left). ODP writes an automatic paragraph style (`fo:text-align`) on the cell's `text:p` and reads it back (the reader's text style table now also holds paragraph styles). The cell renderer aligns each paragraph. |
| Medium | The cell editor was a plain text box: no character formatting for part of a cell's text, runs not shown while editing, caret not on the drawn text. | The cell is edited with the shared `ShapeTextEditor` (the Deck shape text editor instance): `DeckTableCellShapes` turns the cell's paragraphs and runs into a transient frameless rectangle `ShapeObject` styled from the cell look (family, size, colour, vertical anchor, inset; effective bold/italic/underline on the runs so a part can be unformatted) and back. While editing, the table is drawn without that cell's text and the live shape is painted over the cell (`SlidePalette.Overlay`, `SlideView.LiveOverlay`), so text, caret and selection come from the same composed frame. Character formatting commands reach it through `DeckShapeTextEditing.ApplyShapeTextFormat`, routed to `IDeckCellTextTarget`; a collapsed caret pends the format as in shapes. Commit writes value and runs in one undo step (`IDeckTableKeys.WriteRich`); Escape cancels, Tab/Shift+Tab commit and move as before. Line breaks inside a paragraph cross as the shape line break. |

## Limits kept

- Strikethrough has no shape run attribute: it is carried from the cell's runs by rebasing them onto the edited text,
  so text typed while editing takes the strike of the text before it, but strike cannot be toggled while editing (the
  cell look command still applies it to whole cells).
- Paragraph alignment is kept per paragraph through an edit, but the editor composes all paragraphs with the first
  paragraph's alignment, and the alignment commands while editing do not change one paragraph's alignment.
- Turned (up/down) and stacked cells are edited horizontally, as before.
- Justified alignment has no `CellHorizontalAlign` value and still reads as left.

Not seen live; no build or test run in the round (audit cycle rule).
