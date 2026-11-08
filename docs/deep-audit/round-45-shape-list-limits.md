# Round 45: shape list limits (numbering schemes, start value, soft line breaks, hostile lvl)

Follow-up of round 42 ("Remaining") and round 41.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Drawing/Shapes/ShapeListItem.cs | 1-16/16 | arabic only, no start value |
| src/PulseOffice.Drawing/Shapes/ShapeTextLists.cs | 1-91/91 | ok |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingListMarkers.cs | 1-89/89 | always `arabicPeriod`, scheme and `startAt` dropped on read |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingShapeTextRuns.cs | 1-133/133 | `a:br` becomes a paragraph |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextReading.cs | 1-125/125 | `lvl` read through `.Value` |
| src/PulseOffice.Drawing/Shapes/ShapeRichText.cs | 1-145/145 | numbering not carried |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextLists.cs | 1-120/120 | num-format ignored |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextRuns.cs | 1-181/181 | `text:line-break` becomes a paragraph |
| src/PulseOffice.Text.Rich/ListLabels.cs | 1-111/111 | labels `n.` only |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/LevelStyles.cs | 1-140/~200 | scheme of `a:buAutoNum` dropped |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextReading.cs | 1-120, 220-250 | `style:num-format`, affixes and `text:start-value` dropped |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextWriting.cs | 96-300 | always `num-format="1"` + `.` |
| src/PulseOffice.UI.Controls/TextFormat/ListFormat.cs | 1-131/131 | dialog already knows letters, romans, affixes, start |
| src/PulseOffice.UI.Controls/TextFormat/ParagraphListFormats.cs | 1-57/57 | offers arabic only, no affixes/start |
| src/PulseOffice.UI.Controls/TextFormat/ListFormatTabs.cs | 1-54/54 | ok |
| src/PulseOffice.Drawing/Shapes/ShapeTextParagraphs.cs | 1-39/39 | ok |
| src/PulseOffice.Text.Rich/ParagraphLines.cs | 1-160 | U+2028 already a hard break |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs | 180-335 | fidelity rules: PPTX222 added |
| src/PulseOffice.Formats.OpenXml/XlsxFormat.cs | 80-160 | fidelity: XLSX184 added |
| src/PulseOffice.UI.Controls/TextFormat/ListLevelsEditor.cs | 100-140, 260-290 | affixes and start already editable |

## Defects

1. **Medium (hidden loss)** - `a:buAutoNum@type` and `@startAt` were dropped on reading and every numbered paragraph was
   written back as `arabicPeriod` from 1, in Deck text boxes and in Deck and Ledger shape text; ODF
   `style:num-format`, `style:num-prefix`/`num-suffix` and `text:start-value` were dropped the same way (written back as
   `1` + `.`). A PPTX list numbered `a)` from 3 came back as `1.` from 1 in every format.
2. **Medium** - a soft line break (`a:br`, `text:line-break`) in shape text became a new paragraph (round 42 kept the list
   items in place by adding non-list paragraphs, but the break itself was lost: one list item became two paragraphs).
3. **Minor** - Deck `DrawingParagraphReader` read `lvl` through `.Value`: a non-numeric `lvl` threw on reading.
4. **Minor (duplication)** - `ListLabeler` (UI.Controls) had its own letters/roman code: now the shared
   `ListNumberFormats` of Text.Rich.

## Fixes

1. Shared model `ListNumberFormat` (Text.Rich: style arabic / letters / romans, prefix, suffix, start 1..32767, native
   `DrawingScheme` and `OdfFormat` carried for schemes the other format has no name for), normalised by
   `IListNumberFormats` (also the labeller: `NumberMarker` and `ListLabeler` use it). Carried by `TextParagraph.NumberFormat`
   and `ShapeListItem.NumberFormat` (`ShapeRichText` passes it both ways).
   - DrawingML: `IDrawingNumberSchemes` (table of the 16 exact schemes + 25 native ones kept by name; `startAt` read with
     tolerant parsing); used by `LevelStyleReader` (inherited through `LevelStyle.Over`) and `DrawingListMarkers`.
   - ODF: `IOdfListNumbers` reads/writes `num-format`, affixes, `start-value`, and our `pt:number-scheme`
     (`urn:pulseoffice:text`) for a native DrawingML scheme; used by `ListPlacement` and `ListStyleBuilder` (Deck
     `OdfRichTextWriter` makes an automatic list style per format; shape lists group by format).
   - Declared: PPTX222 / XLSX184 when a numbering has affixes or an ODF format DrawingML has no scheme for (written with
     the nearest scheme). ODF keeps every format.
   - Dialog: `ParagraphListFormats` offers all five number styles, affixes and start value (Deck and Ledger); the native
     scheme is kept while style and affixes are unchanged.
2. Soft breaks in shape text are `ShapeTextRun.LineBreak` (U+2028, already a hard line break for the shared typesetter):
   read from `a:br`/`text:line-break`, written back as `a:br`/`text:line-break`, inside the paragraph and its list item.
3. `DrawingParagraphLevels` (tolerant `lvl`, clamped 0..8) shared by Deck text and shape lists.
4. Deck paste of rich text (`DeckRichTextTransfer.Plain`) and outline import (`DeckSlideActions`) carry `NumberFormat`.

## Tests

New: `ShapeListNumberingTests` (Formats.OpenXml: schemes and start through XLSX, ODS and crossed; native
`circleNumDbPlain` and hostile `startAt` through XLSX→ODS→XLSX; XLSX184/PPTX222; soft breaks in XLSX and ODS, listed and
plain; Deck text numbering through PPTX/ODP both ways and hostile `lvl`), `ListLabelsTests` (labels, hostile format),
`ParagraphListFormatsTests` (UI.Controls), `ShapeTextLineBreakTests` (Drawing), `LedgerShapeListTests` (dialog numbers
the shape with romans, affixes and start). Updated: `SheetShapePictureBulletTests` (a soft break now stays inside its item).
Runs: Formats.OpenXml 2348, Formats.Odf 783, Text.Rich 105, Drawing 321, UI.Controls 367, Sheets.UI shape filter 5,
Presentations.UI list/text filter 13: all green.
Corpus `one`: `pptx/NumberedList-12ab-ab-34.pptx`, `odp/invalidBuAutoNumEnumValue.odp` (PPTX222 raised: `I` without suffix
has no DrawingML scheme), `odp/num-to-bullet.odp`, `odp/BulletsAsImage.odp`, `odp/bullet-off-on.odp`: OK, LOSS 0.

## Remaining

- ODF writes one number format for all nine levels of an automatic list style (as for bullets): consecutive items
  with different formats go into separate `text:list` elements, so other ODF readers restart the numbering there.
- The in-place shape editor inserts paragraphs only (Enter); typing a soft break there is not offered.
