# Round 277: Ledger leftovers and cell editing

Area: the round 270 queue items (CSV dates in another epoch, calculated pivot fields in the settings panel, base items
in the dialog, the headless dialog test) and a fresh audit of Ledger editing: in-cell editing and the formula bar,
reference picking, F4, AutoComplete and argument tips, fill handle and series, paste special, undo granularity. Not
built or run here (the coordinator builds and tests at the join).

## Defects fixed

1. **Medium (data): Insert sheet from file kept the 1900 system and dropped formats.** A CSV (always read in the 1900
   system) or a 1904 XLSX inserted into a workbook with another epoch kept its serials, so every date moved four years;
   styles were not copied either, so dates showed as bare numbers. `ISheetCellTransplant` (Sheets) copies values,
   formulas and styles (number formats carried through `IStyleTransplant`) and rebases numbers whose format names days
   (`ICalendarFormats`) by the difference of the two epochs; times and durations stay as they are. This closes the
   round 270 CSV item: the CSV reader always builds its own 1900 workbook, and the only path that puts its cells into
   an existing workbook is this command.
2. **Medium (UX): calculated pivot field names and formulas were not validated in the settings panel.** A duplicate or
   empty name, or a formula that does not parse or names no field, was silently ignored. The panel now says why in a
   message under the boxes (`pivot.calculated.badName`, `pivot.calculated.badFormula`), checks names against every
   source and calculated field (`PivotLayoutModel.NameTaken`), and validates formulas against the source headers
   (`IPivotPreviewer.IsFormulaValid`). Enter commits through a tunnel handler, so the text box cannot swallow it.
3. **Medium (UX): the pivot dialog offered no next or named base items.** "Difference from" and "% difference from"
   now have a base item choice: (previous), (next) and the items of the base field by their labels; changing the base
   field resets it to (previous).
4. **Serious (UX): a click or an arrow key while typing a formula committed it.** Ledger had no point mode: clicking a
   cell or pressing an arrow after `=`, `(`, an operator or a separator ended the entry. `IFormulaPointing` (Engine)
   decides where a reference fits and places it; the cell editor and the formula bar now insert the pointed cell or
   range (pointer drag and Shift+arrows extend it) and replace it while pointing continues; Enter, Tab and Escape
   return to the edited cell first. An entry begun by typing is in enter mode: an arrow where no reference fits commits
   and moves, as in the other spreadsheets; F2 toggles to edit mode, where arrows move the caret.
5. **Medium (UX): no AutoComplete of function or range names and no argument tips.** `IFormulaCompletion` completes
   the name being typed after `=` or an operator from the localized function names and the defined names (inline, the
   suggested rest selected); Tab accepts and opens the parenthesis of a function, Enter drops an unaccepted suggestion.
   A tip under the editor shows the innermost call with its arguments, optional ones in brackets and the current one
   in bold, following the caret and the language's argument separator.
6. **Minor: F4 did not cycle whole column or row references** (`A:C`, `2:5`); they now toggle between relative and
   absolute.
7. **Medium (data): paste special operations with blanks.** A blank source cell erased the target instead of counting
   as zero (Multiply left a blank instead of 0, Add erased the value), and a blank target took the source instead of
   0 (Subtract gave 5 for 0−5). Both now count as zero.
8. **Medium (data): paste special operations onto formulas replaced them with their value.** The target formula is now
   kept and combined, as Excel does: `=(B1*2)+5`, or `=(C3)*(E3)` when formulas are pasted.
9. **Medium (UX): the fill handle copied a single date and did not know day or month names.** A single cell with a
   date format now advances by one day per cell; day and month names, full and abbreviated, in the spreadsheet
   locale's language or in English, continue in the typed case (Sat, Sun, Mon; NOVEMBER, DECEMBER, JANUARY).

## Checked, no defect

- Undo granularity: an entry, a fill, a paste (all parts) and Insert sheet from file are one history step each;
  pointing does not touch the history.
- F4 on single cells and ranges, inside strings and on sheet-qualified references.

## Open (queued)

- Pointing to another sheet while typing a formula: switching sheets still commits the entry.
- The fill handle has no Ctrl toggle between copy and series and no fill options button after the drag; numbered
  text with a two-cell seed steps by the seed length, not by the difference of the numbers.
- Function AutoComplete is inline (one suggestion) rather than a list; the argument tip shows numbered arguments,
  not the argument names, because the function registry has no argument names.
- The formula bar has the pointing, but not the AutoComplete and the argument tip of the cell editor.
