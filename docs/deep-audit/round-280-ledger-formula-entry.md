# Round 280: Ledger formula entry and fill handle leftovers

Area: the round 277 queue item for Ledger editing: pointing to another sheet while typing a formula, the fill handle's
copy/series toggle, fill options and numbered text steps, the function AutoComplete list, argument names in the tips,
and AutoComplete and tips in the formula bar. Not built or run here (the coordinator builds and tests at the join).

## Defects fixed

1. **Serious (UX): switching sheet while typing a formula committed the entry.** `LedgerCellEditing.KeepEntryAcrossSheets`
   keeps the entry when a reference fits at the caret (or a reference is being pointed): the text moves to the formula
   bar with its caret, the grid shows the other sheet, and the pointed cell or range is inserted sheet-qualified
   (`IFormulaPointing.Qualify`: `Data!B3`, `'Data 2'!B3`, quotes doubled, names that read as cells quoted). Arrows and
   Shift+arrows point on the other sheet; Enter or Tab writes the edited cell and returns to its sheet; Escape returns
   without writing. The selection display of the other sheet does not overwrite the formula bar while the entry is
   carried.
2. **Medium (UX): function AutoComplete was one inline suggestion.** `IFormulaCompletion.Matches` lists every function
   and defined name that starts with the name being typed; `LedgerFormulaAssist` shows them in the shared
   `ChoiceListPopup` under the box. Up, Down, Page Up and Page Down move in the list, Tab or a click accepts (a function
   gets its opening parenthesis, `IFormulaCompletion.Accept`), Escape closes the list, Enter commits the entry as typed.
3. **Medium (UX): argument tips showed numbered arguments.** `IFunctionSignatures` (Sheets.Engine, data in the embedded
   `FunctionSignatures.txt`) names the arguments of all 538 required functions; repeated groups continue their numbering
   (`number3`, `criteria_range3`). The tip shows the names, optional ones in brackets, the current one in bold; the
   function wizard uses the same names for its signature and argument boxes.
4. **Medium (UX): the formula bar had no AutoComplete and no argument tip.** The same `LedgerFormulaAssist` is attached to
   the cell editor and the formula bar; the tip is now a popup above the box instead of a text block on the grid overlay.
5. **Medium (UX): the fill handle had no copy/series toggle.** Holding Ctrl (or Alt, the Mac key) while dragging inverts
   the automatic choice, as Excel does: a single number fills a series, anything else is copied
   (`ISheetFill.Toggled`, `FillMode`).
6. **Medium (UX): no fill options after a drag.** `LedgerFillOptions` shows a button at the end of the filled range; its
   list (Copy Cells, Fill Series, Fill Formatting Only, Fill Without Formatting; five languages) redoes the last fill in
   place as one history step (`LedgerCellOperations.RefillAs`, which undoes the fill only when it is still the last step).
7. **Medium (data): the fill handle did not copy formats.** Filled cells now take the seed's style unless the sheet is
   protected (then values only, as before); Fill Formatting Only copies styles alone and Fill Without Formatting keeps
   the target's.
8. **Minor (data): numbered text with a two-cell seed stepped by the seed length.** `Item 1`, `Item 3` now continue
   `Item 5`, `Item 7`; seeds whose numbers do not step evenly keep the old behaviour.

## Open (queued)

- Argument names are the English ones in every interface language; localized names for the four other languages are
  not written.
- The Ctrl toggle decides "single number" only for a one-cell seed; a one-row seed of numbers dragged down (several
  lanes of one cell) is copied when toggled, where Excel fills each lane as a series.
- The fill options button does not follow scrolling or zoom (it hides on the next selection change); it shows a glyph,
  not a ribbon icon.
- Arrow-key pointing in the formula bar works only while the entry is carried to another sheet; typing in the formula
  bar on the edited sheet still moves the caret with arrows (Excel's enter mode in the formula bar).
- The AutoComplete list and the tip popups were not seen live; their placement near the screen edge is unchecked.
