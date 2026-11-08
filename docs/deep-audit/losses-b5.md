# Declared losses block B5: DOCX171 and the minor DOCX write codes

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md | B5 sections | DOCX171 53 rows; DOCX016/031/003/034/020/001/002 1-5 rows each |
| docs/deep-audit/losses-b4.md, losses-b4b.md | whole | `w:fldData` carrier with our marker; no ignorable namespace mechanism in DOCX parts |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs | 433/433 | rules; unused page styles never written |
| Formats.OpenXml/Wordprocessing/DocxSectionCodec.cs | 230/230 | sectPr write/read, header and footer parts |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 1-440/440 | section spans become `Standard`/`ConvertedN` page styles |
| Documents/Styles/UnusedHeaderVariants.cs | whole | DOCX171 predicate |
| Formats.OpenXml/Wordprocessing/DocxBibliography.cs | whole | custom XML part pattern (write with itemProps, safe read) reused |
| Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs | 560-850 | `urn:pulseoffice:docx:anchor` extension in `wp:docPr/a:extLst` reused for DOCX020 |
| Formats.OpenXml/Wordprocessing/DocxListHeaderIndent.cs, DocxParagraphCodec.cs | whole / 1-130 | list headers written with `numId 0` and the list indent |
| tests: DocxTenthAuditLinkTests, DocxThirteenthAuditTests, ObjectRoundTripTests | affected cases | expectations moved from declared losses to round trips |
| tools/corpus-audit/FeatureCounter.cs | 1-140 | header and footer parts referenced by no `sectPr` are counted as orphans (Word drops them) |
| corpus a corpus file, `birt_min.odt`, a corpus file | page styles | unused `First Page`/`Left Page` styles with header fields and tables |

## Specification check

- DOCX171: WordprocessingML has no page styles, only section properties (ECMA-376 Part 1 §17.6.17 `sectPr`); a header or
  footer part is shown only through a `w:headerReference`/`w:footerReference` of a section (§17.10.5, §17.10.2). Custom
  XML data parts (Part 1 §15.2.5, `customXml/itemN.xml` with an `itemProps` part, §22.5) hold arbitrary XML that
  consumers keep without interpreting. ODF 1.3 (§16.9 `style:master-page`) keeps unused master pages with their
  headers and footers.
- DOCX020: ODF `style:number-wrapped-paragraphs="1"` (ODF 1.3 §20.344) limits wrapping to the anchor paragraph; DrawingML
  has no such setting, but `wp:docPr` (CT_NonVisualDrawingProps, §20.4.2.5) has `a:extLst` (§20.1.2.2.15) whose
  extensions with an unknown `uri` are ignored by consumers.

## Design

- DOCX171: new service `DocxStoredPageStyles` (`Formats.OpenXml/Wordprocessing/DocxStoredPageStyles.cs`). After the
  body, every page style that no written section used (`DocxWriteContext.WrittenPageStyles`, filled by
  `DocxSectionCodec.Write`; a first run with model names instead missed styles named only by nested paragraphs or
  reached through a next style, 4 corpus LOSS rows), and that has a header, footer, hidden
  variant or watermark, or is not part of the standard style sheet, is written through `DocxSectionCodec.Write` into a
  full `w:sectPr` (real header and footer parts related to the main part) and stored in a custom XML part
  `<ps:pageStyles xmlns:ps="urn:pulseoffice:page-styles">` with name, usage, next style and even-pages flag. Reading,
  after the sections, restores each stored style unless a written section already uses its name; foreign or broken
  entries are skipped, at most 512 entries; the read does not alter the document's line numbering or watermark.
  Word shows the sections as before and ignores the part; when Word saves, it drops the unreferenced header parts and
  PulseOffice then finds the references missing and restores the page layout only. DOCX171 removed (rule, unused
  `IUnusedHeaderVariants.InPageStylesOutside`, `declared-features.json` entry; it had no localization keys).
- DOCX020: the existing `urn:pulseoffice:docx:anchor` extension gains `wrap-first-paragraph="1"` (and a `character`
  type for objects anchored to a character); the reader sets `WrapFirstParagraph`. Word wraps every paragraph beside
  the object, as before. DOCX020 removed.
- Known limit: a stored style whose name a written section takes on reading (the first section is always read as
  `Standard`, DOCX009) is not restored; its headers stay in the package.
- Corpus tool: header and footer parts referenced from our page-styles part are no longer counted as orphans.

## Codes left declared (not done in this block)

- DOCX016 (list headers): Word has no unnumbered list entry; `numId 0` with the list indent is the approximation; no
  per-paragraph carrier exists yet (a paragraph-keyed part would be needed).
- DOCX031 (cell number formats): `w:tcPr` has no extension point; same carrier problem.
- DOCX003 (section name, protection, hidden, condition, links): `w:formProt` (§17.6.6) protects only under forms
  protection; names and conditions need a carrier.
- DOCX034 (conditional hidden paragraph): needs a field approximation.
- DOCX001 (multi-paragraph deletions): needs paragraph-mark deletions in the revision writer (overlaps DOCX114, B7).
- DOCX002 (connectors, linked files, groups with charts): needs new DrawingML writers.

## Tests

- New `tests/PulseOffice.Formats.OpenXml.Tests/DocxStoredPageStyleTests.cs` (4): unused style with footer field,
  usage, next style and size through DOCX; untouched standard styles not stored; a stored style never replaces a
  written section's style; foreign/broken entries ignored.
- Updated: `DocxTenthAuditLinkTests`, `DocxThirteenthAuditTests` (DOCX171 no longer declared), `ObjectRoundTripTests`
  (wrap-first-paragraph now survives the DOCX route).
- Formats.OpenXml.Tests: 2224/2225; the failure is the timing test `Molti_smart_tag_si_sciolgono_in_tempo_lineare`,
  run while the corpus audit was loading the machine (no code touched there).

## Corpus

- `one` on a corpus file, `birt_min.odt`, a corpus file, `abi10076.odt`, a corpus file,
  a corpus file, `hidden-sections-with-pagestyles.odt`: no LOSS, no DOCX171.
- Full run: **LOSS 0, DECL 193** (was 0 / 349 on main with B1-B4b), 28 READFAIL, 14 NOTZIP.
