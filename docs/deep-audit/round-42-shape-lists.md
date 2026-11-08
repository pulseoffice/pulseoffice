# Round 42: adversarial review of the shape-text lists of round 41

## Coverage

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Drawing/Shapes/ShapeListItem.cs | all | no margins: source indents dropped (defect 3) |
| src/PulseOffice.Drawing/Shapes/ShapeTextLists.cs | all | splice correct for one contiguous edit only (defect 1) |
| src/PulseOffice.Drawing/Shapes/ShapeTextParagraphs.cs | all | ok |
| src/PulseOffice.Drawing/Shapes/ShapeTextEditing.cs | all | prefix/suffix diff: one region per call |
| src/PulseOffice.Drawing/Shapes/ShapeRichText.cs | all | ok |
| src/PulseOffice.UI.Controls/Shapes/ShapeTextEditor.cs | all | `Live` diffs the whole session against the start text (defect 1) |
| src/PulseOffice.UI.Controls/Shapes/ShapeStyling.cs | all | text never changed by the style dialog: no desync |
| src/PulseOffice.Sheets.UI/LedgerShapeListCommands.cs | all | ok |
| src/PulseOffice.Sheets.UI/LedgerViewParts/LedgerShapeTextSession.cs | all | ok; the grid draws `LiveShape` with the list renderer |
| src/PulseOffice.UI.Controls/TextFormat/ParagraphListFormats.cs | all | faithful move from Deck |
| src/PulseOffice.Presentations.UI/DeckTextDialogCommands.cs, DeckShapeTextEditing.cs, DeckView.cs | round-41 diff | faithful move, no regression |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingListMarkers.cs | all | marL/indent overwritten (defect 3) |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingShapeTextRuns.cs | all | `a:br` shifts list items (defect 2) |
| src/PulseOffice.Formats.OpenXml/DrawingML/Text/DrawingTextWriting.cs | round-41 diff | marker writer extracted unchanged |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextLists.cs | all | list style serialised per paragraph (defect 4) |
| src/PulseOffice.Formats.Odf/Shapes/OdfShapeTextRuns.cs | all | `text:line-break` shifts list items (defect 2) |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextReading.cs (ListPlacement) | 15-83 | margins read but dropped by the shape codec |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfTextWriting.cs (ListStyleBuilder) | 96-190 | `Geometry`/`Place` reusable for shape lists |
| src/PulseOffice.Formats.Odf/TextCodecs/OdfParagraphText.cs | 99-160 | line-break read as `\n` |
| src/PulseOffice.Text.Rich/ListLabels.cs | 1-80 | non-list paragraph does not restart numbering of deeper levels only |

## Defects

1. **Grave** - in-place editor: `ShapeTextEditor.Live` rebuilt the shape from the start-of-session text with one
   prefix/suffix diff, so two edits in different paragraphs (Enter in one, Backspace joining two others) were seen as one
   region and the list items after the first edit were reassigned (an item lost, another duplicated).
2. **Medium** - a line break inside a listed paragraph (`a:br`, `text:line-break`) becomes a new model paragraph, but the
   readers added one list item per source paragraph only: every following item shifted by one paragraph.
3. **Medium** - silent loss: the left margin and first-line indent of list paragraphs (`marL`/`indent`, ODF list-level
   geometry) were dropped on reading and regenerated per level on writing.
4. **Minor** - ODF writing serialised the whole list style (with base64 pictures for nine levels) once per listed
   paragraph to deduplicate it: heavy on long picture lists.

## Fixes

1. `ShapeTextEditor` follows every text change of the box (`Follow`): the base shape is spliced one contiguous edit at a
   time, so list items stay with their paragraphs whatever the order of edits. Test
   `ShapeListTextTests.Due_modifiche_in_paragrafi_diversi_dell_editor_tengono_i_punti_al_loro_posto` (failed before).
2. `DrawingShapeTextRuns` and `OdfShapeTextRuns` add a non-list item for every line break read, so following items keep
   their paragraph (a soft break inside a list item has no model: its continuation line is a plain paragraph).
3. `ShapeListItem.Margin`/`Indent` (points, clamped and finite in `ShapeTextLists`): read from `marL`/`indent` and from the
   ODF list-level geometry, written back (DrawingML attributes; ODF through `ListStyleBuilder.Geometry`/`Place`), carried
   by `ShapeRichText` as `MarginLeft`/`Indent` so the renderer uses them; changing a paragraph's level from the dialog
   drops them and the per-level default applies. ODF groups consecutive items into one `text:list` whose style carries
   the geometry of every level (numbering is not restarted by different margins).
4. ODF list styles are registered once per group (cached), not serialised per paragraph.
5. Hostile `lvl` (not a number) no longer read through `.Value`: treated as level 0.

Tests: `SheetShapePictureBulletTests` (margins through XLSX, ODS and crossed; `a:br` and invalid `lvl`; ODS
`text:line-break`). Runs: Formats.OpenXml 2342, Formats.Odf 783, Drawing 320, UI.Controls 365, Sheets.UI and
Presentations.UI shape/list filters: all green. Corpus `one`: `odp/BulletsAsImage.odp`, `odp/bullet-off-on.odp` OK.

## Remaining

- The in-place editor overlays a transparent TextBox on the shape drawn with lists: the caret and selection do not
  follow label indents (caret drawn at the unindented position). Not contained: needs the shared rich-text editor.
- Deck `DrawingTextReading` still reads `lvl` through `.Value` (Deck area, not touched).
- Numbering scheme and start value of shape lists (Deck and Ledger) are arabic only: pre-existing model limit.
