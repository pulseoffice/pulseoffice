# Declared losses block B4: unknown fields (DOCX123, ODF082)

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md | B4 sections | DOCX123 57 rows, ODF082 13 rows |
| Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 732/732 | unknown instructions dropped to plain result text (179, 421, 635) |
| Formats.OpenXml/Wordprocessing/DocxFieldInstructions.cs | 369/369 | `Parse` returns null for unknown keywords; `Instruction` writes known kinds only |
| Formats.OpenXml/Wordprocessing/DocxInlineWriter.cs | 180-260 | fields written as `w:fldSimple` per run |
| Formats.Odf/Odt/OdtVocabulary.cs | 304/304 | `_unhandledFields` list walked as plain text |
| Formats.Odf/Odt/OdtBodyReading.cs | 330-410 | field segments; unhandled branch at 401 |
| Formats.Odf/Odt/OdtBodyWriting.cs | 205-260 | `Segment` builds fields through `OdtFieldTable.Build` |
| Formats.Odf/Odt/OdtFoundation.cs | 1-40 | `Pt` = `urn:pulseoffice:text` |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs | 270-340 | no ignorable-namespace mechanism in DOCX parts: chose `w:fldData` as carrier |
| Documents/Fields/FieldSources.cs | 1-110 | kinds without a source keep the cached text |
| ODF 1.3 schema (rng) | placeholder, text-input, editing-duration, author-initials | `text:placeholder-type` is required |
| Formats.Odf/Odt/OdtWriter.cs, OdtReader.cs | composition lines | `OdtFieldTable` now receives `OdtPreservedFields` |
| tests: DocxRangeAndNoteReadingTests, OdtSenderFieldTests | affected cases | expectations moved from DOCX123/ODF082 to preserved fields / DOCX151 |
| tools/corpus-audit/FeatureRules.cs | 1-240 | ODF field list lacked print/editing/template fields; no rule for our span carrier |

## Specification check

- OOXML (ECMA-376 Part 1, §17.16): a field is either `w:fldSimple` (instruction in `w:instr`, result as child runs) or
  a complex field `w:fldChar begin` / `w:instrText` / `w:fldChar separate` / result / `w:fldChar end`. Both
  `w:fldSimple` and `w:fldChar` may carry an optional `w:fldData` child: application-defined field data, base64 text,
  kept by consumers without interpreting it. It is the carrier for an ODF element that has no Word equivalent, so no
  `mc:Ignorable` namespace has to be declared on every part (document, headers, footers, notes, comments).
- `QUOTE "text"` (§17.16.5, QUOTE) shows literal text: it is the instruction written for an ODF-only field, so Word
  shows the cached result and updating does not change it.
- `MACROBUTTON NoMacro text` shows the text of its instruction, not a result: Word's usual placeholder idiom.
- ODF 1.3 (§7, text fields; checked in `schema/odf1.3/OpenDocument-v1.3-schema.rng`): `text:print-date`,
  `text:editing-duration`, `text:editing-cycles`, `text:author-initials`, `text:author-name`, `text:template-name`,
  `text:text-input` contain text; `text:placeholder` requires `text:placeholder-type`. Our attribute
  `pt:field-instruction` (`urn:pulseoffice:text`) is a foreign attribute, which consumers ignore while keeping the
  element's content (ODF 1.3 Part 3 §3.17).

## Design

- Model: `TextFieldKind.Preserved`. `FieldArgument` holds the Word instruction verbatim, `FieldDetail.NativeOdf` the
  ODF element (attributes and non-text children, at most 64 KiB); the run text is the cached result. No field source
  evaluates it, so Folio shows the cached result.
- Shared table `src/PulseOffice.Formats/PreservedFieldTable.cs` (`IPreservedFieldTable`): Word keyword <-> ODF element
  for PRINTDATE/print-date, EDITTIME/editing-duration, REVNUM/editing-cycles, USERINITIALS/author-initials,
  USERNAME/author-name, TEMPLATE/template-name, FILLIN/text-input, MACROBUTTON/placeholder; anything else `QUOTE`.
- DOCX reader (`DocxFieldInstructions.Parse`): every non-structural instruction that is not a modelled field becomes
  `Preserved`; a `w:fldData` with our marker (`urn:pulseoffice:odf-field` + newline, then the XML) restores
  `NativeOdf` and drops the synthetic QUOTE instruction. An empty MACROBUTTON result shows the instruction's display
  text. A preserved field spanning paragraphs declares DOCX151, which already covers fields spanning paragraphs.
- DOCX writer: `w:fldSimple` with the instruction, plus `w:fldData` when `NativeOdf` is present.
- ODT reader (`src/PulseOffice.Formats.Odf/Odt/OdtPreservedFields.cs`): every formerly unhandled field element, and any
  element carrying `pt:field-instruction`, becomes `Preserved`; `text:span pt:field-instruction` carries a Word-only
  field.
- ODT writer: the native element (validated: text namespace, preserved list only, DTD prohibited, size cap) with the
  current result; otherwise the mapped ODF element; otherwise `text:span`. `pt:field-instruction` whenever a Word
  instruction is known.
- Removed: DOCX123 and ODF082 (nothing is lost any more). DOCX025, DOCX008 and DOCX195 (writer side, listed beside B4
  in the work order) are not touched by this block.
- Known limit, not new: a field whose result has several differently formatted runs is written as one `w:fldSimple`
  per run, as for every Folio field.

## Tests

- New `tests/PulseOffice.Formats.OpenXml.Tests/DocxPreservedFieldTests.cs` (7): DOCX round trip of PRINTDATE with
  switches; USERINITIALS -> `text:author-initials` -> same instruction; ADDRESSBLOCK through the span carrier;
  MACROBUTTON display text -> `text:placeholder`; `text:drop-down` with labels through DOCX (QUOTE + fldData) back to
  the identical element; placeholder -> MACROBUTTON NoMacro -> placeholder keeping its description; foreign `fldData`
  (a `draw:frame`) is not injected into ODT.
- Updated: `DocxRangeAndNoteReadingTests` (SECTIONPAGES kept as a preserved field; field across paragraphs -> DOCX151),
  `OdtSenderFieldTests` (template-name kept).
- Runs: Formats.OpenXml.Tests 2195/2195, Formats.Odf.Tests 767/767.

## Corpus tool

- `FeatureRules`: `urn:pulseoffice:text` mapped to `pt`; `text:span` with `pt:field-instruction` counts as a field;
  the ODF field list gains the print, editing, template, time, macro, input, page-variable and database fields that
  were missing (they were never counted, so their loss could not be seen).
