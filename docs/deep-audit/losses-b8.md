# Declared losses block B8: spanning and nested fields, mid-page section headers, tables in groups, 3D scenes

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md | B7/B8 rows, work order | DOCX151 10 rows, DOCX160 2, DOCX161 0, DOCX125 4, DOCX181 4, DOCX124 2, ODP102 4 |
| docs/deep-audit/losses-b4.md, losses-b4b.md | whole | `Preserved` fields, `w:fldData`, `ComplexField`, `DocxDeletedFieldState` reused |
| Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 1-763/763 | field state is per paragraph: a field left open at the paragraph end is dropped (DOCX151), nested fields in results are flattened (DOCX161) |
| Documents/Marks/InlineMark.cs | 98/98 | range marks are generic records keyed by id |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 225-300 | DOCX125 raised when a continuous section changes headers without a page break |
| Formats.OpenXml/Wordprocessing/DocxInlineWriter.cs | 180-398 | marks written per edge; `ComplexField` writes begin/instr/separate/result/end |
| Formats.Odf/Odt/OdtBodyWriting.cs | 255-310 | `OdtMarkWriter` writes marks as ODF elements |
| corpus documents (5 DOCX151 files) | document.xml | IF fields whose instruction or result crosses paragraphs and a table; nested QUOTE in a QUOTE instruction across paragraphs |
| Formats.OpenXml/Wordprocessing/DocxFieldInstructions.cs | 1-130, 280-340 | `Parse`, `Structural`, `HyperlinkTarget`, nested placeholders |
| Formats.OpenXml/Wordprocessing/DocxWriteContext.cs | 1-175 | write and read contexts gain field-range state |
| Formats.Odf/Odt/OdtBodyReading.cs | 320-360 | inline element dispatch; `Pt` namespace available |
| Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs | 230-275, 400-560, 650-730 | groups with tables were unpacked (`Unpacks`/`Unpacked`); member stories already hold any block |
| Documents/Objects/GroupStories.cs | whole | member shape + story; tables fit |
| Formats.Odf/Odt/OdtDrawings.cs | 150-300 | group members read `draw:frame`/`draw:text-box` already |
| Formats.Odf/Odt/OdtBodyWriting.cs | 440-600 | member stories written inside `draw:custom-shape`, where ODF does not allow tables |
| Documents/Marks/MarkOrder.cs, Clipboard/FragmentExtractor.cs | whole / 75-95 | marks at one offset keep insertion order; copies keep a range only with both edges |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 240-300, 400-440 | DOCX125 logic, composition |
| Formats.Odf/OdpReader.cs | 195-215 | ODP102 raised for any `dr3d:scene` |
| tools/corpus-audit/FeatureRules.cs | 225-240, 275-285 | ODF field rules; DOCX fields counted by `w:fldChar begin` |

## Specification check

- Complex fields (ECMA-376 Part 1 §17.16.18, `w:fldChar` §17.16.17): begin, instruction (`w:instrText`, §17.16.23),
  optional separate, result, end. The characters may sit in different paragraphs and around tables: the paragraph
  marks and tables between begin and end belong to the field. Fields nest freely in the instruction and in the
  result. `w:fldData` (§17.16.17) and `w:fldLock` belong to the begin character.
- ODF 1.3 has no field whose result spans paragraphs nor nested fields (§7). Foreign elements in a paragraph are
  allowed and ignored by other consumers, which keep their content (ODF 1.3 Part 3 §3.17): an empty
  `pt:field-code` element (`urn:pulseoffice:text`) loses nothing for them, since the result is ordinary text.
- Groups (`wpg:wgp`, [MS-DOCX] 2.6) hold `wps:wsp` whose `wps:txbx/w:txbxContent` (§17.17.1.1) may contain tables.
  ODF: `draw:g` (§10.3.15) may contain `draw:frame`, whose `draw:text-box` (§10.4.4) may contain `table:table`;
  `draw:custom-shape` (§10.6.1) and the other shapes allow only `text:p`/`text:list`, so a table cannot sit in a
  non-rectangular ODF shape: a non-rectangular shape holding a table remains DOCX124.
- Continuous sections (§17.6.22, `w:type continuous`): the new section's headers apply from the next page. ODF has
  no "change master page at the next page" on a paragraph; `style:master-page-name` breaks the page (§16.2).
- `dr3d:scene` (ODF §10.5) has no PresentationML twin; DrawingML `a:scene3d`/`a:sp3d` only extrude 2D shapes.

## Design

### Spanning and nested fields (DOCX151, DOCX161, part of DOCX160)

- Model: `FieldCodeMark` (Documents/Marks/InlineMark.cs) with `FieldCodePart` Begin / Code / Separate / End, a field
  id, the instruction text that lies in that paragraph (`Code`), and on Begin the `w:fldData` and lock. The result is
  ordinary content between Separate and End, in any number of paragraphs and tables. Nested fields inside the
  instruction are kept in `Code` with three private-use characters (U+E000 begin, U+E001 separate, U+E002 end), their
  cached results included. Nested fields inside the result are ordinary Folio fields (or ranges) of their own.
- `DocxFieldRangeScan` decides, once per part root, which complex fields become ranges: fields whose instruction
  crosses a paragraph, whose result holds another field, or whose result crosses a paragraph while the field is not a
  modelled kind with result text in its first paragraph (those keep the old behaviour: live field in the first
  paragraph, rest of the result as text; no loss, Word updates it). Excluded: form fields, TOC/INDEX and other
  structural fields, HYPERLINK, XE, fields inside `w:del`/`w:moveFrom` and inside `mc:Fallback`.
- `DocxFieldRangeReader` follows the range across paragraphs through `DocxReadContext.FieldRanges` (a stack, so
  ranges nest in results); `DocxFieldCodeWriter` writes `w:fldChar`/`w:instrText` back paragraph by paragraph, with
  nested fields expanded, and writes nothing for a range whose begin or end was removed by editing (it degrades to its
  result text instead of corrupting the document).
- ODT: `<pt:field-code pt:field-id pt:part pt:code pt:data pt:locked/>`, read back to the same marks.
- Codes: DOCX151 narrowed to form, hyperlink, index-entry and contents fields; DOCX161 narrowed to form and hyperlink
  fields; DOCX160 unchanged outside ranges (left: legacy form fields with nested fields, single-paragraph instructions
  with deeper nesting).

### Tables in grouped text boxes (DOCX181, DOCX124)

- The DOCX reader no longer unpacks groups whose members hold tables: the member story keeps its tables
  (`GroupStories.Member`). DOCX writes the member `wps:txbx` with the tables. ODT writes a member whose story holds a
  table as `draw:frame` + `draw:text-box` inside the `draw:g`, with the member's graphic style; the ODT reader takes
  the frame's style back as the member's fill and line. DOCX181 removed (with `Unpacks`/`Unpacked`).
- DOCX124 narrowed to shapes other than rectangles holding tables: ODF cannot put a table in such a shape.

### Not done in this block

- DOCX125 (headers of a continuous section starting mid-page): needs a "page style from the next page" paragraph
  property, the layout to honour it, the DOCX section writer to emit a continuous `w:sectPr` for it and an ODT carrier.
  Left declared.
- ODP102 (3D scenes): needs an opaque preserved-object type in Deck (ODP keeps the `dr3d:scene` XML; PPTX a fallback
  picture, which needs a 3D renderer, and the XML in an ignorable extension) plus animation targets. Left declared.

## Corpus

- Full run after B8: LOSS 0, DECL 145; DOCX151, DOCX181 and DOCX124 have no rows; left DOCX125 4, ODP102 4, DOCX160 2.
- Tests: Formats.OpenXml.Tests 2238/2238 (new DocxFieldRangeTests 7), Formats.Odf.Tests 770/770, Architecture.Tests 40/40.
