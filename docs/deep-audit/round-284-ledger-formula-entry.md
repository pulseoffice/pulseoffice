# Round 284: Ledger formula entry leftovers

Area: the "Round 280 (Ledger formula entry), open" queue item: localized argument names, the Ctrl fill toggle on
multi-lane seeds, the fill options button, arrow-key pointing in the formula bar, and the AutoComplete and tip popups.
Not built or run here (the coordinator builds and tests at the join).

## Defects fixed

1. **Medium (UX, localization): argument names were English in every language.** `IFunctionArgumentNames`
   (Sheets.Engine, data in the embedded `FunctionArgumentNames.txt`: one row per argument stem with its Italian,
   German, Spanish and French name) translates every one of the 301 argument stems used by `FunctionSignatures.txt`;
   the trailing number of repeated arguments is kept (`number3` → `num3`, `Zahl3`). The argument tip of the cell editor
   and the formula bar and the function wizard's argument boxes use it with the interface language. Names follow the
   conventions of the localized spreadsheet programs where they are well known (`Suchkriterium`, `valor_buscado`,
   `valeur_si_vrai`), descriptive translations elsewhere. A test checks that every argument of every required
   function has a row.
2. **Medium (UX): the Ctrl fill toggle copied a one-row seed dragged down.** `ISheetFill.Toggled` now takes the fill
   direction and decides on the lane length: when every lane has one cell and the seed holds only plain numbers
   (empty cells allowed, no formulas, no dates), each lane fills as a series, as Excel does; otherwise the toggle copies.
3. **Minor (UX): the fill options button did not follow scrolling or zoom and showed a glyph.** `GridViewport` raises
   `Moved` when either scroll offset changes, exposed as `SheetGridView.ViewportMoved`; `LedgerFillOptions` keeps the
   filled corner and re-places the button on scroll and zoom, hiding it (and its list) while the corner is out of view.
   The button shows the shared `ledger.fill.series` icon.
4. **Minor (UX): arrow-key pointing in the formula bar worked only across sheets.** F2 in the formula bar now toggles
   point mode, as it does in the cell editor: arrows (and Shift+arrows) point at cells of the edited sheet when the
   caret accepts a reference, otherwise they keep moving the caret. Edit mode stays the default in the formula bar, as
   in Excel; the mode ends on Enter, Escape or a new selection.

## Open (queued)

- The AutoComplete list and the tip popups were still not seen live; their placement near the screen edge relies on
  the popup positioner's default flip and slide adjustments and is unchecked.
- Localized argument names are written for the tips and the wizard only; typed formulas never contain argument names,
  so nothing else needs them. The German and French names were not compared one by one with Excel's.
