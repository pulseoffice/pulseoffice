# Declared losses block B5b: DOCX016, DOCX031, DOCX003, DOCX034, DOCX002

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/deep-audit/losses-b5.md | whole | codes left open; custom XML part pattern of `DocxStoredPageStyles` |
| docs/declared-losses-analysis.md | B5 rows, remedies | DOCX016/031/003/034/002 one to five rows each |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs | 1-140, 240-400 | rules; body and section writing |
| Formats.OpenXml/Wordprocessing/DocxStoredPageStyles.cs | 105/105 | custom XML part write/read, now shared through `DocxCustomXmlParts` |
| Formats.OpenXml/Wordprocessing/DocxFieldInstructions.cs | 1-180 | `w:fldData` marker, IF instruction, `WithNativeData` only for preserved fields |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 140-150, 260-310, 360-395 | sections rebuilt as `SectionBlock` only when columns change; composition root |
| Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 50-100, 180-200, 296-306, 380-390, 660-700 | hidden `_` bookmarks dropped unless referenced; field runs |
| Formats.OpenXml/Wordprocessing/DocxBlockWriter.cs | 1-140 | every paragraph goes through `Paragraph()` |
| Formats.OpenXml/Wordprocessing/DocxWriteContext.cs | whole | write and read contexts |
| Formats.OpenXml/Wordprocessing/DocxDrawingCodec.cs | 400-440, 595-610 | `Writes`/`Complete`: groups write only DrawingML members |
| Documents/Blocks/TableBlock.cs, SectionBlock.cs, Block.cs | whole | `CellNumberFormat`, section properties, container API |
| tools/corpus-audit/FeatureCounter.cs, FeatureRules.cs | 30-100, 220-230, 335-345 | list counting |

## Specification check

- Sections (DOCX003): WordprocessingML sections are only `w:sectPr` page layouts (ECMA-376 Part 1 §17.6.17) with no name,
  condition, hidden state, link or nesting. `w:formProt` (§17.6.6) protects a section only while the whole document is
  under forms protection (§17.15.1.29 `w:documentProtection w:edit="forms"`), so it cannot express a protected section
  inside an editable document. Bookmarks (§17.13.6.2 `w:bookmarkStart`, §17.13.6.1 `w:bookmarkEnd`) may span any range of
  paragraphs and survive editing in Word; names starting with `_` are hidden bookmarks (§17.13.6.2, `name` attribute).
- List headers (DOCX016): `w:numId w:val="0"` (§17.9.18) removes numbering; a level with `w:numFmt w:val="none"`
  (§17.9.17) still counts the entry and advances later numbers, so it is not a header. The list membership of an
  unnumbered entry has no native form.
- Cell number formats (DOCX031): `w:tcPr` (§17.4.42) has no number format and no extension list.
- Conditional hidden paragraphs (DOCX034): Word has `w:vanish` (§17.3.2.41) without a condition; the IF field
  (§17.16.5.28) evaluates a condition but cannot hide a paragraph. `w:fldData` (§17.16.12) holds application data for a
  field, already our carrier for ODF fields (B4).
- DOCX002: the only remaining corpus row (a corpus file) is a group of form controls; `wpg:wgp` members are shapes,
  groups, pictures and graphic frames (Word 2010 wordprocessingGroup schema), never content controls (`w:sdt`), and
  a control cannot be placed inside a DrawingML group. Kept declared.

## Design

- One carrier for block properties: `DocxBlockProperties` writes a custom XML part
  `<bp:blocks xmlns:bp="urn:pulseoffice:block-properties">` (through the new shared `DocxCustomXmlParts`, also used by
  `DocxStoredPageStyles` so the part creation is not duplicated). Each entry names a hidden bookmark
  `_PulseBlockN` placed around the block in the body: `bp:section` (name, protected, key, hidden, condition, link) spans
  from the first to the last paragraph of the section, `bp:listHeader` (list, level, original margin) and `bp:cell`
  (kind, decimals, grouping, symbol, long date) sit on the paragraph and on the cell's first paragraph. Word keeps the
  bookmarks while editing; the reader records the paragraphs that hold them (`DocxReadContext.CarrierStarts/Ends`), does
  not turn them into document bookmarks, and after sections and stored page styles applies the entries: sections wrap
  the range (or take the `SectionBlock` already rebuilt for column changes when it covers the same range), nested ones
  inside it in document order. At most 65536 entries; keys capped; foreign entries ignored.
- DOCX034: a hidden-paragraph field is written as `IF <condition> "" ""` (empty in Word) with a `w:fldData` holding the
  ODF element `text:hidden-paragraph` with condition and state; `DocxFieldInstructions.WithNativeData` recognises it on
  any field kind and gives back the field with its hidden state (`DocxField.Hidden`).
- Rules: DOCX016, DOCX031, DOCX034 removed (they had no localization keys); DOCX003 narrowed to sections beginning or
  ending with a table (no paragraph to hold the bookmark). `declared-features.json` entries removed.
- Tool: an ODF list counts only when one of its items with a paragraph lies outside an annotation (the list made of
  headers in `annotation-formatting.odt` counted the list items of a comment inside it).

## Tests

- New `tests/PulseOffice.Formats.OpenXml.Tests/DocxBlockPropertiesTests.cs` (4): nested sections with all properties
  through DOCX and then ODT; carrier bookmarks hidden from the model; hidden paragraph field with condition and state;
  currency cell format.
- Updated: `CellParityTests`, `DocxRoundTripTests`, `MasterDocumentParityTests`, `ToolParityTests`,
  `DocxFourteenthAuditTests` (round trips instead of declarations; DOCX003 still declared for a section starting with a
  table).
- Formats.OpenXml.Tests: 2243/2243.

## Corpus

- `one` on `annotation-formatting.odt`, `bullets.odt`, `no-numlevel-but-indented.odt`, `bad-split-section.odt`,
  `inline-endnote-and-section.odt`, `floattable-in-text-section.docx`, a corpus file: no LOSS.
- Full run: **LOSS 0, DECL 151**, 28 READFAIL, 14 NOTZIP. DOCX016/031/003/034 rows gone; one DOCX002 row left
  (a corpus file, grouped form controls).
