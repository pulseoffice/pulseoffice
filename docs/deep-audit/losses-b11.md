# Declared losses block B11: DOCX125, headers of continuous sections starting mid-page

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md, docs/deep-audit/losses-b8.md | DOCX125 rows | 10 rows, 4 files; B8 left the design outline |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 200-330 | `Sections` raises DOCX125 when a continuous section changes inherited properties and no page break follows in it |
| Documents/Styles/Properties.cs | 95-150 | `PageStyleName` is a style-level paragraph property; a deferred page style is positional, not a style property |
| Documents/Styles/StyleResolver.cs | 75-115 | effective paragraph is `paragraph with {...}`: a new `TextParagraph` property carries through |
| Documents/Styles/DirectFormatting.cs | 55-75, 140-190 | `Clear` rebuilds the paragraph: must keep the new property |
| Text.Rich/RichText.cs | 177-260 | `TextParagraph` record |
| Documents.Layout/Flow/Paginator.cs | 60-240, 280-295, 1160-1265 | page style changes only at `PageStyleName`; `NewPage` takes the explicit style or the next style |
| Documents.Layout/Flow/ParagraphEntry.cs, ParagraphEntries.cs | 1-110 / 75-95 | `ParagraphFlow` built from the effective paragraph; the entry key holds the whole `TextParagraph` |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs | 100-130, 215-240, 300-430 | continuous sections are written without header references (they inherit) |
| Formats.OpenXml/Wordprocessing/DocxSectionCodec.cs | 30-63 | `Write(..., continuous, headers, ...)` |
| Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs, DocxParagraphMarkSplits.cs | 50-60 / 95-102 | block controls and split paragraph marks must not carry a section change |
| Formats.Odf/Odt/OdtBodyWriting.cs, OdtBodyReading.cs | 85-125 / 135-175 | paragraph master page written on the automatic style |

## Specification check

- ECMA-376 Part 1 §17.6.22 (`w:type`, `continuous`): the section starts on the same page as the previous one. §17.10.5
  (`w:headerReference`): a section without a reference of a type inherits it from the previous section; Word shows a
  continuous section's own headers from the first page that begins inside it, which the paginator now models.
- ODF 1.3 Part 3 §19.509 (`style:master-page-name`): applying the master page inserts a page break. Neither
  `text:section` (§5.4) nor a master page's `style:next-style-name` (§19.507) gives a master page from the next page on
  at a given point of the text. ODT therefore carries it as our foreign attribute `pt:next-master-page`
  (`urn:pulseoffice:text`) on the `text:p`/`text:h`, ignored by other consumers (Part 3 §3.17), which keep the text and
  the master page itself.

## Design

- Model: `TextParagraph.NextPageStyleName`, a positional property (not a style property): the page style takes effect
  from the next page; if the paragraph opens a page, from that page. `PageNumberRestart` on the same paragraph applies
  with it. Kept by `DirectFormatting.Clear`, stripped from split paragraph-mark segments, stops DOCX block controls.
- Layout: `ParagraphFlow.NextPageStyleName`; `Paginator` remembers the deferred style (and restart) and `NewPage` uses it
  when no explicit style is asked; on an empty page it reopens the page with it at once.
- DOCX reader: a continuous section that changes properties with no page break inside creates its page style as for a
  page-breaking section and sets `NextPageStyleName` on its first paragraph (column wraps still apply). DOCX125 remains
  only when the section holds no top-level paragraph (only tables).
- DOCX writer: `BodyState.Defer` closes the previous section and opens a continuous one that writes its own header and
  footer references (`OwnHeaders`); also when the deferring paragraph leads a column section.
- ODT: `pt:next-master-page` written and read.

## Defects

- Medium (fixed): DocxReader.cs `Sections` dropped the headers and footers of continuous mid-page sections (DOCX125).

## Tests

- Documents.Layout.Tests `DeferredPageStyleTests` (3), Formats.OpenXml.Tests `DocxDeferredSectionHeaderTests` (4),
  Formats.Odf.Tests `OdtDeferredPageStyleTests` (1). Layout 203/203, OpenXml 2260/2260, Odf 776/776, Architecture 40/40.
- Corpus `one`: 090716_Studentische_Arbeit_VWS.docx and a corpus case no longer raise DOCX125.
- Full corpus run after B11: LOSS 0, DECL 133 (no DOCX125 row left).
