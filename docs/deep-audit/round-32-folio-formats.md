# Deep audit round 32: word-processing format carriers (declared-loss blocks B1-B11)

Scope: the DOCX/ODT carriers added by the declared-loss blocks (field code ranges, paragraph mark joins and splits,
tracked rows, block-property bookmarks, custom XML parts, stored page styles, deleted field state, preserved fields,
VML paths).

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Formats.OpenXml/Drawing/VmlPathParser.cs | 248/248 | defect V1 |
| Formats.OpenXml/Wordprocessing/DocxFieldRanges.cs | 284/284 | defect F1 |
| Formats.Odf/Odt/OdtPreservedFields.cs | 90/90 | clean (hardened parser, size cap) |
| Formats.OpenXml/Wordprocessing/DocxBlockProperties.cs | 276/276 | clean (count and key caps) |
| Formats.OpenXml/Wordprocessing/DocxCustomXmlParts.cs | 39/39 | clean (safe loader) |
| Formats.OpenXml/Wordprocessing/DocxStoredPageStyles.cs | 87/87 | clean |
| Documents/Revisions/TrackedRows.cs | 66/66 | clean (one transaction in RevisionResolver) |
| Formats.OpenXml/Wordprocessing/DocxDeletedFieldState.cs | 81/81 | clean |
| Formats.OpenXml/Wordprocessing/DocxParagraphMarkJoins.cs | 87/87 | clean |
| Formats.OpenXml/Wordprocessing/DocxParagraphMarkSplits.cs | 102/102 | clean |
| Formats.OpenXml/Wordprocessing/DocxRowChanges.cs | 44/44 | clean |

## Defects

- **F1 (grave)** `DocxFieldRanges.cs`: the nesting markers U+E000-U+E002 of `FieldCodeMark.Code` were trusted on write.
  A literal U+E000 in `w:instrText` (or in `pt:code` of an ODT) was kept as code and written back as a nested
  `w:fldChar begin` that was never ended; the outer field's `end` then closed the nested field, leaving the outer
  field open (corrupt field structure). Fix: the reader drops literal marker characters from code text, and the
  writer closes every nested field still open before the outer separator or end.
- **V1 (medium)** `VmlPathParser.cs`: finite but huge coordinates (e.g. `1e308`) overflowed into infinite or NaN
  normalised points stored in the model. Fix: a path with a non-finite normalised point is rejected.
