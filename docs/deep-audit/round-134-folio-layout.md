# Deep audit round 134: Folio layout engine

Area: `src/PulseOffice.Documents.Layout` (pagination, line breaking and composition, tables, columns, sections, notes,
objects and wrap, headers and footers, incremental relayout). Previous full reading: round 60. Tests in
`tests/PulseOffice.Documents.Layout.Tests/Round134LayoutTests.cs`.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Flow/Paginator.cs | 1292/1292 | keep-with-next ignored a following table (fixed) |
| Flow/LayoutEngine.cs | 347/347 | ok: suffix probe keyed by full page start (number included), bounded count passes |
| Flow/Stories.cs | 573/573 | ok; decorated paragraphs in one very long cell are inserted at the head of the box list (quadratic only with thousands of bordered paragraphs in one cell, left) |
| Flow/FlowBuilder.cs | 338/338 | ok |
| Flow/PageFrames.cs | 274/274 | ok: first/even header choice matches the paginator's notion of first page |
| Flow/Placement.cs | 256/256 | ok: balancer ignores widows and keeps (the last column absorbs the difference) |
| Flow/FlowItems.cs | 153/153 | ok |
| Flow/ParagraphEntry.cs, ParagraphEntries.cs | 141+123 | ok |
| Flow/ChangeMarkup.cs, ListLabeling.cs, PageBackdrops.cs, PageFields.cs, LayoutFieldFacts.cs, FlowSettings.cs | 184+154+65+74+48+96 | ok |
| Flow/PageNumbers.cs, PlaceholderMarkup.cs, TableMeasure.cs | 22+37+47 | ok |
| Lines/LineBreaker.cs | 153/153 | no break opportunity inside ideographic text (fixed) |
| Lines/LineComposer.cs | 185/185 | justification found nothing to stretch in ideographic lines (fixed) |
| Lines/LineBidi.cs, LineSpacing.cs, Tabs.cs | 122+45+58 | ok |
| Inline/ParagraphShaper.cs, RunTypesetting.cs, ShapedParagraph.cs, InlineInserter.cs | 161+120+118+77 | ok |
| Measurement/TextMeasurement.cs, OpenType/OpenTypeMeasurer.cs | 57+95 | ok |
| Queries/LayoutQueries.cs, LineOffsets.cs, LineNumbers.cs | 126+143+35 | ok |
| Tree/LayoutTree.cs | 560/560 | ok |
| LayoutServiceCollectionExtensions.cs | 84/84 | new service registered |

All files of the project read in full.

## Defects

| Severity | Where | Defect | Status |
|---|---|---|---|
| Medium | Lines/LineBreaker.cs `BreakOne` | Chinese and Japanese text had no break opportunity except spaces: after a Latin word the whole ideographic run moved to the next line leaving a ragged gap, and every ideographic line was an emergency break (marked `Broken`, so wrap around objects skipped narrow segments) with no rule against a line starting with `。` or ending with `「` | fixed: new `ILineBreakOpportunities` (`IdeographicBreaks`) allows a break before or after an ideographic character, with the usual line-start and line-end prohibitions; Hangul keeps breaking at spaces as the reference word processors do by default |
| Medium | Lines/LineComposer.cs `Justify` | a justified ideographic line has no spaces, so it stayed ragged | fixed: without spaces, an ideographic line spreads the free room between characters |
| Medium | Flow/Paginator.cs `ChainHeight` | a heading with keep-with-next followed by a table stayed alone at the bottom of the page: the chain stopped at the table | fixed: the chain includes the table's first row group (and its repeated header rows) |

All three tests failed before the fix and pass after it.

## Left open

- A keep-with-next chain also stops at a section boundary (the next paragraph in a new section may change width).
- Widow and orphan control is not applied inside table cells split across pages.
- Column balancing ignores widows, orphans and keeps; the last column absorbs the difference.

## Tests

`PulseOffice.Documents.Layout.Tests` 263/263, `PulseOffice.Architecture.Tests` 43/43.

## To try live

Type a Chinese or Japanese paragraph after a Latin word in Folio and check that lines fill to the margin and no line
starts with `。` or `、`; justify it; put a keep-with-next heading just before a table near the bottom of a page.
