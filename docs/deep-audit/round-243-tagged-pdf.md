# Round 243 (2026-11-19): tagged PDF follow-ups

Area: the structure layer built in round 239 (`PrintTag`, `PdfStructure`, `IPageCanvas.Tag`). Round 244 worked in
parallel on PDF form fields, Deck links and page labels, so the changes stay in the structure layer and add
collaborators instead of reshaping shared writer methods.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Serious | Link and comment annotations were outside the structure tree (no `/StructParent`, no `OBJR`), so pages with `/Tabs /S` pointed at nothing and assistive tools could not reach links | `IPdfAnnotationTagging` (made by `IPdfStructureWriter.Annotations`) is an optional argument of `IPdfNavigationWriter.Annotations`; each annotation is reserved first, gets `/StructParent`, an `OBJR` kid in the `Link` element that tagged its run on the same page (`PrintTag.Target`), or a new `Link`/`Annot` element, and a `ParentTree` entry after the page keys; link annotations carry `/Contents` |
| Medium | A Ledger sheet printed over several pages became one table per page, with repeated title rows read again on every page | One table per sheet; rows and cells keep their keys across pages; repeated title rows and columns are artifacts |
| Medium | Row headers were never detected | Ledger title columns are `TH` with `/Scope /Row` (`/Both` at the corner); Deck data tables use the header row and first column style |
| Medium | Merged cells had no `ColSpan`/`RowSpan` | `PrintTag.ColumnSpan`/`RowSpan` from Folio cells, Ledger merges (visible rows and columns) and Deck table merges |
| Medium | Folio change bars, line numbers, field shading, field labels and spotlights were inside the paragraph's marked content | `IPageCanvas.BeginArtifact`/`EndArtifact` (forwarded by every wrapper and by `IPageSurface`), used by Folio's page painter around those marks |
| Medium | A Deck text box was one `P`; lists were not tagged; tables were figures | Text boxes are `Sect` with one `P` per paragraph and bullet runs as `L`/`LI`/`Lbl`/`LBody`; data tables are `Table`/`TR`/`TH`/`TD`. The slide is replayed from a recorded drawing, so the shared `ITextSegmentTagging` canvas (`PulseOffice.Printing`) assigns each drawn text run to the next paragraph or cell containing it and makes fills and strokes artifacts |
| Minor | Figures had no `BBox` | The marked content records the drawn area of `Figure` elements (`/A << /O /Layout /BBox >>`) |
| Minor | Line-end hyphens were read as hyphens | Folio tags hyphen runs as `Span` with an empty `/ActualText` |

## Open

The PDF/UA identifier is still not written: it waits for a run through a PDF/UA checker. The sequential text match in
Deck, the root-level link elements for cell and shape links, and the first-page, unrotated figure box are queued.

## Tests

`Round243TaggedPdfTests` (annotations, BBox, spans, scope, artifacts, ActualText), `Round243SegmentTaggingTests`,
`Round243SlideTextSegmentsTests`, `Round243SheetTableTests`, `Round243FolioCellSpanTests`; the round 239 slide test now
expects `Sect` for a body text box.
