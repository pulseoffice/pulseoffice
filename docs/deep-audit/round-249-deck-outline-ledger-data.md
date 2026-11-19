# Round 249: Deck outline cut and paste, slide drag threshold, Ledger data tools

Audit of the two queued Deck items (outline identity through cut and paste, slide drag threshold) and of Ledger's data
tools: sort, filters, validation, remove duplicates, text to columns, subtotals.

| Severity | Defect | Fix |
|---|---|---|
| Medium | Identical outline blocks swapped by cut and paste were matched in order, so the slides kept their old places and the formatting of the two slides was exchanged. | `OutlineBlockTransfer` tells whether a cut covers whole blocks and where pasted blocks landed; the outline view detaches the cut slides on cut (`TextBox.CuttingToClipboardEvent`) and reattaches them at the paste position when the text becomes the cut-down outline with exactly those blocks inserted. Cut slides that are never pasted are deleted when the outline is applied. |
| Medium | Slide drag in filmstrip and sorter had no movement threshold: a press released on a neighbouring frame moved the slide. | `SlideDragSession` arms only after the pointer leaves the platform tap size (`IPlatformSettings.GetTapSize`), otherwise the release is a plain selection. |
| Medium | XLSX validations without `allowBlank` were read as ignoring blank cells, but the schema default is false (Excel omits the attribute when "Ignore blank" is unchecked). | Missing `allowBlank` reads as false. |
| Minor | Text to columns wrote empty parts as empty text cells, which count as non-blank (COUNTA, ISBLANK). | Empty parts become blank cells. |

## Still open
- Cut of the last block leaves a trailing empty line in the outline text, so its paste is not recognised as the same
  block and the slide is recreated instead of kept.
- ODS `table:display-list="sorted"` is read as a dropdown but written back as `unsorted`.
- Remove duplicates compares stored values, not the shown (formatted) values as Excel does.
- The drag threshold and the outline cut and paste have not been seen in the running app.
