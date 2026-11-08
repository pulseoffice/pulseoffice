# Deep audit: sheet model (PulseOffice.Sheets and satellites)

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| src/PulseOffice.Sheets/MergedRegions.cs | 80/80 | restructured: index by row bands |
| src/PulseOffice.Sheets/Worksheet.cs | 260/260 | added `Revision` |
| src/PulseOffice.Sheets/SheetLayout.cs | 328/328 | serious defect left open (see below) |
| src/PulseOffice.Sheets/Workbook.cs | 215/215 | minor noted |
| src/PulseOffice.Sheets/Addressing/CellRange.cs | 56/56 | ok |
| src/PulseOffice.Sheets/Addressing/CellAddress.cs | 78/78 | minor noted |
| src/PulseOffice.Sheets/Addressing/SelectionWorkArea.cs | 77/77 | medium noted |
| src/PulseOffice.Sheets/Addressing/SheetQualifiedReference.cs | 33/33 | ok |
| src/PulseOffice.Sheets/CellRowIndex.cs | 68/68 | ok |
| src/PulseOffice.Sheets/SheetEditGuard.cs | 47/47 | ok |
| src/PulseOffice.Sheets/DatabaseRanges.cs | 66/66 | fixed (culture) |
| src/PulseOffice.Sheets/QueryBinding.cs | 67/67 | ok |
| src/PulseOffice.Sheets/ExternalData.cs | 151/151 | fixed (extent, culture) |
| src/PulseOffice.Sheets/SheetGrouping.cs | 108/108 | ok |
| src/PulseOffice.Sheets/SheetPlacement.cs | 110/110 | ok |
| src/PulseOffice.Sheets/SeriesFill.cs | 150/150 | fixed |
| src/PulseOffice.Sheets/SheetRandomFill.cs | 105/105 | fixed |
| src/PulseOffice.Sheets/Filtering.cs | 82/82 | ok |
| src/PulseOffice.Sheets/Validation.cs | 78/78 | ok |
| src/PulseOffice.Sheets/Redaction.cs | 71/71 | fixed |
| src/PulseOffice.Sheets/SheetObjectExtent.cs | 67/67 | ok |
| src/PulseOffice.Sheets/PageStyles.cs | 92/92 | fixed (culture) |
| src/PulseOffice.Sheets/NamedStyles.cs | 176/176 | fixed |
| src/PulseOffice.Sheets/Sparklines.cs | 97/97 | ok |
| src/PulseOffice.Sheets/ObjectOrdering.cs | 93/93 | ok |
| src/PulseOffice.Sheets/ObjectAlignment.cs | 126/126 | ok |
| src/PulseOffice.Sheets/SheetConnectors.cs | 117/117 | ok |
| src/PulseOffice.Sheets/TrackedChanges.cs | 121/121 | medium noted |
| src/PulseOffice.Sheets/WorkbookComparison.cs | 119/119 | ok |
| src/PulseOffice.Sheets/PasswordKeys.cs | 147/147 | ok |
| src/PulseOffice.Sheets/FormControlBinding.cs | 150/150 | fixed |
| src/PulseOffice.Sheets/Values/CellValue.cs | 104/104 | ok |
| src/PulseOffice.Sheets/Pivot/PivotDefinition.cs | 86/86 | ok |
| src/PulseOffice.Sheets/PlacedObject.cs | 25/25 | ok |
| src/PulseOffice.Sheets/CalculationSettings.cs | 27/27 | fixed |
| src/PulseOffice.Sheets/WorkbookProtection.cs | 27/27 | ok |
| src/PulseOffice.Sheets.Pivot/PivotEngine.cs | 324/324 | restructured |
| src/PulseOffice.Sheets.Pivot/PivotSource.cs | 59/59 | fixed; minor noted |
| src/PulseOffice.Sheets.Pivot/PivotGrouping.cs | 85/85 | fixed |
| src/PulseOffice.Sheets.Pivot/PivotAggregator.cs | 81/81 | ok |
| src/PulseOffice.Sheets.Pivot/PivotCoordinator.cs | 99/99 | fixed (PVT006) |
| src/PulseOffice.Sheets.Pivot/PivotRenderer.cs | 141/141 | ok |
| src/PulseOffice.Sheets.Pivot/PivotTable.cs | 35/35 | ok |
| src/PulseOffice.Sheets.Pivot/PivotFormulas.cs | 213/213 | fixed (culture) |
| src/PulseOffice.Sheets.Pivot/PivotDataLookup.cs | 195/195 | fixed (cache) |
| src/PulseOffice.Sheets.Printing/SheetPrintDocument.cs | 542/542 | fixed; minor noted |
| src/PulseOffice.Sheets.Printing/SheetBands.cs | 33/33 | ok |
| src/PulseOffice.Sheets.Printing/ConditionalCellPainter.cs | 70/70 | ok |
| src/PulseOffice.Sheets.Printing/SheetObjectImages.cs | 15/15 | ok |
| src/PulseOffice.Sheets.Queries/QueryRefresher.cs | 189/189 | fixed; medium noted |
| src/PulseOffice.Sheets/Formatting/NumberFormatParser.cs | 337/337 | ok |
| src/PulseOffice.Sheets/Formatting/SectionRenderers.cs | 222/222 | fixed |
| src/PulseOffice.Sheets/Formatting/NumberFormatter.cs | 79/79 | fixed |
| src/PulseOffice.Sheets/Formatting/DateTimeTokens.cs | 278/278 | minor noted |
| src/PulseOffice.Sheets/Formatting/NumberFormat.cs | 80/80 | ok |
| src/PulseOffice.Sheets/Formatting/NumberFormatCatalog.cs | 103/103 | ok |
| src/PulseOffice.Sheets/Formatting/ConditionalRule.cs | 200/200 | fixed |
| src/PulseOffice.Sheets/Formatting/RowAutoHeight.cs | 281/281 | medium noted |
| src/PulseOffice.Sheets/Formatting/NumberFormatDesign.cs | 218/218 | ok |
| src/PulseOffice.Sheets/Formatting/FormatCodeVocabulary.cs | 184/184 | fixed |
| src/PulseOffice.Sheets/Formatting/FormatHints.cs | 88/88 | ok |
| src/PulseOffice.Sheets/Formatting/ColumnAutoWidth.cs | 56/56 | ok |
| src/PulseOffice.Sheets/Formatting/SystemDateCode.cs | 58/58 | ok |
| src/PulseOffice.Sheets/Styling/CellStyle.cs | 83/83 | ok |
| src/PulseOffice.Sheets/Styling/BorderPresets.cs | 120/120 | ok |
| src/PulseOffice.Sheets/Styling/CellBorderFrames.cs | 85/85 | ok |
| src/PulseOffice.Sheets/Styling/CellStyleDelta.cs | 60/60 | ok |
| src/PulseOffice.Sheets/Styling/SheetThemes.cs | 57/57 | fixed |
| src/PulseOffice.Sheets/Styling/StyleCatalog.cs | 33/33 | ok |
| src/PulseOffice.Sheets/Styling/StyleTransplant.cs | 29/29 | ok |
| src/PulseOffice.Sheets/SheetLabels.cs | 53/53 | ok |
| src/PulseOffice.Sheets/WorkbookFontNames.cs | 28/28 | ok |
| src/PulseOffice.Sheets/CellComment.cs | 32/32 | ok |
| src/PulseOffice.Sheets/Scenario.cs | 32/32 | ok |
| src/PulseOffice.Sheets/FrozenPanes.cs | 8/8 | ok |
| src/PulseOffice.Sheets/TraceArrow.cs | 10/10 | ok |
| src/PulseOffice.Sheets/Values/ExternalCellValues.cs | 6/6 | ok |
| src/PulseOffice.Sheets/Formatting/BooleanNames.cs | 26/26 | ok |
| src/PulseOffice.Sheets/Formatting/CellPresenter.cs | 23/23 | ok |
| src/PulseOffice.Sheets/Formatting/ConditionalAppearanceSource.cs | 13/13 | ok |
| src/PulseOffice.Sheets/Formatting/SpreadsheetLocale.cs | 20/20 | ok |
| src/PulseOffice.Sheets/OccupiedIndices.cs | 34/34 | new (this round) |

All files of src/PulseOffice.Sheets, Sheets.Pivot, Sheets.Printing and Sheets.Queries have been read in full.

## Defects

### Serious
- **Linear MergedRegions** (MergedRegions.cs:13, 26, 40): `At` and `Add` O(n), reading n merges O(n²), and
  `OdsWriter` calls `Merges.At` per cell. **Fixed**: index by bands of 64 rows (`_bands`), `Overlapping`
  and `Expand` go through the index; `Changed` event. Test `MergedRegionsIndexTests` (20,000 merges, merge over
  whole columns down to row 1,048,576, overlaps).
- **SheetLayout: one entry for every hidden row or column** (SheetLayout.cs:110-119, 93-100): hiding whole columns or
  rows creates up to a million zero entries in `_rowHeights`, and `RowOffset`/`ColumnOffset` (296-307) walk
  all entries on every call (O(n) per offset, O(n²) in drawing). **Not fixed**: the representation «hidden =
  height 0 in the dictionary» is read directly by OdsWriter (255-412), OOXML SheetSections (202-298), SheetSnapshot
  (Engine, 55-95), SheetStructure, LedgerFinalCommands, the resize gestures. The right restructuring is a
  set of hidden intervals separate from the sizes, with `HideRows(first,last)` and prefix sums for the offsets,
  and moving all those readers to an interval API: it touches five projects, postponed to a dedicated branch.

### Serious (continued)
- **Cycle of parent styles** (NamedStyles.cs:69-78): `StyleOf` recursed without limit; a file with A→B→A (or A→A)
  sent the process into a stack overflow, which cannot be caught. **Fixed**: iterative chain with a visited set.
  Test `NamedStyleCycleTests` (previously the test process hung).
- **Workday series with a huge step** (SeriesFill.cs:130-142): one step per day, up to billions of
  iterations (interface frozen). **Fixed**: whole weeks in one go, the remainder step by step.

### Serious (pivot, printing, queries)
- **Cubic pivot** (PivotEngine.cs:54-77, 132-155): for every cell (rows × columns) all source rows were walked,
  and every axis level re-filtered all paths: with 20,000 distinct items, hours. **Restructured**: buckets
  (row node, column node) filled in one pass with the path prefixes (`PathComparer`, `PathPrefix`), axis
  built by groups. Test `ManyDistinctItemsBuildInLinearTime` (20,000 rows under 5 s).
- **Pivot beyond the edge of the sheet** (PivotCoordinator.cs:35): a table running past the sheet's rows or columns
  wrote cells with invalid addresses. **Fixed**: rejected with `PVT006`, translated into the five languages.
- **Query result beyond the edge** (QueryRefresher.cs:109-139): same off-sheet cells. **Fixed**: rows and
  columns cut to the sheet, warning `QRY005`. Test `ResultAtTheBottomOfTheSheetIsTruncatedWithAWarning`.

### Medium
- **Print area of whole columns or rows** (SheetPrintDocument.cs:108): a range `A:B` paginated a million
  rows (tens of thousands of empty pages), and the area was recomputed (UsedRange O(n)) several times per page.
  **Fixed**: area reduced to the used part, computed once. Test `WholeColumnPrintArea*`.
- **Pivot on whole columns** (PivotSource.cs:34): a million empty rows and a giant «empty» item. **Fixed**:
  rows limited to the last used row.
- **Date grouping of out-of-range numbers** (PivotGrouping.cs:22): `AddDays` threw. **Fixed**.
- **GETPIVOTDATA rebuilt the pivot on every call** (PivotDataLookup.cs:33). **Fixed**: cache per definition and
  `Worksheet.Revision` of the source sheet. Test `RepeatedLookupsReuseTheTableUntilTheSourceChanges`.
- **Pivot calculated fields with the current culture** (PivotFormulas.cs:106,114). **Fixed**: ordinal.
- **Query: `DateOnly`, `TimeOnly`, `TimeSpan`** (QueryRefresher.cs:167) became text according to the culture (with the
  date format applied). **Fixed**: serial numbers.
- **«General»/«Standard» translated as date codes** (FormatCodeVocabulary.cs:149): in Italian, German, Spanish and
  French the letters of the keyword went through the placeholder table (`General` → `Deneryl` in Italian),
  so the standard format typed in the local language became a date format. **Fixed**: keywords
  protected like AM/PM. Test `FormatCodeKeywordTests` (failed in four languages).
- **Dates beyond 9999 and non-finite values in formats** (SectionRenderers.cs:204, NumberFormatter.cs:67): a number
  like 1e12 with a date format made `AddDays` throw during drawing; infinity and NaN came out as «∞»/«NaN».
  **Fixed**: `#####` and `#NUM!`. Test `ValuesOutsideTheFormatRangeDoNotThrow`.
- **RowAutoHeightTracker.Signature** (RowAutoHeight.cs:266-279) walks all row heights on every `Flush`: with
  millions of hidden rows (see SheetLayout) it is O(n) for every change. **Not fixed**: follows the restructuring of
  SheetLayout (layout `Revision` instead of the signature).
- **Query: `RowShift` ignored** (QueryRefresher.cs:109): `InsertDelete` and `Clip` are not implemented, it always
  overwrites. **Not fixed**: noted.
- **PivotValues.KeyOf writes «VERO»/«FALSO»** (PivotSource.cs:55) in every language: the keys are also the printed
  labels and are saved in the files' filters. **Not fixed**: changing them requires a migration of the excluded values in
  files; noted.
- **Month/year series beyond 9999** (SeriesFill.cs:149): `AddMonths` threw `ArgumentOutOfRange`. **Fixed**:
  range check, the series stops. Step × index in `long`, bounded.
- **Random integers with inverted or huge bounds** (SheetRandomFill.cs:77): `Random.Next` threw. **Fixed**.
- **Calculation iterations without a cap** (CalculationSettings.cs:14): a file with a huge `table:steps` or `iterateCount`
  froze the recalculation of circular references. **Fixed**: cap 32,767 (Excel's), minimum 1.
- **Redaction and form control entries over whole columns** (Redaction.cs:29, FormControlBinding.cs:44):
  walked every cell of the range (millions). **Fixed**: only non-empty cells.
- **ChangeLog without sheet reindexing** (TrackedChanges.cs:12): `SheetIndex` of the recorded changes does not
  follow insertion, move and removal of sheets (`Workbook.ShiftIndexed` touches only external data and ranges),
  so after a move the changes show on the wrong sheet; `At` is linear per cell. **Not fixed**:
  noted for the coordinator (it touches the ODF/OOXML readers of the changes).
- **AreaLink.Area without a cap** (ExternalData.cs:27): `Rows`/`Columns` read from the file added to the anchor overflowed
  (int.MaxValue → negative address, flipped area) or left the sheet. **Fixed**: sum in `long` and cap at
  `MaxRows-1`/`MaxColumns-1`. Test `ModelIdentityCultureTests.AreaLinkWithHugeExtentStaysInsideTheSheet`.
- **Model names compared with the current culture** (DatabaseRanges.cs:23, ExternalData.cs:148,
  NamedStyles.cs:46, PageStyles.cs:63/81/88): in Turkish «ITEMS» ≠ «items», so database ranges, mappings,
  styles and page styles were lost or duplicated depending on the system language. **Fixed**: `OrdinalIgnoreCase`.
  Test `DatabaseRangeNamesMatchTheSameWayInEveryCulture`.
- **SelectionWorkArea.Plan on almost all whole rows** (SelectionWorkArea.cs:53-55): selecting rows 2…1,048,576
  (not from 0, hence not «whole columns») every column with a default style adds a million cells to the set.
  **Not fixed**: requires changing the contract (styles by bands instead of per cell) in Ledger's format
  commands, outside the area.

### Minor
- Worksheet.cs: `_lastRow`/`_lastColumn` did not shrink when the extreme cells were emptied (only `Clear` reset them).
  **Fixed**: count per row and column (`OccupiedIndices`, sorted set + counters, maximum in O(log n)).
  Test `WorksheetUsedExtentTests` (fails before).
- Workbook.cs:92 `InsertSheet(Worksheet, int)` does not check duplicate names (used by undo, where the name is
  free by construction): noted.
- CellAddress.cs:17 `ColumnName` with a column beyond ~475,000 overruns the 4-character buffer (IndexOutOfRange); the
  callers pass valid columns: noted.
- SheetPrintDocument.cs:306: an object anchored to a previous page but spilling over is not drawn on the
  following page; noted.
- SheetThemes.cs:17: two styles with the same name (possible from files, also up to case) made
  `ToDictionary` throw when applying the theme. **Fixed**: case-insensitive keys, first one wins.
- ConditionalRule.cs:132: `IconCount` with an empty set name threw. **Fixed**.
- DateTimeTokens.cs:98: fractions of a second with more than 18 digits overflow the `long`; noted.
- FormControlBinding.cs:126: list index beyond `int.MaxValue` overflowed to negative. **Fixed**.

## Additions
- `Worksheet.Revision`: advances on every effective cell change, on `Clear` and on every change of the merges; a
  write equal to the previous one does not advance it. It serves the conditional formatting cache.

## Tests
Sheets.Tests 303, Sheets.Engine.Tests 1056, Formats.Odf.Tests 574, Formats.OpenXml.Tests 2047, Architecture.Tests 38: all green.

## Tests (second round)
Sheets.Tests 323, Sheets.Engine.Tests 1056, Sheets.Pivot.Tests 56, Sheets.Printing.Tests 26, Sheets.Queries.Tests 24,
Architecture.Tests 38, Localization.Tests 31 (touched for PVT006 and QRY005): all green. Every new test was
also run against the previous code (restored with `git show HEAD:…` into the file) and failed, or the process hung
(style cycle, workday series); the exception is the pivot performance test, not run on the old code
because it would take hours.

Live tests to do after the merge: pivot table with thousands of items and with a destination near the edge of the sheet
(message PVT006); printing and preview with print area `A:B`; query returning more rows than those left
(warning QRY005); «Standard» format typed with the interface in Italian; workday series with a
large step; theme applied to a file with duplicate named styles.

## Overall outcome
Read in full all files of src/PulseOffice.Sheets (85 files), Sheets.Pivot (9), Sheets.Printing (5),
Sheets.Queries (1). Fixed 2 serious ones in the first round (MergedRegions) and, in this one, 5 serious (cycle of parent styles,
workdays with a huge step, cubic pivot, pivot and query off the sheet), 14 medium and 5 minor. Still open,
noted above for the coordinator: the restructuring of SheetLayout (hidden rows and columns as intervals, with the
signature of RowAutoHeightTracker that depends on it), sheet reindexing in the ChangeLog, `SelectionWorkArea.Plan` on
whole rows, query `RowShift` not implemented, the pivot's «VERO»/«FALSO» boolean keys, and the declared minors
(duplicate names in `InsertSheet`, `ColumnName` beyond the limit, fractions of a second beyond 18 digits, objects
spilling across print pages).
