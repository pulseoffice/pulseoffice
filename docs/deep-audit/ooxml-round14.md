# Deep audit round 14: OpenXml and the other non-ODF readers

## Scope

Round 13 found ODF tables of Deck and inline chart data without a cap on the total. This round made the equivalent
cross-cutting pass on every non-ODF reader: `Formats.OpenXml` (DOCX, XLSX, PPTX, charts) and `Formats.Interchange`
(RTF, HTML, CSV, plain text through `TextDocumentAssembler`). The earlier logs (`openxml.md`, `formats-round2.md`,
`formats-round3.md`, `package.md`) already record a full reading of these projects; this round read again, in full, the
methods that turn declared counts, spans or repeated elements into allocations.

| Area | Code read | Outcome |
|---|---|---|
| PPTX tables (`a:tbl`) | `PptxDeckTableMapper.cs` 302/302 | **defect 1** |
| DOCX tables, `gridSpan`, `vMerge`, nested tables | `DocxBlockReader.cs` table methods (lines 285-460) | **defects 2, 3** |
| XLSX columns (`cols/col min..max`) | `SheetSections.cs` `Columns`, `ReadCells`, row index | **defect 4**; cells are sparse (one model cell per XML cell, address validated) |
| XLSX chart caches (`c:ptCount`, sparse `idx`) | `Charts/ChartSpaces.cs` `Literals`, `Indexed`; `XlsxChartMapper.PivotChartExtent` | capped already (100,000 points per series, 1,000,000 per chart) |
| XLSX pivot caches, tables, query tables | `XlsxPivotMapper.cs`, `Tables/XlsxTables.cs`, `XlsxQueryTableMapper.cs` loops | no declared count used to allocate; shared item indexes bounds-checked |
| HTML tables | `Text/Html/HtmlTableReader.cs` 305/305 | capped per table (1,000,000 cells, spans 1-1,000) |
| RTF tables | `Text/Rtf/RtfDocumentReader.cs` table methods (720-811) | rows padded by the assembler: **defect 2** (document total) |
| Assembler of RTF/HTML/text | `Text/Assembly/TextDocumentAssembler.cs` 180-416 | **defects 2, 5** |
| Declared counts used to allocate (`new T[(int)…]`, `Enumerable.Repeat`, `while (list.Count < declared)`) | grep over both projects, each hit read in context | only the four places above |
| EMU, row heights, widths, list starts, nesting depth | already capped in `openxml.md` (rounds 1-3); package depth cap in `Formats.Package` | not repeated |

## Shared component

`src/PulseOffice.Formats/ImportedTableLimits.cs` holds the caps now shared by every reader:
`SlideTableRows` / `SlideTableColumns` 1,000 and `SlideTableCells` 100,000 (Deck tables: the ODP reader of round 13 now
uses these constants too), `TextTableColumns` 1,024 and `TextTableCells` 1,000,000 per text table (the values the ODT
reader and the RTF/HTML assembler already used), and `DocumentTableCells` 2,000,000 for all text tables of a document.
`ImportedCellBudget` grants rows against that document-wide budget (same shape as `OdsRepeatBudget`).

## Defects

| # | Severity | Where | Defect | Fix | Test (before -> after) |
|---|---|---|---|---|---|
| 1 | Serious (denial of service) | `PptxDeckTableMapper.cs:159-179` | A PPTX table read every `a:tr` and padded each to the largest of the `a:gridCol` count and the widest row: N empty rows x M grid columns, both a few bytes each (and compressed to nearly nothing). 3,000 x 3,000 gave 9,000,000 cells; a few hundred KB of XML gives billions | rows, grid columns and cells per row capped at 1,000, total cells at 100,000, the same caps as the ODP reader | `Una_tabella_PPTX_con_righe_e_colonne_a_dismisura_resta_entro_i_tetti_di_Deck` (3 cases): 3,000 / 5,000 / 5,000 -> green |
| 2 | Serious (denial of service) | `Wordprocessing/DocxBlockReader.cs:307,365-368`; `Text/Assembly/TextDocumentAssembler.cs:218` | DOCX: no cap on `w:gridCol` count (5,000 columns kept) and rows padded to the grid with one paragraph per cell: `<w:tr/>` x 1,024 grid columns = 1,228,800 cells from 1,200 empty rows. A per-table cap alone is not enough: the same table repeated N times (or nested) multiplies it; this held for the RTF/HTML assembler too | grid and cells per row capped at 1,024; rows stop at 1,000,000 cells per table; a document-wide budget of 2,000,000 cells (`ImportedCellBudget` in `DocxReadContext` and in the assembler). The cut is declared: new code **DOCX222**; RTF/HTML keep their existing `ODF258` | `Una_griglia_DOCX_di_migliaia_di_colonne_...` (5,000 -> green), `Righe_DOCX_vuote_moltiplicate_...` (1,228,800 -> green), `Molte_tabelle_DOCX_larghe_..._dichiarata`, `MolteTabelleHtmlLargheRestanoEntroIlTettoDiCelleDelDocumento` (the last two written after the fix: running them before would have allocated over 2 million cells, see note) |
| 3 | Medium (quadratic) | `Wordprocessing/DocxBlockReader.cs:409,427` (`Closings`, `Edges`) | for every cell, a linear search in the list of cells with own borders (`FirstOrDefault`) and of vertical-merge closings (`LastOrDefault`): 120,000 bordered cells took 12.3 s | reference-keyed dictionaries (first own borders, last closing), same semantics | `Molte_celle_DOCX_con_bordi_propri_si_leggono_in_tempo_lineare`: 12.3 s at 3,000 rows -> about 1.5 s; now 6,000 rows under a 20 s limit |
| 4 | Medium (time amplification) | `Spreadsheet/SheetSections.cs:725-729` (`Columns`) | every `<col min="1" max="16384">` walked 16,384 columns, and nothing stopped them from overlapping: 3,000 such elements (about 150 KB of XML) took 10 s, 30,000 would take minutes | elements ordered by `min`, each column handled once (overlap is invalid in SpreadsheetML; on overlap the range starting first wins) | `Colonne_XLSX_ripetute_su_tutto_il_foglio_si_leggono_in_tempo_lineare`: 10.0 s -> 0.17 s |
| 5 | Minor (quadratic) | `Text/Assembly/TextDocumentAssembler.cs:410` (`Place`) | the grid row array grew by one element per cell placed: O(columns^2) copies per row | geometric growth capped at the column limit | covered by the existing HTML/RTF table tests |

Note on defect 2: the two document-budget tests were not run against the old code on purpose; the old code would have
built over 2 million table cells with paragraphs (well over a gigabyte) on a machine shared with other agents. The
per-table tests of the same defect did fail before the fix.

## Remaining

- `DocxBlockReader.OriginAbove` walks up the column for each `vMerge` continuation: O(rows^2) on a single vertical
  merge of tens of thousands of rows (minor; bounded now by the table cap of 1,000,000 cells).
- The PPTX table reader has no diagnostic channel, as in ODP (round 13): the cut beyond 1,000 rows or columns is silent;
  such tables are hostile by construction (PowerPoint itself refuses more than 75 rows or columns in its UI).
- `PptxDeckTableMapper.ReadLayout` keeps column fractions from the grid only, so a row wider than the grid gets no
  fractions (pre-existing behaviour, not a size issue).

## Tests run

`PulseOffice.Formats.OpenXml.Tests` in full (2,155 tests; under parallel load two timing tests failed, the pre-existing
`A_pivot_over_many_distinct_items_is_written_quickly` and the new bordered-cells test, both green alone; the new one was
then made more robust), `PulseOffice.Formats.Interchange.Tests` in full (131, green), `PulseOffice.Formats.Odf.Tests` (764, green)
in full. No live tests needed: readers only.
