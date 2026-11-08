# Declared losses, block B6 (Ledger)

Codes: ODF081, XLSX180 (filters per range), ODF192 (linked pictures), ODF193, XLSX059 (grouped pictures and charts),
ODF080, XLSX081 (pivots on external sources). Analysis: [../declared-losses-analysis.md](../declared-losses-analysis.md).

## Files read

| File | Outcome |
|---|---|
| `src/PulseOffice.Formats.Odf/OdsReader.cs` (244) | ODF080/ODF081 raised after database ranges and pivots |
| `src/PulseOffice.Formats.Odf/OdfDatabaseRangeMapper.cs` | Only one filtered range per sheet; named ranges drop their filter |
| `src/PulseOffice.Formats.Odf/Spreadsheet/OdsLinkedPictures.cs`, `OdsGroupedObjects.cs` | Detectors of ODF192/ODF193 |
| `src/PulseOffice.Sheets/DatabaseRanges.cs` | No filter on a named range |
| `src/PulseOffice.Formats.OpenXml/XlsxReader.cs`, `Tables/XlsxTables.cs` | Tables read as database ranges; XLSX writer writes no tables (XLSX031) |
| `src/PulseOffice.Formats.OpenXml/XlsxWriter.cs`, `Spreadsheet/XlsxAutoFilterCodec.cs`, `Spreadsheet/SheetSections.cs` | No table parts written; filter codec bound to the sheet |
| `src/PulseOffice.Formats.OpenXml/XlsxDrawingMapper.cs`, `XlsxShapeMapper.cs`, `XlsxImageMapper.cs`, `Shapes/SheetShapes.cs` | Groups write and read only shapes, freeforms, connectors; pictures never linked |
| `src/PulseOffice.Formats.Odf/OdfShapeMapper.cs`, `OdfImageMapper.cs`, `Shapes/SheetImages.cs`, `Shapes/SheetShapeReading.cs`, `Shapes/SheetShapeWriting.cs` | Groups keep shapes only (ODF042, ODF193); external `xlink:href` skipped (ODF192, ODF023) |
| `src/PulseOffice.Sheets/Pivot/PivotDefinition.cs`, `src/PulseOffice.Formats.Odf/OdfPivotMapper.cs` (report) | No external source in the pivot model |
| `src/PulseOffice.Formats.Odf/OdsWriter.cs`, `OdsFormat.cs`, `src/PulseOffice.Formats.OpenXml/XlsxFormat.cs` | Fidelity checks updated (ODF042, XLSX059, XLSX031 narrowed; ODF023, XLSX027 removed) |
| tests: `XlsxThirteenthAuditTests.cs`, `OdsThirteenthAuditTests.cs`, `OdsExternalPivotTests.cs`, `XlsxOdsParityTests.cs` | Assertions on removed declarations rewritten as round-trip checks |

## Changes

- **Filters per range (ODF081, XLSX180).** `DatabaseRange` gains `ShowsFilterButtons` and `Filters`. ODS: the
  anonymous sheet range, then the first named range with buttons, become the sheet autofilter; every other named range
  keeps its buttons and `table:filter` conditions, and is written back with them. XLSX: the reader keeps the autofilter
  of every further table as a filtered database range (`XlsxTableFilters`); the writer now writes database ranges as
  Excel tables (`XlsxTablePlanner`, `XlsxTableWriter`): name valid for Excel and not a defined name, header cells
  distinct constant texts, no overlap with merges, other tables, query ranges or (unless equal) the sheet filter. A
  table equal to the sheet filter carries it, and the worksheet `autoFilter` is then omitted (Excel forbids both).
  XLSX180 removed; ODF081 narrowed to standard filters on pivot sources (the pivot model computes on the whole source,
  XLSX `worksheetSource` has no filter); XLSX031 narrowed to ranges that cannot become tables.
- **Linked pictures (ODF192, ODF023, XLSX027).** ODS reads an external `xlink:href` as a `RasterImageObject` with
  `LinkPath` and writes it back as a link; XLSX writes `a:blip r:link` with an external image relationship and reads it
  back. ODF023 and XLSX027 removed; ODF192 narrowed to pictures whose data is missing from the package (class (d)).
- **Pictures in groups (ODF193, ODF042, XLSX059).** ODS reads `draw:frame`/`draw:image` inside `draw:g`
  (`SheetGroupImageReader`) and writes them (`IOdfPictureStore`, implemented by `OdfImageMapper`); XLSX writes and
  reads `xdr:pic` inside `xdr:grpSp` (`XlsxGroupPictures`). Freeforms in XLSX groups were already written: the
  XLSX059 count no longer lists them. Codes narrowed to charts, embedded objects, vector pictures and controls.
- **External pivots (ODF080, XLSX081): not done, kept declared.** The pivot model (`PivotDefinition`) has only a sheet
  range or a query binding as source and the engine recomputes from it; keeping an external pivot needs a cached-record
  source and a refresh path in the pivot engine and UI. This is the "Large" item of the analysis and stays for a later
  block.

## Verification

Tests: Formats.OpenXml.Tests 2213/2213, Formats.Odf.Tests 767/767, Architecture.Tests 40/40. Corpus run: LOSS 0, DECL 236; remaining B6 rows ODF192 2, ODF193 2, XLSX031 3, ODF080 2, XLSX081 6.
