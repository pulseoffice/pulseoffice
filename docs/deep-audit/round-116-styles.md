# Round 116: styles across the three modules

## Scope

1. Rename for Ledger cell styles and Deck graphic styles, through the shared styles panel, with undo, protection,
   both formats and five-language texts.
2. Audit of the style management code of Ledger and Deck: model, commands, panel sources, ODS/XLSX/ODP/PPTX style
   reading and writing.

## What was built

- `IStyleRenamePrompt` / `StyleRenamePrompt` (`PulseOffice.UI.Controls.StylePanels`): the one rename prompt (modal with
  veil, through `IDialogService` and `FormPromptView`). Folio's `FolioStyleDialogs.RenameAsync` now delegates to it, Ledger
  and Deck use it too; each module passes its own explanation key.
- `ICellStyleReferences` / `CellStyleReferences` (`PulseOffice.Sheets.Styling`): rename and delete of a named cell style
  as one `CellStyleRelink` (catalog before/after plus every cell link on every sheet), applied and reverted as a single
  undo step. Names compare case-insensitively, as the catalog does; a case-only rename is allowed. Refused: built-in
  styles, missing styles, empty names, control characters, names over 255 characters (Excel's limit), names already
  taken. Delete hands the cells and the child styles to the deleted style's parent (Folio behaves the same way).
- `LedgerStyleCommands.RenameAsync` / `Delete`: refuse in read-only documents and when a sheet holding cells of the style
  forbids cell formatting (`ISheetEditGuard.CanFormat`), with the existing status messages and remedies.
  `LedgerStylesSource` enables `Rename` for custom styles.
- `GraphicStyleCatalog.Rename` keeps the style's position (ODP encodes graphic styles by position, `QS<n>`);
  `DeckStyleCommands.RenameAsync` relinks every linked shape and freeform through `ChangeGraphicStyles` (one undo step).
  `DeckStylesSource` enables `Rename`.
- New texts in it/en/de/es/fr: `styles.rename.title`, `styles.rename.name`, `styles.rename.taken`,
  `styles.rename.invalid`, `styles.renamed`, `ledger.styles.rename.explanation`, `deck.styles.rename.explanation`,
  `diagnostic.xlsx240`.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/NamedStyles.cs | 191/191 | ok (cycle-safe `StyleOf`) |
| src/PulseOffice.Sheets.UI/LedgerStyleCommands.cs | all | defects 6, 7 |
| src/PulseOffice.Sheets.UI/LedgerStylesSource.cs | all | rename wired |
| src/PulseOffice.Presentations.UI/DeckStyleCommands.cs | all | defect 8 |
| src/PulseOffice.Presentations.UI/DeckDesignActions.cs | 1-116 | residual R1 |
| src/PulseOffice.Drawing/Shapes/GraphicStyles.cs | all | ok |
| src/PulseOffice.Presentations/EmbeddedStyled.cs | all | ok |
| src/PulseOffice.Shell/StylePanels/ModuleStylesSource.cs | all | ok (read-only gate) |
| src/PulseOffice.UI.Controls/StylePanels/StylesPanelSource.cs | all | ok |
| src/PulseOffice.Formats.Odf/CellStyles/Naming.cs | all | defects 1, 2, 3, 4 |
| src/PulseOffice.Formats.Odf/OdfStyleMapper.cs | all | defects 1, 5 |
| src/PulseOffice.Formats.Odf/OdsReader.cs | 170-200 | defects 2, 3 |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs | 15-35, 330-390 | defect 5 |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs | 15-40, 262-300 | ok |
| src/PulseOffice.Formats.Odf/OdfConditionalMapper.cs | 1-60 | context for defect 4 |
| src/PulseOffice.Formats.Odf/Presentation/OdfGraphicStyles.cs | all | defect 9 |
| src/PulseOffice.Formats.Odf/Presentation/Writing/ElementWriters.cs | 155-225 | defect 9 |
| src/PulseOffice.Formats.Odf/Shapes/DeckObjectCodecs.cs | 110-200 | ok |
| src/PulseOffice.Formats.OpenXml/XlsxStyleMapper.cs | 1-130 | defect 10 |
| src/PulseOffice.Formats.OpenXml/CellStyles/Codecs.cs | 215-306 | defect 11 |
| src/PulseOffice.Formats.OpenXml/XlsxReader.cs | 120-250 | defect 11 |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs | 295-315 | ok (PPTX020 declares graphic styles) |
| src/PulseOffice.Documents/Editing/StyleReferences.cs, StyleEditing.cs | all / 130-211 | reference for the shared mechanism |

Not read in full this round: the rest of `SheetPartReaders.cs`, `SheetWriting.cs`, `XlsxReader.cs`, the Deck master and
layout code, and the PPTX shape-style mappers beyond the fidelity rules.

## Defects

Each was shown by a test that failed before the fix (the ODS and XLSX ones were run red; the ODP freeform one follows
from the writer not emitting the attribute at all).

Grave
1. ODS wrote the raw display name as `style:name` of a named cell style (`OdfStyleMapper.DeclareNamed`), so any style
   with a space or a non-ASCII character produced an invalid NCName; the reader then also defined a second, decoded copy.
   Now `style:name` is the encoded form and `style:display-name` the real name; the reader uses the display name.
2. The ODS reader dropped the parent of every named cell style (`OdsReader.ReadStyles` called `Define` without parent):
   the hierarchy was lost on every round trip. `OdfCellStyleReader.ReadNamed` reads `style:parent-style-name` from
   `office:styles`.
3. The ODS reader skipped every named style whose name began with `ce` or `ConditionalStyle` (`OdsReader.cs:194` before
   the fix): a user style called «cerise» or «Centrato» vanished. The filter is gone.
4. Our own conditional-format styles (`Condition<n>`) were read back as user cell styles, polluting the styles panel
   after each ODS round trip. `OdfConditionOnlyStyles` skips styles without display name named `Condition<n>`,
   `ConditionalStyle<n>` or `ConditionalStyle_<n>`.
5. The link from a cell to its named style was never written in ODS: the automatic `ce<n>` style always had
   `Default` as parent, so `Cell.StyleName` was lost on save. The automatic style now carries the encoded named style as
   parent and `OdsCellReader.NamedStyleOf` follows it; a cell referencing a common style directly gets the inherited
   look (`StyleOf`), not only its own properties.
10. XLSX named style formats (`cellStyleXfs`) dropped alignment, number format and protection.
11. XLSX never read custom cell styles back into the catalog: they vanished from the styles panel and the gallery after
   a round trip. `XlsxStyleReader.ReadNamedStyles` reads `cellStyles` + `cellStyleXfs`; built-in styles keep their
   parent and built-in flag. XLSX has no style inheritance: declared as `XLSX240` (custom styles with a parent).

Medium
6. Deleting a Ledger cell style left cells and child styles pointing at a name that no longer existed (re-creating a
   style with that name silently relinked them). Delete now goes through `CellStyleReferences`.
7. Ledger style delete ignored read-only and sheet protection when started from the panel; rename and delete now refuse.
9. ODP: freeforms lost their link to a graphic style (the writer omitted `style:parent-style-name`, the reader relinked
   only `ShapeObject`).

Minor
8. Deck «New style» with the name of an existing style silently overwrote it; it is now refused with a message.

## Residual

- R1: `DeckDesignActions.ChangeGraphicStyles` relinks only top-level objects of slides, not objects inside groups or on
  masters and layouts; the same holds for the ODP reader's relink. Not changed this round.
- R2: ODS automatic cell styles from other suites hold only the properties that differ from their parent; the reader
  does not merge the parent's properties into them (pre-existing, outside the named-style path touched here).
- R3: the ODS `Default` parent cannot be told apart from no parent.

## Tests

New: `tests/PulseOffice.Formats.Odf.Tests/OdsCellStyleRound116Tests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/XlsxCellStyleRound116Tests.cs`,
`tests/PulseOffice.Sheets.Tests/CellStyleReferencesTests.cs`, `OdpObjectOpsRoundTripTests.A_freeform_keeps_its_link_to_a_graphic_style`.
Run green, one project at a time: Formats.Odf (840 + ODP subset 216), Formats.OpenXml (2474), Sheets (378), Sheets.UI (481),
Documents.UI (370), Presentations.UI (107), Drawing (321), Localization (32), Architecture (43), Desktop subset
(composition, source rules, styles: 28).

## To test live

- Ledger: styles panel, right-click a custom style, Rename; cells on two sheets and a derived style follow; ⌘Z restores;
  rename blocked on a protected sheet that holds cells of that style; built-in styles show Rename disabled.
- Deck: rename a graphic style used by a shape and a freeform; ⌘Z; save as ODP and reopen.
- Folio: rename still works through the shared prompt (title now «Rename Style» from the shared key).
- Open a saved ODS in the reference suite: style names with spaces and accents appear correctly, hierarchy kept.
- XLSX in Excel: custom styles listed under cell styles with alignment and number format.
