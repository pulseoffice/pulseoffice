# Deep audit round 131: Ledger named cell styles with explicit properties

Scope: the cell-style leftovers of round 125 (docs/deep-audit/round-125-style-leftovers.md, "Residual"): no "not set"
value in named styles, font and borders inherited as a whole, `Restore` moving styles to the end, `Remove` leaving
orphaned children.

## The new model

- `CellStyleProperties` (`src/PulseOffice.Sheets/Styling/CellStyleLayering.cs`) is a flag set of 27 single properties:
  font family, size, bold, italic, underline, double underline, overline, strikethrough, shadow, outline, script, font
  colour, background, each border side and both diagonals, horizontal and vertical alignment, wrap, rotation, indent,
  number format, protection (locked) and language. Groups `Font`, `Borders`, `Alignment`, `All`.
- `ICellStyleLayering` / `CellStyleLayering` (Strategy table, one entry per property): `Differences(style, basis)`,
  `Overlay(basis, layer, properties)`, and flag access for the boolean properties (used by the dialog).
- `NamedCellStyle.Explicit` is the set of properties the style sets; everything else is inherited. The stored
  `StyleId` holds only the explicit values (the rest at default).
- `NamedStyleCatalog` (now with an injectable layering) resolves `StyleOf` from `CellStyle.Default` through the parent
  chain root to leaf, overlaying each style's explicit properties: a child can switch off wrap, protection, bold, ... set
  by its parent, and font and borders are inherited attribute by attribute. `InheritedStyleOf` gives what the parent
  chain supplies, `OwnStyleOf` the stored layer, `DeclaredAlong` the union of explicit sets along the chain.
- `Define(..., explicitProperties)`: when not given, the explicit set is the difference from the default style (the
  previous behaviour for code and files that do not say more). `Restyle` keeps a style's explicit set and adds what
  changed (themes use it).
- Cell direct formatting is unchanged: cells still carry a flattened `CellStyle` plus the style name; grid, printing,
  spelling and macros (`SheetFacade.StyleName`) read the flattened style, so they follow the new resolution with no change.

## Formats

- ODS: a named style writes exactly its explicit properties (`OdfNamedStyleLayer.Limit`, `CellStyles/NamedLayer.cs`):
  an explicit property at its default value is written with its neutral value (`fo:wrap-option="no-wrap"`,
  `style:cell-protect="protected"`, `fo:font-weight="normal"`, `fo:language="zxx"`, the General data style...), a
  property that is not explicit is removed from the declaration so it is inherited through `style:parent-style-name`.
  On reading, `OdfNamedStyleLayer.Declared` turns the attributes present (including `fo:border` for every side) into
  the explicit set. Automatic cell styles keep resolving through the XML parent chain as before.
- XLSX: cell styles have no parent (loss already declared as `XLSX240`). `cellStyleXfs` apply flags now carry the
  explicit groups along the chain (`XlsxStyleIncludes`): `applyFont/Fill/Border/Alignment/NumberFormat/Protection` are
  written true when the group is explicit, and the protection element is written whenever protection is explicit
  (locked or not). On reading, a group whose apply flag is not `0` is explicit, so a built-in child re-attached to its
  built-in parent keeps its own reset values. Per-attribute granularity inside a group cannot be represented in XLSX;
  since XLSX already flattens every custom style (XLSX240), no new code was needed.

## Defects

Medium

1. A child named style could not switch off a boolean or reset an enum set by its parent (wrap, protection, bold,
   vertical alignment...): `NamedStyleCatalog.Merge` treated default values as "not set"
   (`src/PulseOffice.Sheets/NamedStyles.cs:85` before the change). Fixed by the explicit layer. Tests:
   `NamedStyleRound131Tests.A_child_can_switch_off_what_its_parent_switches_on`,
   `OdsCellStyleRound131Tests` (two, run red with the layer disabled), `XlsxCellStyleRound131Tests` (two).
2. Font and borders were inherited as a whole: a child changing only the font colour lost the parent's bold and size,
   a child with one border lost the parent's other sides (`NamedStyles.cs:87,89`). Fixed per attribute. Test:
   `Font_and_borders_are_inherited_attribute_by_attribute` (run red).
3. `NamedStyleCatalog.Remove` left children pointing to the removed parent, unlike the panel's delete command, and both
   paths lost the removed style's contribution to its children's look. `NamedStyleCatalog.Without` is now the single
   rule: children are handed to the removed style's parent and absorb its explicit layer, so their look does not
   change; `CellStyleReferences.Delete` and `Remove` both use it. Test:
   `Removing_a_style_hands_its_children_to_its_parent_with_their_look_unchanged` (run red).
4. Redefining a style (update from selection, or "new style" with an existing name) moved it to the end of the panel
   on redo and on undo (`NamedStyleCatalog.Restore` appended). `Restore` now replaces in place, or inserts at the
   recorded position when the style is absent; the command records `IndexOf` before the change. Test:
   `Restoring_a_redefined_style_keeps_its_place`.
5. Updating a style from the selection dropped its parent and its built-in flag (`LedgerStyleCommands.Define` passed
   `parent: null`, `builtIn: false`): updating "Heading 1" made it a deletable root style. The update now keeps parent
   and built-in flag and adds to the explicit set only what differs from the inherited look
   (`LedgerStyleCommands.Definition`).

## Styles dialog

"New style from parent" (Derive in the styles panel) now shows an "Inheritance" section with one row per property:
boolean properties offer "Inherit from parent (current value)", "Yes", "No"; the others "Inherit from parent" or "Own
value (as now)". A derived style starts with everything inherited. Texts `style.inherit.*` and `style.property.*` in
the five languages.

## Residual

- XLSX keeps explicit sets per group (font, fill, border, alignment, number format, protection), not per attribute;
  it already drops the hierarchy of custom styles (XLSX240).
- Printing still does not wrap, rotate or draw underline/strikethrough (round 125 residual, print canvas).

## Tests run

- `tests/PulseOffice.Sheets.Tests`: 390 passed; `tests/PulseOffice.Sheets.UI.Tests`: 494 passed;
  `tests/PulseOffice.Sheets.Printing.Tests`: 39 passed.
- `tests/PulseOffice.Formats.Odf.Tests`: 860 passed; `tests/PulseOffice.Formats.OpenXml.Tests`: 2517 passed.
- `tests/PulseOffice.Architecture.Tests`: 43 passed; `tests/PulseOffice.Localization.Tests`: 32 passed.
- Defect 5 and the dialog have no automated test (UI command path); to be checked live.

## To test live

- Ledger, styles panel: derive a style from a wrapped, unlocked, bold style; set Wrap "No", Bold "No"; apply it: the
  cell does not wrap, is not bold, keeps the parent's font size. Save as ODS and XLSX, reopen: same look.
- Change the parent's font size: the child follows; change the child's colour only: it keeps the parent's bold.
- Update a built-in style from the selection, then undo and redo: it stays in its place in the panel and stays built-in.
- Delete a style that has children: children move under the deleted style's parent with the same look; undo restores
  the order.
