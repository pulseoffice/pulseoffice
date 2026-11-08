# Deep audit, round 2: formats

Area: `src/PulseOffice.Formats`, `Formats.Odf`, `Formats.OpenXml`, `Formats.Package`, `Formats.Pdf`, `Formats.Interchange`.

Focus: performance regressions of the kind round 1 introduced (linear scans inside loops, part-name comparisons
with a different case sensitivity than the Open XML library, caps that silently cut legitimate data), measured on
large synthetic documents, then a full re-read of the central readers and writers and a corpus run.

## Method

1. Files touched by round 1 (`git log -i --grep=audit` on the format projects) scanned for `PreviousNode`,
   `NodesBeforeSelf`, `ElementsBeforeSelf`, `IndexOf`, `Count()`, `ElementAt`, case-insensitive comparisons and
   numeric caps; each hit inspected in context.
2. Large-document performance tests in both test projects (`OdfLargeDocumentPerformanceTests`,
   `OpenXmlLargeDocumentPerformanceTests`): 100,000-row sheet; 40,000 merges, comments or styles; 10,000 plain and
   formatted paragraphs; 2,000 slides; one slide with 20,000 shapes; a group of 20,000 shapes. Limits are generous
   (30 s) except where a defect was found, where the limit sits well below the time measured before the fix.

## Measurements (this machine, Release of the test build)

| Case | ODF write / read | OOXML write / read |
|---|---|---|
| 100,000 rows x 2 columns | 1.2 s / 1.1 s | 1.0 s / 1.7 s |
| 40,000 merges | 0.4 s / **7.3 s -> 0.9 s** | 0.1 s / 0.2 s (after the shared fix) |
| 40,000 comments | 0.3 s / 0.8 s | 0.5 s / 1.1 s |
| 40,000 styled cells (3,000 styles) | 0.6 s / 0.7 s | 0.2 s / 0.2 s |
| 10,000 paragraphs (plain / formatted) | 0.1 s / 0.1 s | 0.2 s / 0.2 s |
| 2,000 slides | 0.2 s / 0.1 s | 0.2 s / 0.4 s |
| 20,000 shapes on one slide | 0.7 s / **5.6 s -> 0.5 s** | 0.5 s / 1.2 s |
| group of 20,000 shapes | 0.6 s / 0.6 s | 0.9 s / 2.1 s (4.0 s doubling: linear) |

## Defects

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| 1 | Major | `src/PulseOffice.Formats/OverlappingMerges.cs:16` | Every merge read from ODS or XLSX scanned all merges already read (`merges.All.Any`): quadratic, 40,000 merges took 7.3 s | uses the banded index `MergedRegions.Overlapping` | `A_sheet_with_many_merges_comments_and_styles_writes_and_reads_in_linear_time(merge)` in both projects |
| 2 | Major | `src/PulseOffice.Formats.Odf/Presentation/Reading/PageReaders.cs:23` | Shapes without `draw:z-index` (all ODP files, ours included) took their order from `parent.Elements().ToList().IndexOf(node)`: quadratic per slide, 20,000 shapes took 5.6 s | `OdpSiblingIndex`: positions computed once per parent and kept as an annotation | `A_slide_with_twenty_thousand_shapes_without_z_index_reads_in_linear_time` |
| 3 | Minor | `src/PulseOffice.Formats.Odf/Presentation/Reading/SlideReading.cs:238` | Custom shows checked each slide id with a linear search over the slides | set of slide ids | covered by the existing custom-show round trips |
| 4 | Minor | `src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs:543` | Content beyond the sheet limits scanned the whole diagnostic list for every cell outside | checked once per row | existing ODF043 tests |
| 5 | Major (silent loss) | `src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs:17` (`MaxContentRepeat = 4096`) | Every repeat in an ODS sheet was cut at 4,096 without a diagnostic: a value row repeated 10,000 times (the reference suite compresses identical consecutive rows this way), a value repeated across 5,000 columns, hidden rows or row heights repeated beyond 4,096 and array formulas taller than 4,096 rows all lost data silently | `OdsRepeatBudget`: repeats are clamped only to the sheet bounds; the total of materialized cells (4 Mi) and of row and column settings (4 Mi) per workbook is the bomb guard, and cutting reports the new warning **ODF270** | `A_value_row_repeated_ten_thousand_times_is_read_in_full`, `A_value_cell_repeated_across_five_thousand_columns_is_read_in_full`, `Hidden_rows_repeated_beyond_four_thousand_stay_hidden`, `A_repeated_block_beyond_the_cell_budget_is_cut_and_reported` (all failed before) |

The performance test classes run without parallelism (`DisableParallelization`): under the parallel load of the
whole project the timings tripled.

## Defects, second group

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| 6 | Major | `src/PulseOffice.Formats.OpenXml/Spreadsheet/SheetSections.cs:747` | Each cell hyperlink of an XLSX sheet searched the part's hyperlink relationships linearly: 40,000 links took 10.3 s to read | `HyperlinkTargets`: one dictionary per part (weak table keyed by the part) | `A_sheet_with_many_merges_comments_and_styles_writes_and_reads_in_linear_time(link)` (10.3 s -> 1.4 s, limit 4 s) |
| 7 | Medium | `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxWriteContext.cs:140` (`DocxReadContext.Link`), `Presentation/PptxRunLinks.cs:22`, `Spreadsheet/XlsxDrawingRunLinks.cs:11`, `Spreadsheet/XlsxObjectLinks.cs:36` | Same linear search for every hyperlink of a DOCX part, slide, drawing or drawing object | same service | `A_text_with_ten_thousand_formatted_paragraphs...` now carries a hyperlink per paragraph (DOCX read 1.5 s -> 0.9 s) |

Note: the existing timing tests (`A_pivot_over_many_distinct_items_is_written_quickly` and similar) are sensitive to
machine load; they failed once while the corpus run used the CPU and passed on rerun.

## Defects, third group: many number formats

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| 8 | Major | `src/PulseOffice.Formats.OpenXml/XlsxStyleMapper.cs:157` and `:135`, `CellStyles/Codecs.cs:54`, `Conditional/RuleCodecs.cs:49,64` | Writing XLSX searched all written `numFmt` elements for every style (`Elements().Any`) and took each new index from `ChildElements.Count`, which walks the children: 40,000 cells with distinct number formats took **64.8 s** to save | set of written format ids and running counters | `A_sheet_with_many_merges..._in_linear_time(format)` in the OOXML project (64.8 s -> 0.9 s, write limit 8 s) |
| 9 | Medium | `src/PulseOffice.Sheets/Formatting/NumberFormatCatalog.cs:100` (outside the area, minimal change) | `CodeOf` looked the custom code up by scanning every registered format; ODS writing calls it for every cell, so saving was quadratic in cells x formats (40,000: 2.6 s, 80,000: 8.8 s) | reverse dictionary id -> code kept by `Register` | same theory in the ODF project (2.6 s -> 0.55 s); `PulseOffice.Sheets.Tests` green |
| 10 | Minor | `src/PulseOffice.Formats.Odf/OdfStyleMapper.cs:31` | Every new data style was inserted at the head of the declaration list (`InsertRange(0, ...)`), moving all previous ones | data styles and cell styles kept in two lists, same output order | same test |
| 11 | Minor | `src/PulseOffice.Formats.OpenXml/XlsxReader.cs:231` | Reading searched the `numFmts` list for every cell format | dictionary built once (first declaration wins, as before) | same test |

The wide-sheet case (40 rows x 10,000 columns, `A_wide_sheet_with_ten_thousand_columns_reads_in_linear_time`) was
added as a guard for the row compactor of the streaming ODS reader: it reads in 0.9 s, removals stay next to the run
they extend, so no defect there.

## Corpus run and the fix it led to

First full run after defect 5: **LOSS 1** (a corpus file, formulas, `ODF270`). The file fills
its first sheets with styled empty rows repeated 983,039 times across 1,011 columns; with the cap gone that
formatting filler used up the whole cell budget and the formulas of the later sheets were not loaded.

| # | Severity | Where | Defect | Fix | Test |
|---|---|---|---|---|---|
| 12 | Major (introduced by fix 5, caught before commit to main) | `src/PulseOffice.Formats.Odf/Spreadsheet/SheetPartReaders.cs` (`OdsTableReader.ReadRow`) | Formatting-only cells drew on the same budget as content | cells that carry no value, formula or link keep the former 4,096-copy cap (rows and columns) and never draw on the budget; only content does | `Formatting_filler_repeated_to_the_sheet_end_does_not_use_up_the_budget_of_later_content` (failed before) |

Final corpus run (`run ... 2 60`, after all fixes): summary identical to `tools/corpus-audit/summary.md`: 5,016 files,
9,948 copies OK, NOTZIP 14, READFAIL 28, **LOSS 0**, DECL 529, no TIMEOUT.

## Coverage of the re-read

Read in full: `OdsReader.cs`, `Spreadsheet/SheetPartReaders.cs`, `Spreadsheet/StreamingContent.cs`, `Tables/TableCells.cs`,
`OdpReader.cs`, `Presentation/Reading/PageReaders.cs`, `Presentation/Reading/SlideReading.cs`, `OdfStyleMapper.cs`,
`XlsxReader.cs`, `XlsxStyleMapper.cs` (writing half), `Packaging/OoxmlPackageRepair.cs`, `Packaging/PackageRelationships.cs`.
Not re-read in this round: `OdsWriter.cs`, ODT and DOCX readers and writers, `PptxReader.cs`/`PptxWriter.cs`; they were
covered only by the large-document measurements (all linear).

## Remaining, not fixed (minor)

- `Presentation/Reading/PageReaders.cs` `AnimationTargetResolver.Paragraph`: one descendant scan per paragraph animation.
- `PptxInteractionMapper.cs:143`, `PptxMediaMapper.cs:98`, `Shapes/DeckObjectCodecs.cs:350`: linear relationship search per shape (few per slide).
- `OdpSlideReader.Read`: unnamed pages try suffixes from 2 each time (quadratic only with tens of thousands of unnamed slides).
- Existing timing tests (`A_pivot_over_many_distinct_items_is_written_quickly`) are load-sensitive.
