# Round 204: Folio minor queue (line numbering, gutter, column widths, mirrored margins, move display)

Area: the Folio items left open by rounds 198 and 199 in `docs/audit-queue.md`. No build or test run in this round:
the coordinator runs `tools/run-tests.py --changed` at the join.

## Defects fixed

| Severity | Defect | Fix | Test |
|---|---|---|---|
| Medium | ODT kept the line numbering start value and the per-section restart only in our extension attributes, so other readers numbered from 1 without restarts; continuous Word sections with the same layout did not restart numbering when Word restarts per section. | Paragraphs carry `TextParagraph.LineNumberRestart`, laid out by `LineNumbers` (the restart table comes from the body paragraphs). ODT writes the start value and each per-section restart as the standard `text:line-number` paragraph property (marked `pt:implied-line-number` when it only restates the configuration) and reads a first-paragraph restart in other files as the start value; `pt:start` is no longer written but still read, `pt:restart-on-section` stays because the configuration has no standard flag. DOCX reads `w:lnNumType w:restart="newSection"` of a continuous section with the same layout as a restart of its first paragraph and writes such restarts as continuous sections; other restarts are declared (DOCX248, five languages). | `Round204FolioMinorQueueTests` (Layout), `Round204OdtFolioMinorQueueTests`, `Round204DocxFolioMinorQueueTests` |
| Medium | The gutter was not in the page dialog. | Gutter width and position (left or top) in the margins section of the page tab, written in the same undo step as the page style, bounded so the text keeps at least 1 cm; the preview shows it. Texts in the five languages. | `Round204FolioPageDialogTests` |
| Medium | Columns of different widths had no editor; the dialogs kept them only while the column count was unchanged. | The shared `ColumnLayoutEditor` (UI.Controls) gains an equal-widths switch and one width and spacing-after row per column; `ColumnLayout.Widths` carries them. `FolioColumnLayouts` converts between `SectionColumns` and the editor for the page and section dialogs, showing relative widths scaled to the text width and keeping the stored widths when they were not edited. `SectionColumns.KeepingSpansOf` is gone. Texts in the five languages. | `Round204FolioPageDialogTests` |
| Minor | Reading `w:mirrorMargins` marked every page style mirrored, symmetric ones included, so a document mixing mirrored and symmetric page styles came back different. | `DocxMirroredMargins` decides symmetry for the reader and the fidelity rules; the reader mirrors only the asymmetric page styles (all of them when none is asymmetric). DOCX037 remains only for asymmetric non-mirrored styles mixed with mirrored ones, which Word cannot express. | `Round204DocxFolioMinorQueueTests` |
| Minor | Changing the tracked-move colour or marks in the settings was drawn only when a paragraph was laid out again. | `IMoveDisplayOptions.Changed`, raised by `SettingsMoveDisplayOptions` for the three move settings; every open `FolioEditor` whose document holds tracked moves reflows at once (the markup cache is keyed by the display). | `Round204MoveDisplaySettingsTests` |

## Open items

Listed in the Folio section of `docs/audit-queue.md` (marked 204).

## Follow-up after the cycle 12 join

The editor's subscription to the shared `IMoveDisplayOptions.Changed` kept every closed Folio document alive (the
editor is never disposed on close), breaking the round 156 and 163 closed-document checks. The subscription moved
into `FolioMoveDisplayLink`, linked only while the `FolioView` is attached to the visual tree; on re-attach it compares
the current `MoveDisplay` with the last one seen and relayouts if it changed meanwhile. To bring `FolioEditor` under the
800-line cap (its exception in `DesignRulesTests` is gone), table-cell navigation and column resizing moved into
`FolioCellCursor` (`editor.Cells`), the selected object's frame entry, delete, resize, rotate and lock check into
`FolioSelectedObject` (`editor.Objects`), and the format brush into `FolioFormatBrush` (`editor.Brush`).
