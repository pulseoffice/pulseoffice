# Round 118: Deck style relinking, ODS style inheritance, shape style mappers

## Scope

1. Close the three residuals of round 116 at the root: Deck graphic-style relinking inside groups, masters and layouts
   (commands and ODP reader); ODS automatic cell styles from other suites inheriting their parent's properties; ODS
   `Default` as parent told apart from no parent.
2. Read in full what round 116 read only partially: Deck master/layout style code, the PPTX shape style mappers, and the
   ODS/XLSX style readers and writers.

## What changed

- `IGraphicStyleRelinker` / `GraphicStyleRelinker` (`PulseOffice.Presentations`): one walk over slide objects, objects in
  `ShapeGroupObject` (nested, depth-capped at 64), master objects and layout objects. `DeckDesignActions.ChangeGraphicStyles`
  uses it (injected through `DeckActions`) and its undo also restores every master; `OdfGraphicStyles.Read` uses it to turn
  `QS<n>` into display names everywhere.
- ODP master and layout decoration shapes now write `style:parent-style-name` to their graphic style (`OdfMasterPages.Decorate`).
- `OdfCellStyleInheritance` (`Formats.Odf/CellStyles/Inheritance.cs`): automatic and common cell styles are read with the
  property sets and data style of their common-style ancestors merged under their own (cycle-safe, depth 32; `Default`
  is the root and is not merged, as the model's default style already stands for it; `fo:border` and `fo:border-*`
  override each other).
- `OdfCellStyleOverrides`: an automatic cell style whose cell carries a named style now writes neutral values for every
  property the named style sets and the cell does not (for example `fo:font-weight="normal"` under a bold style), so the
  reference suite and our reader no longer inherit the named style's look into a cell that overrode it.
- `OdfPropertySetPlacement`: property sets added to a style keep the ODF order (cell, paragraph, text).
- ODS named styles: no parent is written as no `style:parent-style-name`; parent `Default` is written and read as
  `Default`. `XLSX240` no longer counts styles whose parent is `Default`.
- PPTX: the fidelity rules on shape styles (`PPTX029`, `PPTX030`, `PPTX034`, `PPTX035`) now see freeforms, connectors,
  fontwork, shapes inside groups and master/layout objects, not only top-level preset shapes on slides. A hatch without
  background is written with a transparent `a:bgClr` and read back without background (it used to become white).

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Presentations.UI/DeckDesignActions.cs | all | R1 fixed |
| src/PulseOffice.Presentations.UI/DeckStyleCommands.cs | 60-200 | minor 6 (residual) |
| src/PulseOffice.Presentations/EmbeddedStyled.cs | all | ok |
| src/PulseOffice.Presentations/SlideMaster.cs | all | ok |
| src/PulseOffice.Presentations/Presentation.cs | 1-100 | ok |
| src/PulseOffice.Drawing/Shapes/ShapeGroup.cs | all | ok |
| src/PulseOffice.Formats.Odf/Presentation/OdfGraphicStyles.cs | all | R1 fixed |
| src/PulseOffice.Formats.Odf/Presentation/OdfMasterPages.cs | 1-230, 330-460 | defect 2 |
| src/PulseOffice.Formats.Odf/Presentation/Writing/ElementWriters.cs | 150-230 | ok |
| src/PulseOffice.Formats.Odf/Presentation/Writing/DocumentWriters.cs | 255-300 | ok (ODP020 declares other master decoration) |
| src/PulseOffice.Formats.Odf/OdpReader.cs | 150-215 | ok |
| src/PulseOffice.Formats.Odf/Shapes/DeckSceneCodec.cs | 1-90 | ok |
| src/PulseOffice.Formats.Odf/OdsReader.cs | 241/241 | R2 fixed |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs | 689/689 | R2 context; ok otherwise |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs | 412/412 | defect 3, minor 7 |
| src/PulseOffice.Formats.Odf/CellStyles/Naming.cs | 222/222 | R2, R3 fixed |
| src/PulseOffice.Formats.Odf/CellStyles/Properties.cs | 343/343 | ok |
| src/PulseOffice.Formats.Odf/OdfStyleMapper.cs | 95/95 | R3, defect 3 |
| src/PulseOffice.Formats.Odf/Spreadsheet/OdsDefaultCellStyle.cs | all | ok |
| src/PulseOffice.Sheets/NamedStyles.cs | 191/191 | ok |
| src/PulseOffice.Formats.OpenXml/XlsxReader.cs | 247/247 | ok |
| src/PulseOffice.Formats.OpenXml/XlsxStyleMapper.cs | 184/184 | minor 8 (residual) |
| src/PulseOffice.Formats.OpenXml/CellStyles/Codecs.cs | 306/306 | minor 9 (residual) |
| src/PulseOffice.Formats.OpenXml/XlsxFormat.cs | 120-135 | XLSX240 adjusted |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/Reading.cs | 315/315 | defect 5 |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/Parts.cs | 157/157 | defect 5 |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/DrawingStyleFidelity.cs | all | ok |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/Vocabulary.cs | all | ok |
| src/PulseOffice.Formats.OpenXml/PptxDeckShapeMapper.cs | all | ok |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs | 280-345 | defect 4 |

Not read this round: `XlsxShapeStyleMapper.cs` and the deck object codecs of the OpenXml project beyond the mapper entry
points.

## Defects

Grave
1. (R1) Renaming, redefining or deleting a Deck graphic style did not reach shapes inside groups, on masters or on layouts,
   and the ODP reader left those objects holding the encoded `QS<n>` name. Test:
   `DeckRound118StyleRelinkTests` (two cases, run red).
2. ODP master and layout shapes lost their graphic style link on save (`OdfMasterPages.Decorate` wrote no parent).
   Test: `OdpObjectOpsRoundTripTests.Master_and_layout_shapes_keep_their_link_to_a_graphic_style` (run red).
3. (R2) ODS automatic cell styles from other suites lost every property inherited from their named parent (a bold, filled
   «Heading» cell with only a red colour of its own came in plain red). Test:
   `OdsCellStyleRound118Tests.An_automatic_style_from_another_suite_inherits_its_parents_properties` (run red). Its
   companion writer defect: our own automatic styles under a named style omitted the properties the cell had reset, so in
   ODF terms (and in the reference suite) the cell inherited them back. Test:
   `A_cell_that_overrides_its_named_style_back_to_plain_keeps_the_override` (green before only because the reader ignored
   inheritance; it would have gone red with fix 3 alone).

Medium
4. PPTX fidelity rules on shape styles ignored freeforms, connectors, fontwork, group members and master/layout objects:
   their gradient, hatch, tile and dash losses were silent. Test: `PptxShapeStyleRound118Tests` (written before the fix
   but run only after it).
5. A hatch without background became a hatch on white after a PPTX round trip. Test:
   `PptxShapeStyleRound118Tests.A_hatch_without_background_stays_without_background` (same note).
6. (R3) ODS wrote `Default` as parent for styles without parent and read `Default` back as no parent. Test:
   `Default_as_parent_and_no_parent_stay_distinct` (run red).

Minor (residual, not changed)
7. Deck «Apply style» collects the selected contents in a value-equality `HashSet`, so an identical copy of a selected
   shape elsewhere on the slide is restyled too (`DeckStyleCommands.Apply`).
8. ODS: properties of the built-in `Default` cell style are never written (an empty `Default` is emitted) nor read; a
   language set on a named style cannot be reset in a cell below it; a cell with General number format under a named
   style with a number format inherits that format in ODF (no "General" data style is written).
9. XLSX: `ReadStyleNames` does not trim names as `ReadNamedStyles` does, so a style name with surrounding blanks loses
   its cell links; `DeclareNamedStyles` assumes `Default` is the first catalog entry (cellStyleXfs 0).

## Tests

New: `tests/PulseOffice.Presentations.UI.Tests/DeckRound118StyleRelinkTests.cs`,
`tests/PulseOffice.Formats.Odf.Tests/OdsCellStyleRound118Tests.cs`,
`OdpObjectOpsRoundTripTests.Master_and_layout_shapes_keep_their_link_to_a_graphic_style`,
`tests/PulseOffice.Formats.OpenXml.Tests/PptxShapeStyleRound118Tests.cs`.

## To test live

- Deck: group two shapes with a graphic style, put a styled shape on the master and on a layout; rename the style, ⌘Z,
  redo; save ODP, reopen: every object keeps the style.
- Open an ODS made by the reference suite with styled cells (Heading, Accent, custom styles with children): look, styles
  panel and cell links as in the reference suite.
- Save from Ledger a cell under a bold named style with bold removed; open it in the reference suite: not bold.
- PPTX: hatch without background on a shape; open in PowerPoint.
