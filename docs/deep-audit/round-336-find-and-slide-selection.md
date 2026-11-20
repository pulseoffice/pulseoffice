# Round 336: find leftovers and Deck slide selection

Area: the Find (331), Find and replace (331), Folio find and replace (331) and Deck (332) queue items.

## Changes

- Shared matching: `ITextPatternMatcher` / `TextPatternMatcher` (Text.Rich) matches a `TextPattern` (text, match
  case, whole words, regular expression) and expands `$1` groups in the replacement. Invalid expressions and
  timeouts (1 s) find nothing. `TextFindQuery` (UI.Controls) carries whole words and regular expressions and
  `TextFindHighlights` uses the matcher, so the slide, table and shape highlights follow the same options.
- Deck find: `DeckTextSearch` works on a `TextPattern` (the old `matchCase` overloads remain); matches carry their
  length. `DeckFindTarget` offers Whole words and Regular expressions (existing texts), selects the matching table
  cell (`DeckViewOptions.SetTableCells`) instead of the whole table, shows the notes pane and selects the match in the
  notes (`DeckTarget.SelectNotes`), and Replace All now also replaces inside notes, which it counted but left alone.
- Ledger find: `ICellSearchText` decides the text searched in a cell. `PresentedCellSearchText` (registered in the
  app) searches numbers, dates and booleans as displayed through `ICellPresenter`, as "Look in: Values" does, and
  parses a numeric replacement in the spreadsheet locale; `StoredCellSearchText` keeps the old behaviour as the
  default. `ISheetSearch`, `IWorkbookSearch` and the highlights now come from DI with that strategy.
  `ILedgerFindScope` restricts find, count and Replace All to a multi-cell selection and keeps the restriction while
  Find Next walks the matches inside it (the selection collapses to the found cell); "All sheets" and "In comments"
  search without it.
- Folio replace: a match that removes a `^b` mark at a `SectionBlock` boundary dissolves that section
  (`ISectionEditing.Unwrap`) before deleting the text, so the text merges into one section as when a section break
  is deleted in Word.
- Deck slide selection: Shift+Up/Down in the filmstrip extend the selection from its anchor (`SlideSelection.ExtendTo`),
  plain arrows collapse it; dragging a selected slide in the filmstrip or the sorter moves every selected slide
  together, keeping their order, in one undo step (`DeckActions.MoveSlides`).

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | Deck Replace All counted matches in the notes but did not replace them. | Notes rewritten with the same pattern. |
| Medium | Ledger value search matched the stored value, not the displayed text; numeric replacements parsed invariantly. | `PresentedCellSearchText`. |
| Medium | Ledger search ignored a multi-cell selection. | `ILedgerFindScope`. |
| Medium | Folio: replacing a `^b` at a section block boundary deleted the text but kept the section. | Section dissolved. |
| Medium | Deck find was literal and case-only; a table match selected the whole table; notes matches not shown. | Pattern options, cell selection, notes selection. |
| Minor | No Shift+Up/Down extension in the filmstrip; dragging moved only the slide under the pointer. | Extension and group move. |

## Open

- Deck paste options button after pasting slides (floating Ctrl menu) is not built: it needs a shared floating
  paste-options component wired into the three modules; queued.
- Ledger: values found through the displayed text are replaced in the stored text (a match on a thousands separator
  replaces nothing); Excel does not offer replace in values at all. The Ledger highlights do not apply the
  selection restriction.
- Deck: a notes match selects the text in the notes box, which has no background highlight of every match.
- Nothing tried in the running app; no build or test run in this round (cycle rule).
