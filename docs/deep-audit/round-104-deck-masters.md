# Deep audit round 104: Deck masters, layouts and placeholders

Scope: master/layout inheritance of text styles, placeholder matching on layout change, multiple masters,
master add/delete/duplicate, background inheritance, theme colours and fonts, header/footer placeholders,
PPTX and ODP round trip of all of it.

## Coverage (files read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PlaceholderInheritance.cs | 138/138 | ok |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/PresentationParts.cs | 139/139 | ok |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/DeckStructure.cs | 220/220 | defects D1, D2 |
| src/PulseOffice.Formats.OpenXml/PptxReader.cs | 169/169 | ok |
| src/PulseOffice.Presentations/SlideMaster.cs | 70/70 | ok |
| src/PulseOffice.Presentations/SlideLayoutRegistry.cs | 136/136 | ok |
| src/PulseOffice.Formats.OpenXml/PptxWriter.cs | 132/132 | ok |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/StructureWriters.cs | 207/207 | see D1 |
| src/PulseOffice.Presentations/MasterEditing.cs | 34/34 | ok |
| src/PulseOffice.Presentations/Presentation.cs (masters, 1-100) | 100/~210 | ok |
| src/PulseOffice.Presentations.UI/DeckMasterCommands.cs | 106/106 | ok |
| src/PulseOffice.Presentations.UI/DeckDesignActions.cs | 113/113 | ok (delete/assign master undo symmetric) |
| src/PulseOffice.Formats.OpenXml/Presentation/Reading/SlideParts.cs | 331/331 | ok |
| src/PulseOffice.Formats.Odf/Presentation/OdfMasterPages.cs | 442/442 | defect D2 |
| src/PulseOffice.Formats.Odf/Presentation/Writing/StyleBuilders.cs (SeedStyleBuilder) | 139/139 | see R1 |
| src/PulseOffice.Formats.Odf/Presentation/Reading/SlideReading.cs (OdpMasterReader) | 95-140/282 | see R1 |

## Findings

### D1 (medium) - PPTX: layout background dropped when the layout has no placeholders
`DeckStructure.cs` (PptxMasterReader) kept the standard template's background whenever a layout part carried no
placeholders, so a background set on the Blank layout (`MasterEditing.WithLayoutBackground`, the layout background
command) was written and silently lost on reading. Fixed: the layout background is always read from the layout part.
Test: `PptxMasterRoundTripRound104Tests.Layout_background_and_placeholder_geometry_survive` (failed before).

### D2 (serious) - ODP: layout backgrounds lost entirely
`OdfMasterPages` wrote a layout master page only for layouts with their own decoration or hidden master objects, and the
reader never read a layout page's fill back. A layout background therefore vanished in ODP for every layout, while
PPTX kept it (rule 7 asymmetry, silent). Fixed: a layout with a background gets its own master page (fill plus
`qm:background` with the colour role); the reader takes the marker, or for foreign layout pages the fill when it
differs from the master's. Test: `OdpMasterRoundTripRound104Tests.Layout_background_and_placeholder_geometry_survive`
(failed before).

### R1 (remaining, declared here) - ODP keeps only the first master's layout geometry and seeds
`SeedStyleBuilder`/`OdpMasterReader` serialise the presentation-page-layouts of the primary master only; secondary
masters come back with standard layout placeholders in ODP (PPTX keeps them). Not fixed in this round.
