# Round 309: Ledger conditional formatting leftovers and data validation

Area: the two conditional formatting items queued by round 304 (ODS style map size, per-icon picker), then a fresh
audit of Ledger data validation: list sources, in-cell dropdowns, input messages, error alerts, marking invalid data,
copy, paste and fill of validated cells, XLSX (`dataValidations`, x14 extension) and ODS (`table:content-validation`)
round trips. No build or test was run in the round (audit cycle rule); the join runs them.

## Defects

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Serious | Tools › Mark Invalid Data only counted cells holding an error value; it never checked validation rules and drew nothing. | Fixed |
| 2 | Medium | Pasting validated cells kept the rule's formula text while moving its anchor: a custom rule `A1>0` pasted to C1 still tested A1; a copy of the lower part of a rule shifted its relative references too. | Fixed |
| 3 | Medium | Pasting validation removed only rules wholly inside the target; a rule partly covered kept its cells under the pasted ones. | Fixed |
| 4 | Medium | The fill handle and Fill Down/Right/Up/Left did not carry data validation to the filled cells (the reference suites and Excel do). | Fixed |
| 5 | Medium | XLSX reading: a rule whose `sqref` lists several areas gave every area the same relative formula, so the second area tested the cells of the first. | Fixed |
| 6 | Medium | The input message of a rule was never shown when its cell was selected. | Fixed (status bar) |
| 7 | Medium | Rule editor: the per-icon picker was one long combo box of every icon of every set. | Fixed |
| 8 | Minor | ODS `style:map` cells: checked whole-column rules; writing is already per run of repeated rows and cells, one derived style per base style and rule set; a test now holds the size bound and its stability on reopening. | Verified, test added |
| 9 | Minor | Error alerts are reported in the status bar only: no modal with Retry/Cancel (stop), Yes/No/Cancel (warning) or OK/Cancel (information), and the error title is not shown; warning and information accept the value silently. | Open, queued |
| 10 | Minor | Input messages go to the status bar and its history rather than to a note box next to the cell. | Open, queued |
| 11 | Minor | XLSX: a literal list longer than Excel's 255-character `formula1` limit is written as is. | Open, queued |
| 12 | Minor | Transposed paste of validation and fill near the sheet's first row or column reanchor only the rule's anchor cell; relative references are not swapped. | Open, queued |

## Changes

1. `ConditionalIconPicker` (Sheets.UI): a button showing the chosen icon that opens a flyout with the shared
   `ThumbnailGallery`, grouped by icon set (localised set names) with "No icon" first; `ThumbnailGalleryOptions.ShowsCaptions`
   (new, default true) lets a gallery of small glyph tiles drop its captions while keeping tooltips and accessible
   names. `ConditionalVisualOptions` takes the injected `IGalleryNavigator`.
2. `InvalidDataFinder` (`IInvalidDataFinder`, Sheets.Engine, registered in DI) returns the non-empty cells whose value
   breaks their rule, row by row, up to 255 (Excel's limit). `ledger.trace.markInvalid` stores them as
   `TraceKind.Invalid` marks (a cell, no arrow), drawn as red ellipses by `GridOverlayPainter`, refreshed by Refresh
   Traces and removed by Remove All Traces; the status text now counts cells breaking their rule.
3. `SheetClipboard` copies each rule clipped and reanchored on the clipped area and reanchors it again on paste
   (`ValidationAnchoring`); before pasting it cuts the target out of existing rules (`ValidationPlacement`).
4. `ISheetFill.Validations` plans the validation list after a fill (`FillValidations`): the target is cut out of
   existing rules and every source rule is repeated with the source period, reanchored on each copy. Used by the fill
   handle (one history step inside the fill transaction, skipped for "fill without formatting" and on protected sheets)
   and by the Fill commands (sheet snapshot).
5. `XlsxMetadataMapper.ReadValidations` builds the rule on the first `sqref` area and reanchors it on each further area.
6. `LedgerCellEditing.AnnounceInputMessage` reports "title: message" when the active cell changes onto a cell whose
   rule shows an input message.

## Tests

`Round309OdsStyleMapSizeTests`, `Round309ValidationRangesTests` (XLSX), `Round309ValidationPasteTests`,
`Round309FillValidationTests`, `Round309InvalidDataTests` (engine), the icon gallery case in
`LedgerFormatSurfacesTests` and `Selecting_a_cell_with_an_input_message_shows_it_in_the_status_bar` in
`LedgerEditorTests`.

## Covered without defects

List sources (typed lists, ranges, named ranges, other-sheet ranges) through `DataValidator.ChoicesOf` and the ODS
`cell-content-is-in-list` form; the x14 validation extension on reading; ODS reading of several areas per validation
name with base-cell reanchoring; dropdown display and sorting (rounds 149 and 253).
