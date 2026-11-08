# Deep audit round 60: Folio layout engine

Area: `src/PulseOffice.Documents.Layout` (pagination, lines, tables, columns, sections, notes, objects and wrap, keeps,
headers and footers, incremental relayout). Tests in `tests/PulseOffice.Documents.Layout.Tests/RoundSixtyProbeTests.cs`
(serial collection `IncrementalTests` for the timing probes).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Flow/Paginator.cs | 1277/1277 | column separator used the raw gap (fixed with the column layout) |
| Flow/LayoutEngine.cs | 347/347 | ok: suffix probe converges on equal page starts, page-count passes bounded (4) |
| Flow/PageFrames.cs | 274/274 | column count not clamped (fixed) |
| Flow/Placement.cs | 253/253 | ok: balancer bounded by iterations and capacity |
| Flow/ParagraphEntry.cs | 145/145 | ok: line suffixes reuse the whole composition, no quadratic split paragraphs |
| Lines/LineBreaker.cs | 153/153 | ok: every line advances at least one character, surrogate pairs kept |
| Lines/LineComposer.cs | 177/177 | ok |
| Lines/Tabs.cs | 58/58 | ok: NaN/non-positive interval falls back to 36 pt |
| Flow/FlowItems.cs | 149/149 | column width and position ignored absurd gaps (fixed); exact row height could be negative (fixed) |
| Flow/Stories.cs | 394/394 | ok: story slicing and table placement cached per width |
| Flow/FlowBuilder.cs | 338/338 | column count clamp (fixed) |
| Flow/ParagraphEntries.cs | 122/122 | ok: spacing, widows, orphans, indents sanitised |
| Flow/PageNumbers.cs, PageFields.cs, LayoutFieldFacts.cs, TableMeasure.cs | 22+74+48+47 | ok |
| Lines/LineSpacing.cs | 41/41 | negative leading gave negative line heights (fixed) |
| Flow/FlowSettings.cs | 96/96 | ok |
| Inline/ParagraphShaper.cs, RunTypesetting.cs, ShapedParagraph.cs, InlineInserter.cs | 161+120+106+75 | ok: drop-cap lines are clamped by both readers |
| Measurement/TextMeasurement.cs, OpenType/OpenTypeMeasurer.cs | 57+95 | ok |
| Queries/LineNumbers.cs, LayoutQueries.cs, LineOffsets.cs | 35+104+118 | ok |
| Flow/ListLabeling.cs, PageBackdrops.cs, PlaceholderMarkup.cs, ChangeMarkup.cs | 150+64+37+184 | watermark angle/transparency NaN gave a NaN font size (fixed) |
| LayoutServiceCollectionExtensions.cs | 79/79 | ok (layout is single-threaded: no background layout callers) |
| Tree/LayoutTree.cs | 531/531 | `RotatedRect.Around` turned a NaN rotation into a NaN rectangle (fixed) |

All 33 files of the project read in full (5931 lines).

## Probes (all bounded with a 20 s watchdog)

| Probe | Result |
|---|---|
| 3000 paragraphs all keep-with-next | 91 pages, 73 ms |
| footnote of 200 paragraphs on a 100 pt page | terminates, note split |
| 45 columns with gap wider than the page, section with negative gap | terminates |
| one cell of 2000 paragraphs under a 30-line repeated header | 127 pages, 42 ms |
| keep-together + keep-with-next paragraph taller than the page | terminates |
| nested table taller than the page, negative row height | terminates |
| balanced 3-column section of 4000 paragraphs | 84 pages, 97 ms |
| one paragraph of 100 000 words with borders | 433 pages, 110 ms |
| 751 pages, one line added on page 10, five edits | relayout median 19 ms against a full layout of 285 ms |

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | Flow/FlowItems.cs `ColumnLayout` | a column gap from a file wider than the body pushed every column after the first off the page (text invisible); a negative gap made columns overlap | fixed: `Spacing(total)` clamps the gap to `[0, (total - count) / (count - 1)]`, width and left use it, the separator line too |
| Medium | Flow/FlowItems.cs `TableRowItem.Height` | an exact row height below zero (file, macro) gave a negative row: the table had negative height and the following text climbed back over the preceding one | fixed: row heights are clamped at zero |
| Medium | Lines/LineSpacing.cs `LeadingSpacing` | a negative leading larger than the line gave negative line heights: lines stacked upwards, off the top of the page | fixed: the line height is clamped at zero, the ascent follows |
| Minor | Tree/LayoutTree.cs `RotatedRect`, Flow/Placement.cs `ObjectOutlines` | a NaN rotation (macro, agent) produced NaN bounds, so the object stopped pushing text aside | fixed: a non-finite rotation is treated as none |
| Minor | Flow/PageBackdrops.cs | a NaN watermark angle or transparency produced a NaN font size and position | fixed |
| Minor | Flow/PageFrames.cs, Flow/FlowBuilder.cs | the column count from the model (macros, agent) was not clamped: a million columns allocated per-column arrays on every region | fixed: clamped to `SectionColumns.LargestCount` |

No non-termination, no quadratic behaviour and no stale page numbers were found: every probe of the brief terminates in
milliseconds, and the incremental relayout converges on equal page starts (the page number is part of the start, so a
shifted numbering is re-laid rather than reused). The full `PulseOffice.Documents.Layout.Tests` project passes (224).

Open: a proportional line spacing factor around 1e300 still overflows to infinite heights (readers bound it; not reachable
from files). Nothing to try live beyond opening a file with an absurd column gap, a negative exact row height or a
negative leading.
