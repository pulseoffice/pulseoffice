# Round 94: bidirectional text in Folio (and shared typesetting)

Limit found by round 93: Folio had no paragraph direction, so right-to-left text was laid out and navigated only logically.

## Coverage log (files read, lines read/total, outcome)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Documents.Layout/Lines/LineComposer.cs | 177/177 | runs emitted in logical order only; alignment physical |
| src/PulseOffice.Documents.Layout/Inline/ShapedParagraph.cs | 106/106 | no levels |
| src/PulseOffice.Documents.Layout/Inline/ParagraphShaper.cs | 161/161 | no direction |
| src/PulseOffice.Typography.Shaping/HarfBuzzTextShaper.cs | 119/119 | direction guessed per span (complex spans already drawn RTL inside a run) |
| src/PulseOffice.Typography.Shaping/ComplexScriptSegmenter.cs | 101/101 | Hebrew/Arabic spans include trailing neutrals |
| src/PulseOffice.Documents.Layout/Queries/LineOffsets.cs | 118/118 | x of an offset and hit-testing assume x grows with the offset |
| src/PulseOffice.Documents.UI/Rendering/GlyphRunPainters.cs | 200/200 | complex spans shaped by HarfBuzz; plain characters painted left to right |
| src/PulseOffice.Formats.Pdf/PdfTextComposer.cs | 124/124 | ActualText carries the logical text of shaped spans |
| src/PulseOffice.Documents.UI/Editing/CaretNavigator.cs | 150/150 | Left/Right step logically |
| src/PulseOffice.Documents/Styles/Properties.cs, DirectFormatting.cs, StyleResolver.cs | 223/223, 193/193, 137/137 | no direction in paragraph or character properties |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxParagraphCodec.cs | 110/110 | w:bidi ignored |
| src/PulseOffice.Formats.Odf/Odt/OdtProperties.cs | 464/464 | style:writing-mode ignored for paragraphs; text-align already logical (start/end) |
| src/PulseOffice.Documents.Layout/Inline/InlineInserter.cs | 75/75 | **defect (medium)**: the rebuilt paragraph dropped `Drop` (drop cap lost when a paragraph has inline objects/citations/deletions); fixed, carries `Drop` and `Direction` |
| src/PulseOffice.Documents.Layout/Flow/ParagraphEntries.cs | 122/122 | frame margins are start/end in the model; kept, mirrored by the composer |
| src/PulseOffice.Documents.Layout/Lines/LineBreaker.cs | 60/153 | width symmetric in the margins: no change needed |
| src/PulseOffice.UI.Controls/Text/Rich/TextTypesetting.cs | 400/400 | Deck/Ledger shape text always `FlowDirection.LeftToRight` |
| src/PulseOffice.Text.Rich/RichTextFormatter.cs | 60/535 (interface, ReadParagraph, SetAlign) | no direction |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextStyleTables.cs, OdfTextWriting.cs, OdfTextReading.cs | 100/…, 50/…, 245/… | ODP shape paragraphs: no writing-mode |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/LevelStyles.cs, DrawingTextReading.cs, DrawingTextWriting.cs | 210/…, 130/…, 70/… | PPTX `a:pPr/@rtl` ignored |
| src/PulseOffice.Documents.Layout/Flow/ParagraphEntry.cs | 75/145 | wrap segments narrowed margins physically: wrong side for RTL; now `ParagraphFrame.Narrowed` |
| src/PulseOffice.Documents.Layout/Flow/Stories.cs | 20/573 (decoration) | background/border box used the start margin as the left edge; now `LeftEdge` |

## Design

- **Model**: `TextDirection` (`LeftToRight`, `RightToLeft`) in `PulseOffice.Text.Rich.Bidi`. `TextParagraph.Direction` and
  `ParagraphProperties.Direction` (style-inheritable, direct formatting, style resolver); `TextRun.RightToLeft` and
  `CharacterProperties.RightToLeft`. Alignment and margins stay logical: Left = start, MarginLeft = start indent (as DOCX).
- **Algorithm**: `IBidiClasses` (`UnicodeBidiClasses`, range and category based), `IBidiMirroring` (`BidiMirroring`:
  Bidi_Mirrored pairs and paired brackets), `IBidiAlgorithm` (`BidiAlgorithm` + internal `BidiResolution` and
  `BidiIsolatingSequence`: P2/P3, X1–X10 with the 125 depth limit, W1–W7, N0 with the 63-entry bracket stack, N1/N2,
  I1/I2), `IBidiReordering` (`BidiReordering`: L1 per line, L2 visual order). Run-level RTL becomes virtual RLE/PDF
  around the run (`BidiEmbedding`).
- **Folio layout**: `ILineBidi` (`LineBidi`) resolves a paragraph once (cached per `ShapedParagraph`, skipped entirely
  for paragraphs with no RTL character, control or direction), gives L1 levels per line and run starts at level changes
  and between right-to-left "atoms" (a joined Hebrew/Arabic sequence, otherwise one text element). `LineComposer` splits
  runs there, tags `GlyphRun.Level`, then `Arrange` places the content runs of each tab segment in visual order, mirrors
  bracket glyphs at odd levels and reflects the whole line for RTL paragraphs (start margin, indent, list label, drop cap
  and end alignment land on the right side). `ComposedLine.RightToLeft`/`Bidirectional` drive caret and hit-testing.
- **Queries**: `LineOffsets.XOf`, `OffsetAt`, `SpanBetween` measure inside RTL runs from their right edge; `OffsetAt`
  searches runs in visual order on bidirectional lines.
- **Caret**: `CaretNavigator` Left/Right on a bidirectional line move to the nearest caret stop in the arrow's direction
  (ties broken by logical distance); at the visual edge they leave the line in reading order. Selection stays logical.
- **Painting, print, PDF**: unchanged painters; complex RTL atoms are shaped by HarfBuzz as before, so screen, print and
  PDF share the same visual order, and PDF `ActualText` of shaped spans stays logical.
- **Deck and Ledger**: the shared `TextBlockTypesetter` sets `FlowDirection` and Start/End alignment and mirrors margins;
  `IRichTextFormatter.SetDirection`/`ParagraphFormat.Direction`; commands `deck.paragraph.leftToRight/rightToLeft`,
  `ledger.shapeText.leftToRight/rightToLeft` (one undo step, «history.draw.shapeTextDirection»), `folio.paragraph.*`.
- **Formats**: ODT `style:writing-mode` (margins swapped to physical, `left`/`right` text-align mirrored), DOCX `w:bidi`
  and `w:rtl`, ODT run direction as `pt:right-to-left`, ODP `style:writing-mode`, PPTX `a:pPr/@rtl` (algn and marL/marR
  mirrored, they are physical).

## Defects found

| Severity | Where | Defect | Status |
|---|---|---|---|
| medium | src/PulseOffice.Documents.Layout/Inline/InlineInserter.cs:50 | the rebuilt paragraph lost its drop cap | fixed |
| medium (introduced and caught) | src/PulseOffice.Documents.Layout/Lines/LineBidi.cs | per-line RTL scan made long paragraphs quadratic (three timing tests failed) | fixed before landing: cached per paragraph |
| feature gap | layout, caret, formats | no paragraph direction / bidi | implemented |

## Tests

- `tests/PulseOffice.Text.Rich.Tests/BidiAlgorithmTests.cs` (17, hand-encoded UAX #9 cases).
- `tests/PulseOffice.Documents.Layout.Tests/BidiLayoutTests.cs` (10).
- `tests/PulseOffice.Documents.UI.Tests/FolioBidiNavigationTests.cs` (3, headless).
- `tests/PulseOffice.UI.Controls.Tests/RichTextDirectionTests.cs` (4).
- `tests/PulseOffice.Formats.OpenXml.Tests/BidiParityTests.cs` (3, ODT/DOCX), `DeckBidiParityTests.cs` (1, ODP/PPTX).

## Limits

- Character classes come from ranges and general categories, not from UnicodeData's Bidi_Class table: rare symbols in
  Arabic/Hebrew blocks may be classified AL/R instead of ON; no full BidiCharacterTest.txt run.
- Caret stops at a direction boundary share one x; the caret is drawn at the trailing edge of the earlier run.
- Mirrored bracket glyphs are substituted in the glyph run, so a PDF copy of a lone mirrored bracket yields the mirrored
  character (shaped spans keep logical `ActualText`).
- Folio rulers, the paragraph dialog and tab stops stay physical/left-based in RTL paragraphs; tabs in RTL paragraphs
  are laid out from the start edge in the mirrored line.
- Deck/Ledger shape text: first-line indent and list labels of RTL paragraphs stay on the left.
- Not verified live, in Word, PowerPoint or the reference suite (headless round trip not run in this round).
