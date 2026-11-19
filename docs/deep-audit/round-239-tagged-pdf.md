# Round 239: tagged and accessible PDF

Area: PDF export accessibility and metadata. Round 238 worked on page labels, form fields, links and bidirectional
text in the same writer at the same time, so this round added new collaborators and kept away from the annotation and
navigation code.

## Defects found

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Serious | PDF export wrote no tagged structure: no marked content, no `StructTreeRoot`, no `MarkInfo`, no role map. Screen readers had no reading order, headings, lists, tables or alternative text, and the files could not claim PDF/A level A | Fixed |
| 2 | Medium | Agent PDF export (`ShellAgentWorkspace`) and "send as PDF" (`ShellSendCommands`) wrote no `/Lang`, keywords or author, and "send" ignored the document title | Fixed |
| 3 | Medium | When the document properties have no language, the catalog got no `/Lang` even though Folio, Ledger and Deck all know the text language | Fixed: `IPrintableDocument.Language`, used by the exporter when the options name none |
| 4 | Minor | Figures without alternative text passed silently | Fixed: warning PDF015 |
| 5 | Minor | Link and comment annotations are not linked into the structure tree (no `/StructParent`, no `OBJR`) | Queued (annotations belong to round 238's code) |
| 6 | Minor | No layout attributes: figures have no `BBox`, merged cells no `ColSpan`/`RowSpan`, row headers are not detected | Queued |
| 7 | Minor | Folio change bars, line numbers, field shading and spotlights are drawn inside the paragraph's marked content instead of as artifacts | Queued |
| 8 | Minor | Deck text boxes are one `P` element each (no element per paragraph, slide lists not tagged `L`), Deck tables are figures | Queued |
| 9 | Minor | No PDF/UA identifier is written; the file meets the main tagged-PDF rules but has not passed a PDF/UA checker | Queued (verification) |

## Design

- **Shared model** (`PulseOffice.Printing/PrintStructure.cs`): `PrintTag` (stable key, `PrintRole`, parent tag, heading
  level, alternative text, language) and `IPageCanvas.Tag(PrintTag?)`, a default no-op. Painting code names the element
  the next drawing belongs to; `null` means artifact (backgrounds, grid, borders, headers and footers, page chrome).
  Keys are stable across pages, so a paragraph or table split by a page break is one element with content on both
  pages. Every wrapper canvas (clipped, scaled, filtered, path-less) forwards the call.
- **PDF side** (`PdfStructure.cs`): `PdfStructureTree` collects elements in first-seen order and the marked-content
  references of every page; `PdfMarkedContent` (one per page, injected into `PdfContentCanvas`) opens `BDC` with an
  `MCID` (or `/Artifact BMC`) lazily before the first drawing of a new element and closes it when the element changes or
  the page ends; `IPdfStructureWriter` writes the elements under a `Document` element, the parent tree, the role map
  (`Sheet`, `Slide` → `Sect`, `Title` → `H1`, `H7`–`H10` → `H6`), `/StructParents` and `/Tabs /S` on pages with
  content; `TH` cells carry `/Scope /Column`. The exporter adds `/MarkInfo << /Marked true >>` and `/StructTreeRoot`;
  `PdfExportOptions.Tagged` (on by default) turns it off. With a structure, PDF/A export claims conformance level A.
- **Folio**: `IFolioStructureIndex` maps paragraphs to headings (outline levels), list items (`L`/`LI`/`Lbl`/`LBody`,
  consecutive items of one list grouped), table cells (`Table`/`TR`/`TH` for header rows/`TD`, nested tables inside
  their cell), text frames and figures (alternative text from description, title or name). Its tagger visitor is paired
  with the page painter through `PageMarks.Structure` and `LayoutVisitorPair`, so the painter itself is unchanged;
  hyperlink runs become `Link` children, footnote and endnote lines `Note`, headers and footers artifacts.
- **Ledger**: each sheet is a `Sheet` section, each printed page a `Table`, each row a `TR`, cells `TD` (print title
  rows `TH`); fills, grid lines, borders and row and column headings are artifacts; objects are figures with their
  alternative text; printed comments are `Note`.
- **Deck**: `ISlideStructureTags` declares each slide as a `Slide` section and its elements in reading order (title
  first, then stacking order); text boxes are `P`, the title `Title`, pictures and objects figures; master and layout
  elements and backgrounds are artifacts; notes pages tag the notes text as a `Note` of the slide.

## Tests

`Round239TaggedPdfTests` (structure, marked content balance, catalog entries, untagged option, level A, language
fallback, PDF015, element continued over two pages), `Round239SheetStructureTests`, `Round239FolioStructureTests`,
`Round239SlideStructureTests`. All small in-memory inputs.
