# Deep audit, round 3: formats

Area: `src/PulseOffice.Formats`, `Formats.Odf`, `Formats.OpenXml`. Round 2 (`formats-round2.md`) left `OdsWriter.cs`,
the ODT and DOCX readers and writers and `PptxReader.cs`/`PptxWriter.cs` covered only by large-document timings, and
four minor items open. This round re-reads those files in full, closes the minors and measures each suspected
quadratic path with a synthetic document before and after the fix.

## Coverage of the re-read

| File | Lines read | Outcome |
|---|---|---|
| `Formats.Odf/OdsWriter.cs` | 577/577 | defects 1 and 2 |
| `Formats.Odf/Odt/OdtReader.cs` | 507/507 | no defect |
| `Formats.Odf/Odt/OdtWriter.cs` | 455/455 | no defect |
| `Formats.OpenXml/Wordprocessing/DocxReader.cs` | 427/427 | defect 3 |
| `Formats.OpenXml/Wordprocessing/DocxWriter.cs` | 435/435 | no defect |
| `Formats.OpenXml/PptxReader.cs` | 169/169 | no defect in the file; defect 4 found through its slide readers |
| `Formats.OpenXml/PptxWriter.cs` | 117/117 | no defect in the file; defect 4 found through `SlideWriter` |
| `Formats.OpenXml/PptxInteractionMapper.cs`, `PptxMediaMapper.cs`, `Shapes/DeckObjectCodecs.cs` (picture codec) | relevant classes | round-2 minor closed (defect 5) |
| `Formats.Odf/Presentation/Reading/PageReaders.cs` (`AnimationTargetResolver`), `SlideReading.cs` (`OdpSlideReader`) | relevant classes | round-2 minors closed (defects 6 and 7) |

## Defects

Every defect has a test in `OdfLargeDocumentPerformanceTests` or `OpenXmlLargeDocumentPerformanceTests` that failed
before the fix (verified by running it against the old code) and passes after.

| # | Severity | Where | Defect | Fix | Test (before -> after) |
|---|---|---|---|---|---|
| 1 | Major | `Formats.Odf/OdsWriter.cs:563-567` (`BuildRow`) and `Spreadsheet/OdsObjectLinks.cs:31` | For **every row** of a sheet holding objects, the writer rebuilt the dictionary of all object hyperlinks (`OdsObjectLinks.Wrap`) and looked each anchored object's alt text up with a linear `FirstOrDefault`: rows x objects | `PlacedObjectIndex` (new, `Formats`): ids, contents and links indexed once per sheet; `Wrap` takes the prepared links | `A_sheet_with_many_linked_and_described_shapes_writes_in_linear_time`, 12,000 shapes: 37.4 s -> 0.6 s |
| 2 | Medium | `Formats.Odf/OdsWriter.cs:460` (`StyledWidth`), `:271`, `:286` | `ColumnStyles.Keys.Max()` expands every styled column of the run-length map, and `Padded` calls it for every row: a sheet styled on all 16,384 columns paid 16,384 steps per row. `BuildColumns` and `TrailingColumns` also expanded the keys | width taken from the last run; trailing columns walk `RunsIn` | `A_sheet_with_every_column_styled_writes_in_linear_time`, 120,000 rows: 4.9 s -> 1.1 s |
| 3 | Major | `Formats.OpenXml/Wordprocessing/DocxReader.cs:222-233` (`Link`) | Every linked text box replaced the whole object collection (`Objects.Replace`) and searched it again: text-box chains x objects, 6,000 chains took 13.2 s to read | copies collected by reference and applied in one `Replace` | `A_text_with_many_linked_text_boxes_reads_in_linear_time`, 6,000 chains: 13.2 s -> 1.1 s |
| 4 | Major | `Presentations/Slide.cs:213` (`Interaction`, `SetInteraction`, `RemoveInteraction`; outside the area, minimal change) | Each lookup of a shape's click action scanned the slide's whole list: PPTX `SlideWriter` (`:83`, `:93`, `:108`), ODP `ElementWriters:74`, both readers (`SlideParts:114`, `PageReaders:208`) and `PptxSounds:81` call it once per element | index by target id kept by the slide (first action wins, as `FirstOrDefault` did), rebuilt only after a removal | `A_slide_with_thirty_thousand_linked_shapes_writes_and_reads_in_linear_time`: write 3.0 s -> 1.2 s, read 9.5 s -> 3.0 s (with defect 5) |
| 5 | Minor (round-2 open item) | `PptxInteractionMapper.cs:143`, `PptxMediaMapper.cs:88,98`, `Shapes/DeckObjectCodecs.cs:350` | Linear relationship search per hyperlink, media and linked picture | `PartRelationshipIndex<T>` (new, `Packaging/`) with hyperlink, external and data-reference variants; `HyperlinkTargets` now delegates to it | same test: read 9.5 s -> 5.0 s before defect 4 was fixed |
| 6 | Minor (round-2 open item) | `Formats.Odf/Presentation/Reading/PageReaders.cs:312` (`AnimationTargetResolver.Paragraph`) | One descendant scan and `IndexOf` per paragraph animation | paragraph positions computed once per frame | `A_frame_with_twelve_thousand_animated_paragraphs_reads_in_linear_time`: 4.5 s -> 0.5 s |
| 7 | Minor (round-2 open item) | `Formats.Odf/Presentation/Reading/SlideReading.cs:57` | Unnamed pages probed suffixes from 2 each time | `OdpSlideIds`: per-read set of ids and next suffix per name | `Ten_thousand_unnamed_pages_read_in_linear_time`: 2.1 s -> 0.17 s |
| 8 | Minor | `Formats.OpenXml/XlsxDrawingMapper.cs:66` | Same linear alt-text lookup per drawing anchor as defect 1 (cheaper here: once per sheet, 12,000 objects 1.5 s) | `PlacedObjectIndex` | covered by `A_sheet_with_many_linked_and_described_shapes_writes_and_reads_in_linear_time` (OOXML) |

No silent loss between formats, no asymmetric write/read and no swallowed exception was found in the files re-read:
`OdtReader` drops an undecodable protection key to "no key" (`FormatException`), which leaves the restriction in place,
as in round 1.

## Remaining (not fixed)

- `HyperlinkTargets`/`PartRelationshipIndex` rebuild a part's index on every miss, so a file whose links point at
  missing relationships stays linear per dangling link (damaged files only); the rebuild keeps lookups correct if a
  reader adds relationships while reading.
- `DocxReader.Sections`: each continuous section with its own columns moves its blocks with `RemoveRange`/`Insert` on
  the body: sections x blocks, only with thousands of column changes.
- `PptxWriter.Write` lets I/O exceptions propagate, while `DocxWriter` and `OdtWriter` turn them into a failed result;
  callers catch them today, so it is a consistency point, not a loss.

## Corpus run

Final run (`run ... 2 60`, Release, after all fixes): summary byte-identical to `tools/corpus-audit/summary.md`:
5,016 files, 9,948 copies OK, NOTZIP 14, READFAIL 28, **LOSS 0**, DECL 529, no TIMEOUT.

## Tests run

`PulseOffice.Formats.Odf.Tests` (760 passed), `PulseOffice.Formats.OpenXml.Tests` (2,142 passed),
`PulseOffice.Presentations.Tests` (307 passed), `PulseOffice.Architecture.Tests` (38 passed), one project at a time.
