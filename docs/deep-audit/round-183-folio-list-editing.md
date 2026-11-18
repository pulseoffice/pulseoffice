# Round 183: Folio list editing and output

Area: the list editing paths round 179 left open (Tab and Shift+Tab, Enter on an empty item, Backspace at the start of
an item, Enter in the middle, paste, restart and continue, undo), list numbering in PDF export and printing, and the
per-level separator.

## Defects

1. **Medium: Enter on an empty nested item ended the whole list.** An empty item on level 2 or deeper left the list at
   once; other word processors first move it one level up and leave the list only from the first level.
   `FolioTypingAssist.EndEmptyListItem` now outdents with `IListEditing.ShiftLevel` when the level is above the first,
   as one undo step. Test: `Round183FolioListEditingTests`.
2. **Medium: Backspace at the start of a list item merged it into the previous paragraph.** The number and the item
   vanished in one keystroke. The first Backspace now turns the item into an unnumbered list paragraph
   (`IListEditing.SetNumbered(false)`, its own undo step); the next one merges as before
   (`FolioTypingAssist.RemoveNumberAtItemStart`). Test: `Round183FolioListEditingTests`. (Committed together with 1.)
3. **Minor: the per-level separator was lost.** A Word level text `%1-%2.` was read and shown as `1.1.`. `ListLevel`
   now has `Separator` (default period): `ListLabeling` joins the shown levels with it, DOCX writes and reads it in
   `w:lvlText` (the text between the first two placeholders), ODT writes and reads our `pt:level-separator` on the
   level style, since ODF has no per-level separator (other readers show periods). Tests:
   `Round183ListSeparatorLabelTests`, `Round183ListSeparatorTests`, `Round183OdtListSeparatorTests`.

## Reviewed without defects

- Tab and Shift+Tab at the start of an item or over several selected items change the level through
  `ListLevelKeyAction` as one step; inside a table cell, away from an item start, they move between cells.
- Enter in the middle of an item splits it with the list properties kept, one step.
- Restart and continue commands are single history steps through `ListEditing`.
- PDF export and printing draw the same labels as the screen: the only label source is `ListLabeling`, used by
  `FlowBuilder` for every output.

## Open items

- A Word level text with different separators between different placeholder pairs (`%1.%2-%3`) keeps only the first
  one; the list dialog does not yet offer the separator for editing (it is preserved through the dialog's merge).
- Paste of list items from other modules and drag and drop of list items were not exercised beyond reading the code.
- The coordinator builds and tests: nothing was compiled in this round.
