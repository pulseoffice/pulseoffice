# Round 107: Deck masters and layouts, part two

What round 104 left: per-master layout geometry in ODP, custom PPTX layouts of the same type, placeholder
remapping when the layout changes, hostile layout files.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Presentations/MasterFactory.cs | 352/352 | standard layouts already carry ids (`layout.*`), no defect |
| src/PulseOffice.Presentations/SlideFactory.cs | 192/192 | R107-1, R107-2, R107-3 |
| src/PulseOffice.Presentations/SlideMaster.cs | 70/70 | R107-4 (templates looked up by type only) |
| src/PulseOffice.Presentations/Slide.cs | 291/291 | R107-4 (no layout identity on the slide) |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/DeckStructure.cs | 220/220 | R107-4 (layouts keyed by type, last wins) |
| src/PulseOffice.Presentations/HeaderFooter.cs | 57/57 | no defect |
| src/PulseOffice.Formats.OpenXml/Presentation/PptxHeaderFooter.cs | 84/84 | no defect |
| src/PulseOffice.Formats.Odf/Presentation/OdfHeaderFooter.cs | 159/159 | no defect |
| src/PulseOffice.Presentations/Decorations/ThemeDecorations.cs | 303/303 | no defect |
| src/PulseOffice.Formats.Odf/Presentation/OdfMasterPages.cs | 442/442 | R107-5 |
| src/PulseOffice.Formats.Odf/Presentation/Reading/SlideReading.cs | 282/282 | R107-5 (only the first master's layouts are read) |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/StructureWriters.cs | 207/207 | R107-4 (layout name written as the type) |
| src/PulseOffice.Formats.Odf/OdpReader.cs | 236/236 | no defect of its own |
| src/PulseOffice.Formats.Odf/OdpWriter.cs | 140/140 | no defect of its own |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs (SlideListWriter) | 120-170/341 | R107-4 (layout parts keyed by type) |
| src/PulseOffice.Formats.Odf/Presentation/Writing/DocumentWriters.cs (OdpStylesBuilder, fidelity) | 180-300/324 | R107-5, R107-6 |
| src/PulseOffice.Formats.Odf/Presentation/Writing/StyleBuilders.cs (SeedStyleBuilder) | 94-139/139 | R107-5 |
| src/PulseOffice.Presentations.UI/DeckSlideActions.cs (SetLayout) | 200-220/~400 | R107-3 |
| src/PulseOffice.Presentations/SlideLayoutRegistry.cs | 136/136 | no defect |

Merged main after round 104 (layout backgrounds) before the fixes below; the one conflict in `DeckStructure.cs`
kept round 104's unconditional layout background.

## Defects

### R107-4 (serious) - PPTX: several layouts of the same type collapsed into one, last wins
`PptxMasterReader` keyed the model layouts by `SlideLayout` type, so a master with two `obj` layouts (or custom `cust`
layouts mapping to one type) kept only the last; slides on the first layout were drawn and re-saved with the other's
geometry, and the writer keyed layout parts by type too. Fixed at the root: the model layout has an identity.
`Slide.LayoutId` names the template; `SlideMaster.Template(Slide)`, `DecorationFor(Slide)`, `BackgroundOf(Theme, Slide)`
resolve by identity and fall back to the type. `PptxLayoutIdentity` (new, injected into the master and slide readers)
gives the first layout of each type the standard template and every further one `layout.custom.<ordinal>`; the writer
writes one layout part per template identity and links each slide to its own; the layout name of a further layout is
kept. Tests: `PptxCustomLayoutTests` (two).

### R107-5 (serious) - ODP: only the first master's layout geometry and placeholder styles were saved (round 104 R1)
Fixed: `OdpStylesBuilder` writes the presentation page layouts and seed styles of every master, those of a secondary
master prefixed with its master page name (`master2-AL1T0`, `master2-sd_TitleSlide_Title`); `OdpMasterReader.Read`
takes the master name, reads only its prefixed layouts and seeds and the frames of its own master page;
`OdfMasterPages.Read` builds secondary masters through it instead of the standard factory. Other suites ignore the
prefixed layouts (no slide refers to them by those names). Test: `OdpSecondMasterLayoutTests` (first fact).

### R107-6 (declared) - ODP: further layouts of the same type
ODF has one presentation page layout per type in our mapping; further layouts of a type are written as the first one.
Declared in `CheckFidelity` as **ODP107**. Test: `OdpSecondMasterLayoutTests` (second fact).

### R107-1 (medium) - Layout change left orphan content as phantom placeholders
`SlideFactory.Relayout` kept non-empty content with no placeholder in the new layout under its old placeholder role
(a second "Body" on a Title Only slide), so it was later taken as a placeholder by the next layout change and written
as a placeholder that the layout does not have. Now it becomes a plain text box (`PlaceholderRole.Free`) in place.

### R107-2 (medium) - Quadratic placeholder matching on layout change
`Relayout` searched the source for every new placeholder (`FirstOrDefault` with a reused set), looked ids up with
linear `Slide.Find` in a loop, and tested diagram membership with `Any(ReferenceEquals)` per member. A hostile layout
with thousands of placeholders went quadratic to cubic. Now: per-role queues, hash sets of ids and instances.
Test: `RelayoutRound107Tests` (3,000 placeholders; orphan content).

### R107-3 (minor) - Layout change could not choose between layouts of the same type
`DeckSlideActions.SetLayout` compared types only. New overload `SetLayout(Slide, SlideLayoutTemplate)` (also on
`DeckActions`), a no-op when the slide already uses that template, one undo step otherwise.
`ISlideFactory` gained `Create(id, template)` and `Relayout(slide, master, template)`.
Tests: `DeckRound107LayoutChangeTests` (headless, content by type and order, orphan as box, undo/redo, custom layout).

## Hostile files

- Slide without a layout relationship: kind inferred from the placeholders, `LayoutId` null (unchanged path).
- Layouts referring back to their master: the SDK resolves part relationships once; `PptxLayoutIdentity` walks the
  master's layout list only, no recursion.
- Thousands of placeholders: matching is now linear (R107-2).

## Remaining

- The layout gallery still lists one entry per type; choosing a further layout of a type is available to commands and
  macros through the new overload but not yet from the ribbon gallery.
- Fail-before runs: the new tests use the new identity API (`Template(Slide)`, `Create(id, template)`), so they do not
  compile against the old code; the defects were confirmed by reading (last-wins dictionary, unprefixed single-master
  serialisation, role kept on orphans).
