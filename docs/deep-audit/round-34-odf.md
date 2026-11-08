# Deep audit round 34: ODF side of the declared-loss blocks

Scope: the ODF code changed by blocks B1-B11 (`git show` of 1ee9963, 07982bb, 6340025, 418a762, 9227b7f, f301949,
d2b3b7d, 189a8c9 on `src/PulseOffice.Formats.Odf`). Rounds 32/32b already covered the field-code carriers on both sides.

## Coverage

| Area | Read | Result |
|---|---|---|
| Formats/LinkedFiles.cs | full | defect R1 |
| Spreadsheet/OdfLinkedPicture.cs, OdsLinkedPictures.cs | full | clean (never fetched; only file/http/https/relative hrefs) |
| Spreadsheet/OdfGroupPictures.cs, OdsGroupedObjects.cs | full | defect R2 |
| OdfImageMapper.cs (linked and preferred pictures) | changed parts | clean |
| OdfDatabaseRangeMapper.cs | full | clean |
| OdsReader.cs / OdsWriter.cs (B6 diff) | changed parts | clean |
| Odt/OdtStylesReading.cs, OdtStylesWriting.cs (hidden variants, picture bullets) | changed parts | clean (explicit `pt:` flags keep the switches when every variant is hidden) |
| Odt/OdtBodyReading.cs, OdtBodyWriting.cs (field-code, change-id rows, grouped text-box tables) | changed parts | clean |
| Presentation/OdfMasterPages.cs, OdpReader.cs (master text boxes) | changed parts | clean |
| OdfDimensionLines.cs, Shapes/SheetPrimitiveShapeReader.cs | changed parts | defect R3 |

## Defects

- **R1 (medium, security/leak)** `LinkedFiles.ToPath` turned any absolute URI into a linked-picture path. An XLSX or PPTX
  external image relationship to `javascript:`, `data:`, `vbscript:` or any custom protocol became `LinkPath`, and the
  ODF writer then wrote it out as `xlink:href` (the ODF readers already refused it, so ODS reread it as a missing
  picture). Fix: only `file:`, `http:`, `https:`, rooted and relative paths become linked paths; other schemes give
  no link. Test: `LinkedFileSchemeTests` (Formats.OpenXml.Tests).
- **R2 (minor, silent loss)** `SheetGroupImageReader` recognised a vector picture inside a group only by the `.svg`
  extension: an SVG stored under another name with `draw:mime-type="image/svg+xml"` became a raster object holding SVG
  bytes, and `ODF193` was not declared. Fix: shared `OdfVectorPictures.Declares` (extension or media type) used by the
  reader and by the loss check. Test: `OdsRound34GroupPictureTests`.
- **R3 (minor, interoperability)** a multi-line dimension-line label was written as one `text:p` holding a newline,
  which ODF whitespace rules collapse to a space in other readers. Fix: one `text:p` per line (the reader already joins
  paragraphs with a newline). Test: `OdtDimensionLineTests.A_two_line_label_is_written_as_two_paragraphs`.

## Remaining notes

- Linked hrefs starting with `../` are also tried as package entries after trimming dots and slashes (existing
  behaviour shared by all ODF picture readers): a file linking `../Pictures/x.png` shows the embedded picture of the
  same name if the package has one. Not changed.
