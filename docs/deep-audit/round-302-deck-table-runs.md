# Round 302: Deck table runs, cell editor and grid looks (2026-11-20)

Area: the Deck table items queued by rounds 297 and 298.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Serious | Per-run formatting inside a table cell was flattened to one cell look on reading PPTX and ODP (declared as PPTX225 and ODF281), so a bold word in a cell was lost or spread to the whole cell. | The table model now carries `DataTableObject.CellTexts`: per cell, paragraphs (`TableCellParagraph`) of runs (`TableTextRun`) whose properties are optional overrides of the cell look. The snapshot keeps the plain value; a cell's runs apply only while their plain text matches the value (`TableCellIndex.Text`), so any edit that changes the value without knowing about runs simply drops back to the plain text. Row and column insertion and deletion move the runs with their cell. PPTX writes `a:r` with only the overrides for grid tables (the full look over them for own-look tables) and reads them back merging adjacent equal runs; ODP writes `text:span` with automatic `text` styles and reads them through the content's text styles. The shared table renderer draws them (`ITableCellRichText`). The two losses are retired; the ODP span diagnostic class is emptied. |
| Medium | Typing into a Deck table cell made one undo step per keystroke and had no caret. | `DeckTableCellEditor`: typing into a selected cell, Enter or F2 open a text box over the cell with a real caret, selection, IME and clipboard; Escape cancels, Tab and Shift+Tab commit and move (Tab past the end adds a row), leaving the box commits. The session writes once through `IDeckTableKeys.Write`, so one edit session is one undo step. |
| Medium | A grid table was written to ODP without `table:template-name`. | The ODP writer names a `table:table-template` written in `styles.xml` by Folio's own template writer (`OdtTableTemplates`), fed with the grid look converted to `TableProperties` (`OdfDeckGridTemplates`, collected per write in `OdfTableTemplateSink`). |
| Medium | In PPTX the built-in Table Grid style had our lines but not our header tint and banding. | `PptxGridTableStyles` writes a "PulseOffice Grid" table style per accent in `tableStyles.xml` (whole-table lines and ink, `firstRow` tint and accent bold text, `band2H` fill) and the table names it; the reader recognises the style by name, keeps the table a grid and restores its accent. The colours come from one shared `ITableGridLook` in `PulseOffice.Document`. A table naming the built-in Table Grid with formatted runs but no cell properties now also stays a grid. |

## Research

DrawingML table cells hold ordinary `a:p`/`a:r` text bodies, so runs map natively; ODF table cells in presentations
hold `text:p` with `text:span`, also native. Table styles: ECMA-376 `a:tblStyleLst` entries in `tableStyles.xml`
(PowerPoint reads custom entries); ODF 1.3 `table:table-template` with `table:first-row`, `table:body`,
`table:even-rows` and friends, referenced by `table:template-name`.

## Open (queued)

- Grid header rows and stacked cell text draw the plain value; per-paragraph alignment is not modelled.
- The cell editor is a plain text box: editing drops a cell's runs, no partial character formatting, default font.
- ODP grid templates do not restore the accent on reading; neither format's grid style has been opened in
  PowerPoint or the reference suite.

## Not covered

Nothing from the area was skipped; the shared shape text editor was not reused for cells (see open items).
