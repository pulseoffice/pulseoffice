# Round 312: Ledger data validation leftovers

Area: the validation items queued by round 309 (error alerts, input messages, long XLSX lists, transposed paste and
clipped fill copies).

## Defects

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | Error alerts went to the status bar only: no modal, no error title, and warning and information rules accepted the value silently. | `IValidationAlertPrompt` (`ValidationAlertPrompt` over the shared `ChoicePromptView` and dialog host, with veil) asks with the rule's error title (or "Invalid data") and message: stop gives Retry/Cancel, warning "Continue?" with Yes/No/Cancel, information OK/Cancel. `ValidationAlertContents` builds the choices and is tested alone. Accept writes the value as one undo step; Retry and No reopen the editor on the cell with the typed text; Cancel keeps the old value. Enter or Tab no longer move the selection when the editor reopened. Paste, fill and object-linked writes keep the non-modal check. |
| 2 | Medium | Input messages appeared only in the status bar. | `LedgerInputMessageNote`: a themed note box beside and below the active cell with the input title in bold and the message, shown on selection, repositioned on scroll and zoom, hidden on cells without a message; its accessible name carries the text. |
| 3 | Medium | A typed list longer than Excel's 255-character `formula1` was written as is; Excel repairs the file and drops the rule. | Such a rule is left out of `dataValidations` and written whole in a `urn:pulseoffice:spreadsheet` worksheet extension (`XlsxLongValidationLists`) that our reader merges back; XLSX103 now says the rule is kept only there. |
| 4 | Medium | Transposed paste of validation only shifted the anchor; relative references were not turned. | Transposed paste rewrites both formulas with the `TransposingRewriter` used for conditional formats. |
| 5 | Checked, no defect | Fill copies clipped at the first row or column. | The reanchoring by the intersection's start already gives each cell its own references; a test now pins it. |

## Research on the long list

Excel accepts as a list source only a delimited string of at most 255 characters or a reference to one row or column;
a defined name holding an array constant is refused. The native alternative is a hidden helper sheet holding the
entries, referenced by the rule. That needs a projection of the workbook at write time (an extra sheet that is not in
the model) and its recognition on read; it is queued. Until then the extension keeps the round trip lossless for
PulseOffice and Excel opens the file without repair (without that rule, declared by XLSX103).

## Tests

- `Round312ValidationAlertTests` (Sheets.UI): choices and titles per alert style.
- `LedgerEditorTests` (Desktop): the note box; an invalid entry asks and follows Accept and Cancel; Retry reopens the editor.
- `Round312LongValidationListTests` (OpenXml): the long list stays out of `dataValidations` and round-trips.
- `Round312ValidationTransposeTests` (Sheets.Engine): transposed and plain paste, clipped fill copy.

## Open

- Long typed lists: hidden helper sheet mapping, so Excel sees the rule (queued).
- The alert modal and the note box not yet seen in the running app.
- The note box lives in Ledger only; a shared input-help note for Folio form fields could reuse it.
