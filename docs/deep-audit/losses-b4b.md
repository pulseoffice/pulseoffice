# Declared losses block B4b: DOCX025, DOCX008, DOCX195

Complement of block B4 (see [losses-b4.md](losses-b4.md)).

## Coverage log

| File | Lines read | Outcome |
|---|---|---|
| docs/declared-losses-analysis.md | B4 rows and remedies | DOCX025 3 rows, DOCX008 1, DOCX195 1 |
| docs/deep-audit/losses-b4.md | whole | `w:fldData` carrier reused |
| Formats.OpenXml/Wordprocessing/DocxWriter.cs | 1-260/438 | rules for the three codes |
| Formats.OpenXml/Wordprocessing/DocxInlineWriter.cs | 1-372/372 | XE written as point fields; fields in deletion ranges as `w:fldSimple` inside `w:del` (schema-invalid) |
| Formats.OpenXml/Wordprocessing/DocxRevisionWriter.cs | 1-150/150 | point deletions write field runs as plain deleted text |
| Formats.OpenXml/Wordprocessing/DocxInlineReader.cs | 1-732/732 | XE read as point entries; deleted runs ignore `w:fldChar`/`w:delInstrText` |
| Formats.OpenXml/Wordprocessing/DocxIndexes.cs | 1-210/210 | TOC/INDEX instruction and scan |
| Formats.OpenXml/Wordprocessing/DocxBookmarkReferences.cs | whole | hidden bookmarks kept only when referenced |
| Formats.OpenXml/Wordprocessing/DocxReader.cs | 1-431/431 | DOCX117 raised on any `w:delInstrText` |
| Documents/Indexes/IndexDefinition.cs, Marks/InlineMark.cs | whole / 75-100 | model of indexes and entries |
| Formats.OpenXml/Wordprocessing/DocxBlockReader.cs | 40-145 | index opening from `Scan` |
| Formats.OpenXml/Wordprocessing/DocxWriteContext.cs | whole | bookmark id table |
| Formats.Odf/Odt/OdtBodyReading.cs, OdtBodyWriting.cs | index-mark and field sections | ODT already keeps range entries, index settings and deleted fields |
| corpus a corpus file | deleted regions | the only remaining deleted fields are `text:hidden-paragraph` and `text:hidden-text` |

## Specification check

- DOCX008: `XE` has the switch `\r bookmark` (ECMA-376 Part 1 §17.16.5.79): the entry's page range is the bookmark's
  extent. ODF: `text:alphabetical-index-mark-start`/`-end` (§7.11.3/7.11.4). Both formats represent a range.
- DOCX195: deleted field codes are `w:delInstrText` (§17.16.13) inside a complex field (`w:fldChar`) within `w:del`
  (§17.13.5.14). `w:del` holds runs only (EG_ContentRunContent), so a `w:fldSimple` inside it was schema-invalid: the
  old range-deletion path wrote exactly that. Fields Word has no instruction for (conditional hidden text and
  paragraphs, counters, sender fields) still have no representation in Word, deleted or not.
- DOCX025: TOC (§17.16.5.68) and INDEX (§17.16.5.35) switches cover levels 1-9, hyperlinks, page numbers, letter
  headings, captions with or without label; there is no switch for case sensitivity, combined entries, outline-free
  contents, more than nine levels, category-and-number captions, user-index sources or bibliography order. The
  application-defined `w:fldData` of the begin `w:fldChar` (§17.16.17), already the B4 carrier, holds them.

## Design

- DOCX008: `IndexEntryMark` Start writes a hidden bookmark `_IdxN` plus `XE "path" \r _IdxN`; End closes the bookmark
  (`DocxWriteContext.OpenIndexRange`/`CloseIndexRange`, paired per entry key). Reading: `DocxBookmarkReferences.IndexRanges`
  keeps the bookmarks named by `XE \r`; the inline reader records each ranged XE; the new `DocxIndexRanges` post-pass turns
  the bookmark's start/end into the entry's start/end and drops the point entry; the bookmark itself stays only when a
  REF/PAGEREF/NOTEREF also points at it. Code DOCX008 removed.
- DOCX195: `DocxInlineWriter.ComplexField` writes a field as `w:fldChar` begin (with `w:fldData` for preserved fields) /
  `w:instrText` or `w:delInstrText` / separate / result / end, used by point deletions (`DocxRevisionWriter`) and by
  deletion ranges (replacing the invalid `w:fldSimple`). Reading: `DocxDeletedFieldState` follows `w:fldChar` and
  `w:delInstrText` inside `w:del`, so the deleted run comes back as the field; DOCX117 is no longer raised for a whole
  field inside one `w:del`. DOCX195 narrowed to fields without a Word instruction.
- DOCX025: `DocxIndexSettings` encodes the whole `IndexDefinition` (marker `urn:pulseoffice:index`, key=value lines,
  base64, 4 KiB cap) into `w:fldData` when the instruction alone would not give it back; `DocxIndexCodec.Scan` returns
  the data and `Parse(instruction, data)` restores it; foreign data is ignored. Word keeps the field data and applies its
  own switches when it updates the index. Code DOCX025 removed.
- `tools/corpus-audit/declared-features.json`: entries DOCX008, DOCX025 and the stale DOCX123, ODF082 removed. No
  `diagnostic.<code>` localization keys existed for these codes.

## Tests

- New `tests/PulseOffice.Formats.OpenXml.Tests/DocxIndexRangeAndDeletedFieldTests.cs` (11): range entry across two
  paragraphs through DOCX; Word XE `\r` with a visible bookmark also referenced by PAGEREF; deleted AUTHOR field with
  `w:delInstrText`, no DOCX117/DOCX195; deleted preserved PRINTDATE keeps instruction; paragraph count in a deletion
  still declares DOCX195; five index settings sets through DOCX; foreign `w:fldData` ignored.
- Updated: `ReferenceRoundTripTests` (DOCX025 no longer declared).
- Runs: Formats.OpenXml.Tests 2206/2206 (after fixing one test expectation for the XML-escaped quote).

## Corpus

- `one <corpus file>`: odt->docx still DECL DOCX195 (deleted hidden paragraph and hidden text).
- Full run: LOSS 0, DECL 349 (unchanged baseline), 28 READFAIL, 14 NOTZIP.
