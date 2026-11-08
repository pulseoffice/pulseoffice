# Deep audit: Formats.OpenXml/Wordprocessing (DOCX)

Area: `src/PulseOffice.Formats.OpenXml/Wordprocessing`, 49 files, 7,902 lines. Every file read in full with Read.
New tests: `tests/PulseOffice.Formats.OpenXml.Tests/DocxDeepAuditTests.cs`. Loss codes: DOCX220–229.

## Coverage

| File | Lines read/total | Outcome |
|---|---|---|
| DocxBlockReader.cs | 454/454 | defects: negative column widths, row height `uint`→`int` that becomes negative, table indent without a cap (fixed) |
| DocxNumberingCodec.cs | 194/194 | defect: list start without a cap (`int.MaxValue` in letters → a label of 82 million characters in `ListLabeling`) (fixed) |
| DocxTableCodec.cs | 146/146 | ok |
| DocxTablesInParagraphs.cs | 47/47 | ok |
| DocxCharacterCodec.cs | 125/125 | defect: size, scale and spacing without a cap (fixed) |
| DocxInlineReader.cs | 717/717 | recursion of nested controls/hyperlinks: covered by the package's depth cap |
| DocxFlavour.cs (DocxValues) | 64/64 | defect: `Points` accepted `NaN` and `Infinity` (fixed) |
| DocxReader.cs | 424/424 | ok; DOCX220 declared |
| DocxParagraphCodec.cs | 174/174 | NaN of indents and spacings via `Points` (fixed) |
| DocxStylesCodec.cs | 193/193 | ok |
| DocxFieldInstructions.cs | 369/369 | ok |
| DocxWriter.cs | 435/435 | ok |
| DocxBlockWriter.cs | 238/238 | ok |
| DocxWriteContext.cs (also DocxReadContext) | 132/132 | defect: invalid hyperlink address → uncaught `UriFormatException`, the DOCX save is interrupted (fixed) |
| DocxDrawingCodec.cs | 839/839 | defect: extent, position and distances in `long` EMU converted to `int` without a cap (2^63 → `int.MaxValue`, negatives), `long` products overflowing in unpacked groups (fixed) |
| DocxInlineWriter.cs | 365/365 | ok |
| DocxInlineObjects.cs | 276/276 | defect: VML measures of OLE objects `NaN`/`Infinity`/negative → size `int.MaxValue` or negative (fixed) |
| DocxAnnotationsCodec.cs | 169/169 | ok: cyclic replies (mutual `paraIdParent`) stay out of the text and are removed as unanchored; guard test |
| DocxIndexes.cs | 210/210 | ok (table of contents levels `\o "1-999"` without a cap but harmless: used only as a comparison) |
| DocxVmlShapes.cs | 214/214 | defect: VML coordinates, origin and rotation `NaN`/`Infinity` in group members (fixed); performance note: `Type()` searches for the `shapetype` in the whole document for every shape (quadratic with many VML shapes), left |
| DocxEmbeddedFonts.cs | 151/151 | defect: unreadable embedded font discarded silently (empty `catch`) → now DOCX221 |
| DocxContentControls.cs | 143/143 | defect: surrogate checkbox symbol (`D800`) → `ArgumentOutOfRangeException`, **the whole document did not open** (fixed) |
| DocxRevisionWriter.cs | 131/131 | ok |
| DocxSignatureLineCodec.cs | 123/123 | ok |
| DocxBibliography.cs | 105/105 | ok |
| DocxParagraphFrames.cs | 102/102 | minor: `uint`→`int` without a cap (fixed) |
| DocxWatermarks.cs | 101/101 | defect: rotation and opacity `NaN`/huge → `NaN` angle, transparency outside 0–100 (fixed) |
| DocxVmlReader.cs | 84/84 | ok |
| DocxTableStyleParagraphs.cs | 84/84 | ok (`basedOn` chain protected by `visited`) |
| DocxFloatingTables.cs | 78/78 | minor: negative distances from the text (fixed) |
| DocxBorders.cs | 75/75 | defect: width (`w:sz`) and distance (`w:space`) `uint` without a cap → borders of millions of points (fixed; the same in `DocxTableCodec.Borders`) |
| DocxRangeMarkPlacement.cs | 74/74 | ok (note: `Nearest` linear per mark, quadratic only with many marks outside paragraphs) |
| DocxDropCaps.cs | 70/70 | defect: drop cap lines and distance without a cap (2,000,000,000 lines) (fixed: 2–10 lines as in Word) |
| DocxProtectionCodec.cs | 64/64 | ok |
| DocxDeletedParagraphMarks.cs | 56/56 | ok |
| DocxThemeColours.cs | 50/50 | ok |
| DocxTabPositions.cs | 49/49 | ok |
| DocxPageBreaks.cs | 47/47 | ok |
| DocxSectionInheritance.cs | 45/45 | ok |
| DocxSmartTags.cs | 43/43 | defect: every smart tag re-analysed the whole document (quadratic: 40,000 tags = 19 s) (fixed: one pass per nesting level) |
| DocxThemeFonts.cs | 42/42 | ok |
| DocxLegacyFormFields.cs | 38/38 | ok (selected index out of range handled by the converter) |
| DocxVmlPictures.cs | 34/34 | ok |
| DocxSectionStarts.cs | 27/27 | ok |
| DocxHeaderFooterExtent.cs | 26/26 | ok |
| DocxBookmarkReferences.cs | 25/25 | ok |
| DocxListHeaderIndent.cs | 21/21 | ok |
| DocxParagraphMarkChanges.cs | 16/16 | ok |
| DocxSectionCodec.cs | 213/213 | defects: page and margins `uint`→`int` negative or zero, columns up to 32,767, page start without a cap (fixed) |

**Total: 49 files of 49, 7,902 lines of 7,902.**

## Defects

### Blocking
- **Unbounded nesting → process stack overflow** (tables, controls, groups nested thousands of times): the SDK's
  `OpenXmlElement.RootElementContext` already overflows, even before our recursive readers
  (`DocxBlockReader.CollectWithIndexes`/`TableWithDefaults`, `DocxInlineReader.Walk`). Fix at the root in the
  package: new step `Packaging/DeepNestingRepair.cs` that trims elements beyond depth 384 in every XML part,
  damage `PartDamageKind.Trimmed`; DOCX declares it as DOCX220, XLSX and PPTX as a recovered part (XLSX049, PPTX025).
  Outside the area, bare minimum: `OoxmlPackageRepair.cs`, `XlsxReader.cs`, `PptxReader.cs` (one condition).

### Severe
- List start without a cap (`w:start`, `w:startOverride`): now 0–32,767 as in Word. The same is missing in ODT
  (`OdtStylesReading.cs:321`, `OdtBodyReading.cs:105`): reported, outside the area.
- Page, margins and columns out of size (`DocxSectionCodec.Read`): page 144–158,400 twips, margins ≥ 0, columns 1–45, page start 0–32,767.

- **DOCX save interrupted** by a hyperlink with an invalid address (`http://exa mple.com`, `a:b c`,
  `http://[x`): `DocxWriteContext.Link` did `new Uri(target, UriKind.Relative)`, which throws `UriFormatException`, not
  caught by the `catch` of `DocxWriter.Write`. Now the address is percent-encoded and as a last resort becomes
  relative. The same pattern remains in `PptxInteractionMapper.cs:131` and `PptxMediaMapper.cs:149` (outside the area, reported).

- **Document that does not open** because of a checkbox with a surrogate symbol (`w14:checkedState w14:val="D800"`):
  `DocxContentControls.Symbol` called `char.ConvertFromUtf32` on a surrogate. Now invalid code points
  fall back to the default symbol; when writing, `Hex` no longer throws on unpaired surrogates.

### Medium
- `DocxValues.Points` accepted `NaN`/`Infinity` from the file: non-finite indents and spacings.
- Character size (≤ 1,638 pt), scale (1–600 %), spacing (±1,584 pt) without a cap.
- Negative or overflowed row height and grid widths.
- Drawings: measures in EMU beyond `int` or negative (`DocxDrawingCodec.Twip`): now sizes and distances 0–633,600 twips,
  positions ±633,600, products in `double`.
- Borders (`DocxBorders.Side`, `DocxTableCodec.Borders`): width ≤ 12 pt, distance ≤ 31 pt as in Word.
- Drop caps (`DocxDropCaps.Merge`): lines 2–10, distance 0–1,584 pt.
- Smart tags (`DocxSmartTags.Unwrap`): quadratic unwrapping, now linear per level.
- Watermark (`DocxWatermarks.Extract`): `NaN` rotation and huge opacities; now a finite angle and transparency 0–100.
- Unreadable embedded fonts discarded silently (`DocxEmbeddedFonts.Add`): now DOCX221.
- `w:sym` outside the basic plane (`1F600`) truncated to a wrong character (`DocxInlineReader`): now a whole code point.
- OLE objects (`DocxOleCodec.Size`) and VML groups (`DocxVmlShapes.Number`/`Pair`) accepted `NaN` and `Infinity`.

## Outcome

All the defects found were fixed (1 blocking, 3 severe, 12 medium/minor), each with a test in
`DocxDeepAuditTests.cs` verified red before the fix. New codes: DOCX220 (nesting trimmed),
DOCX221 (unreadable embedded font). Remaining, declared: `Type()` of `DocxVmlShapes` quadratic with many
VML shapes; `Nearest` of `DocxRangeMarkPlacement` linear per mark; list start without a cap in ODT; unprotected `new Uri`
in `PptxInteractionMapper` and `PptxMediaMapper`.

Live tests (coordinator): open a DOCX with very deeply nested tables (warning DOCX220, no crash);
save to DOCX a document with a hyperlink `http://exa mple.com`; open a DOCX with a checkbox with the
symbol `D800`.

## Notes
- The test `XlsxMalformedInputTests.GarbageAttributesNeverRejectTheWorkbook(xl/worksheets/sheet1.xml)` already fails on the
  branch (non-numeric row attribute `r`): it comes from the previous XLSX commits, outside this area.

## PPTX and XLSX round (Presentation, Spreadsheet)

Preliminary fixes:
- **Blocking** — `SheetSections.cs:605` (`SheetDataReader.ReadRow`): a non-numeric row attribute `r` made the
  workbook be rejected, because `row.RowIndex.Value` threw outside the `try` introduced by fe09676e. Now `r` is read with
  invariant `uint.TryParse` and, if unreadable, the row takes the implicit position.
- **Medium** — `DocxSmartTags.Unwrap`: still quadratic (the SDK's `Remove`/`InsertBeforeSelf` walk the siblings);
  now the children of each parent are rebuilt in one pass. The timing test passed only on an idle machine.
- **Severe** — `PptxInteractionMapper.cs:131`, `PptxMediaMapper.cs:149`: `new Uri` on an unreadable address
  (`http://host:99999/`, `http://[::1/`) interrupted the PPTX save. Now `TryCreate` and, as a last resort, an
  encoded relative address.
- **Medium** — `DeckStructure.cs:66`: `p:sldSz` zero, negative or huge accepted; now within the ECMA limits
  (914,400–51,206,400 EMU) when reading and writing (`PresentationWriters.SlideEmu`, before `(int)` overflowed).
- **Medium** — `PptxAnimationMapper.cs:209-213`: delay and duration without a cap (duration from a non-finite `double` cast to
  `int`), `int.TryParse` with the current culture; now a cap of one hour and the invariant culture.

| File | Lines read | Outcome |
|---|---|---|
| Presentation/PptxPrintMasters.cs | 260/260 | notes body without limits when reading and `NaN` when writing: now 1–4,000 pt |
| Presentation/PptxEmbeddedFonts.cs | 98/98 | unreadable embedded font discarded silently (and decoder exceptions not caught): now PPTX220 |
| Presentation/PptxSounds.cs | 86/86 | clean |
| Presentation/PptxHeaderFooter.cs | 85/85 | clean (date rendered with the interface culture: intended) |
| Presentation/PptxColorMaps.cs | 121/121 | clean |
| Presentation/PptxRunLinks.cs | 80/80 | clean (`TryCreate` already present) |
| Presentation/PptxTextDefaults.cs | 70/70 | clean |
| Presentation/PptxElementRotation.cs | 59/59 | huge rotation: the product by 60,000 overflowed `long` and gave a wrong angle; now reduced to one turn first |
| Presentation/PptxElementVisibility.cs | 39/39 | clean |
| Presentation/PptxAnimationSound.cs | 39/39 | clean |
| Presentation/PptxShapeTextRuns.cs | 21/21 | clean |
| Presentation/Reading/PptxAlternateContent.cs | 22/22 | quadratic unwrapping (search from the root and `Remove`/`InsertBeforeSelf` linear in the siblings): now per parent, detaching the children |
| Presentation/Reading/OoxmlValues.cs | 172/172 | `Twips` overflowed `int` with huge EMU (sign inverted): now limited |
| Presentation/Reading/PlaceholderInheritance.cs | 138/138 | clean |
| Presentation/Reading/PresentationParts.cs | 139/139 | presentation interval `uint` cast to `int` (negative): now limited |
| Presentation/Reading/PptxGroupFlattener.cs | 162/162 | quadratic unwrapping of groups (20,000 groups: 42 s, now 2 s) |
| Presentation/Reading/ShapeColourNormalizer.cs | 149/149 | `fillRef` ≥ 1001 (theme background styles) ignored: the shape lost its fill; `uint` indices cast to `int` |
| Presentation/Reading/PptxDiagramExpander.cs | 181/181 | minor, not fixed: `nextId` can overflow with `cNvPr id` = `uint.MaxValue` |
| Presentation/Reading/DeckStructure.cs | 217/217 | slide size already fixed (above) |
| Presentation/Reading/BackgroundFills.cs | 222/222 | clean (colour modifiers limited to 0–1) |
| Presentation/Reading/PptxTableStyles.cs | 222/222 | negative or huge border width and cell size without limits: now 0–1,584 pt and 1–4,000 pt |
| Presentation/Reading/SlideParts.cs | 321/321 | position in the z-order computed with `ToList().IndexOf` for every shape: quadratic (40,000 shapes: 52 s, now 2 s); now an index per tree in `PptxSlideSource.Position` |
| Presentation/Writing/PresentationWriters.cs | 307/307 | non-hexadecimal pen colour written as it is (file rejected by PowerPoint): now the default value; new rule PPTX221 |
| Presentation/Writing/StructureWriters.cs | 207/207 | minor, not fixed: date/footer/number placeholders of the master written with a fixed 16:9 width even in 4:3 |
| Presentation/Writing/SlideWriter.cs | 174/174 | negative background opacity written negative: now ≥ 0 |
| Presentation/Writing/Foundation.cs | 88/88 | clean |
| Presentation/Writing/PptxDiagramWriter.cs | 126/126 | **severe**: SmartArt with an unreadable data part and modified text made the save crash (`XmlException` not caught); now the original is written and PPTX221 is declared |
| Presentation/Writing/PptxDiagramTexts.cs | 29/29 | clean |
| Presentation/Writing/PptxSlideGroupWriter.cs | 64/64 | group ID recomputed by walking the whole tree for every group (now a counter per tree); huge or non-finite rotation; `InDocumentOrder` remains, indexing the tree for every group (shared base class in `PulseOffice.Formats`, outside the area) |
| Presentation/Writing/TextBuilders.cs | 65/65 | size, indent and margin of the seed without limits or `NaN` check |

Presentation completed: 32 files, 3,994 lines.

### Spreadsheet

| File | Lines read | Outcome |
|---|---|---|
| Spreadsheet/OccupiedCells.cs | 11/11 | clean |
| Spreadsheet/XlsxThemeColours.cs | 27/27 | clean |
| Spreadsheet/OoxmlProtectionKeys.cs | 30/30 | clean |
| Spreadsheet/XlsxDrawingRunLinks.cs | 32/32 | clean (`TryCreate`) |
| Spreadsheet/Xlsx1904DateShift.cs | 39/39 | **severe**: the cache of "calendar" formats was an instance field, and `XlsxReader` keeps only one: a second 1904 workbook read with the same reader reused the classification of the first for the same format ID, shifting by 1,462 days numbers that are not dates (or leaving real dates behind). Now the cache is local to the call |
| Spreadsheet/XlsxObjectLinks.cs | 43/43 | clean |
| Spreadsheet/XlsxLinkSyntax.cs | 45/45 | clean |
| Spreadsheet/OoxmlTextEscape.cs | 54/54 | verified: decoded `_x0001_` and `_xD800_` then pass into ODS without errors (regression test added) |
| Spreadsheet/XlsxDynamicArrays.cs | 67/67 | minor, not fixed: `int.TryParse` without the invariant culture on `t`/`v` (digits only, no effect in practice) |
| Spreadsheet/XlsxDataTableWriter.cs | 77/77 | clean |
| Spreadsheet/XlsxAutoFilterCodec.cs | 95/95 | filter column `uint` cast to `int` (negative) and non-finite top-N count: now discarded / limited |
| Spreadsheet/XlsxPageSetupCodec.cs | 180/180 | `NaN`/huge/negative margins became `int.MaxValue` or negative, scale up to 4,000,000 %, breaks beyond the sheet or negative, negative "fit to" pages written as huge `uint`s: now within limits |
| Spreadsheet/SheetSections.cs | 749/749 | **severe**: `NaN`/infinite numbers written as `NaN`/`Infinity` (a file Excel has to repair) and read as non-finite numbers: now `#NUM!` in both directions; non-hexadecimal or 3-digit tab colour written as it is; array formulas searched in a `List` (quadratic): now sets; row heights, column widths and outline levels without a cap (409 pt, 255 characters, 7) |

Spreadsheet completed: 13 files, 1,449 lines.

### Outcome of the PPTX and XLSX round

Coverage: Presentation 32/32 files (3,994 lines), Spreadsheet 13/13 files (1,449 lines), plus `DocxSmartTags.cs` reread.
New codes: PPTX220 (unreadable embedded font), PPTX221 (modified text in a SmartArt with unreadable data).

- Blocking (1): unreadable row `r` rejected the workbook (`SheetSections.cs:605`).
- Severe (5): `new Uri` in the PPTX save (`PptxInteractionMapper.cs:131`, `PptxMediaMapper.cs:149`); SmartArt with unreadable data and modified text (`PptxDiagramWriter.cs:24`); 1904 format cache shared between workbooks (`Xlsx1904DateShift.cs:14`); non-finite numbers in XLSX (`SheetSections.cs:55,90`).
- Medium (14): quadratic behaviour in groups, alternate content, z-order, smart tags and array formulas; slide size; animation timings; embedded fonts discarded silently; `fillRef` to background styles; non-hexadecimal pen and tab colours; EMU beyond `int`; XLSX margins/scale/breaks; XLSX rows/columns without a cap.
- Minor (8): huge rotations (shapes and groups), notes body size, presentation interval, table borders and cell size, background opacity, placeholder seed, autofilter.

Remaining, declared: `PptxDiagramExpander` can overflow `nextId`; master placeholders at a fixed 16:9 width;
`SlideGroupWriter.InDocumentOrder` indexes the tree for every group (base class in `PulseOffice.Formats`, outside the area);
`int.TryParse` without culture in `XlsxDynamicArrays`.

Live tests (coordinator): open a PPTX with 20,000 groups or 40,000 shapes (seconds, not minutes); save to PPTX
a slide with a hyperlink `http://host:99999/`; open two 1904 XLSX files in a row; open in Excel an XLSX
saved from a sheet with `=0/0` converted to a value and a coloured tab.

## Round of the shared folders (Packaging, Diagrams, Charts, …)

### Packaging

| File | Lines read | Outcome |
|---|---|---|
| Packaging/OoxmlPackageRepair.cs | 180/180 | **severe**: cap per part (512 MB) and per compressed package, but no cap on the expanded sum: a thousand parts of 500 MB compressed into 500 KB each all passed into memory. Now a sum of declared sizes beyond 1 GB rejects the package before decompressing (verified separately that .NET truncates every part to its declared size, so the declared sum is a real cap). **medium**: `ActualName` linear, called for every relationship (quadratic): now an index |
| Packaging/ContentTypeRepair.cs | 130/130 | **medium**: search of the `Override`s linear per relationship (quadratic) and comparison between escaped and unescaped name: a part with spaces received a second, duplicate `Override`. Now an index by decoded name and the name rewritten escaped |
| Packaging/DeepNestingRepair.cs | 98/98 | clean (XML comments lost only in the parts that are too deep: accepted) |
| Packaging/MalformedXmlRepair.cs | 156/156 | clean (regex with timeout, DTD forbidden) |
| Packaging/DuplicateRelationshipRepair.cs | 26/26 | clean |
| Packaging/PackageRelationships.cs | 86/86 | clean: `TargetMode="External"` excluded, `..` beyond the root stopped at the root; names never touch the disk, so no zip slip |
| Packaging/SafeXmlLoader.cs | 27/27 | clean (DTD forbidden, no resolver) |
| Packaging/UndeclaredPartRepair.cs | 59/59 | clean; false content types of known parts are corrected only if generic (conservative choice) |
| Packaging/OoxmlReadFailures.cs | 10/10 | clean |

Test: `OpenXmlPackagingAuditTests` (fails without the fix).

### Diagrams

| File | Lines read | Outcome |
|---|---|---|
| Diagrams/DiagramDataModel.cs | 133/133 | **blocking**: the `parOf` connections were taken as they are; a cycle (a node parent of the document) sent `Descendants` into infinite recursion: *stack overflow*, process killed on opening the PPTX (verified: the test host crashed). A node with two parents also multiplied the descendants exponentially. Now the model is a tree visited breadth-first from the root, each node a single parent, depth at most 128 |
| Diagrams/DiagramLayoutTree.cs | 295/295 | **severe**: `forEach` nested or calling each other with `ref` multiplied the work at every level (up to 48 levels: exponential). Now a cap of 20,000 between boxes and expansions |
| Diagrams/DiagramLayoutEngine.cs | 122/122 | **severe**: a huge `primFontSz` constraint (`1e300`) made the text fitting run in steps of 0.5 points forever. Now size between 1 and 4,000 points |
| Diagrams/DiagramAlgorithms.cs | 345/345 | clean (minor, not fixed: a negative `sibSp` can overlap the children, rendering only) |
| Diagrams/DiagramConstraintSolver.cs | 136/136 | clean (non-finite values already discarded) |
| Diagrams/DiagramDrawingComposer.cs | 180/180 | clean |
| Diagrams/DiagramLayoutDefinition.cs | 114/114 | clean |

Outside the folder, the known defect: `Presentation/Reading/PptxDiagramExpander.cs` — with a `cNvPr id` equal to `uint.MaxValue`
`nextId` went back to 0 and the IDs of the diagram members collided with those already present. Now an allocator
(`DiagramShapeIds`) skips the used IDs and zero. No test was found that tells it apart through the public model
(element IDs do not derive from `cNvPr`): fix verified by reading.

Test: `OpenXmlDiagramAuditTests` (cycle: host crash before the fix; huge size: hang beyond 30 s).

### Charts

| File | Lines read | Outcome |
|---|---|---|
| Charts/ChartSpaces.cs | 455/455 | **medium**: source sheet name put in quotes only if it contained spaces: `L'anno`, `Q1-2024`, `2024` gave invalid series formulas (Excel repairs the file). Now `SheetNameQuoting` from `Sheets.Engine`, the same as for formulas. **medium**: non-hexadecimal series colour (`rosso`, `#ABC`) written as it is into `srgbClr`; now it falls back to the palette. **severe**: inline data filled up to the declared `ptCount` (100,000) for every series, with no cap on the number of series: 10,000 empty series = 8 GB. Now a cap of 1,000,000 points per chart |
| Charts/ChartFamilies.cs | 484/484 | clean |
| Charts/InlineChartParts.cs | 40/40 | clean |
| Charts/OoxmlChartFidelity.cs | 12/12 | clean |
| Charts/XlsxChartOrientation.cs | 34/34 | clean |

Test: `OpenXmlChartAuditTests` (all three fail without the fix).

### Conditional

| File | Lines read | Outcome |
|---|---|---|
| Conditional/XlsxConditionalExtension.cs | 108/108 | **medium**: the extended icon sets (x14) wrote the priority zero-based (`priority="0"`, outside Excel's semantics) and `X14IconSetRuleReader` read it back without subtracting 1, alone among the readers: in an Excel file the relative order with the main rules shifted by one. Now one-based in writing and reading |
| Conditional/X14RuleReaders.cs | 168/168 | as above; non-positive priorities brought back to 1 |
| Conditional/X14DataBars.cs | 163/163 | **minor**: `NaN`/infinite thresholds written as `NaN`/`Infinity` and read back as such; now 0 |
| Conditional/RuleCodecs.cs | 442/442 | same threshold defect in the main branch; `Priority + 1` overflowed with `int.MaxValue` |
| Conditional/RangeRuleCodecs.cs | 128/128 | clean |

Test: `OpenXmlConditionalAuditTests` (both fail without the fix).

### DrawingML and Shapes

| File | Lines read | Outcome |
|---|---|---|
| DrawingML/DrawingDuotone.cs | 43/43 | **blocking**: `uint.Parse` on a duotone colour of six non-hexadecimal characters (`ZZZZZZ`) threw `FormatException`, which the reader treats as a malformed file: XLSX, DOCX and PPTX rejected entirely. Now `TryParse` and the duotone is discarded; when writing a non-hexadecimal colour becomes `000000` |
| DrawingML/OoxmlSvgBlip.cs | 43/43 | clean |
| DrawingML/OoxmlThemeScheme.cs | 32/32 | clean |
| Shapes/OoxmlFontworkText.cs | 57/57 | clean |
| Shapes/DeckObjectCodecs.cs | 497/497 | **blocking**: `GetPartById` on an `r:embed` naming no relationship threw `ArgumentOutOfRangeException` and the PPTX was rejected; now `TryGetPartById`. **medium**: EMU positions beyond the twip range cast to `int` (they came back negative); now limited. **severe**: `new Uri` on the path of a linked image in the save (same defect already fixed elsewhere); now `TryCreate` with a fallback. **minor**: rotation of Deck shapes and formula size without limits (rotation normalized; no test tells it apart because the usual path already normalizes it) |
| Shapes/SheetShapes.cs | 314/314 | minor, not fixed: non-hexadecimal connector colour written as it is |

Tests: `OpenXmlDrawingAuditTests`, `OpenXmlDeckShapeAuditTests` (fail without the fix).
Known, not fixed: the OLE frame uses `id + 1000` for the replacement image, which can collide with another ID.

### ShapeStyles

| File | Lines read | Outcome |
|---|---|---|
| ShapeStyles/Parts.cs | 156/156 | **medium**: `DrawingColours.Of`, through which the fills, lines, shadows and glows of all shapes go, wrote any text into `srgbClr` (`verde` → `0VERDE`): files to be repaired in Office. Now a non-hexadecimal colour becomes `000000` |
| ShapeStyles/Reading.cs | 315/315 | clean (`GetPartById` preceded by the relationship check) |
| ShapeStyles/OoxmlPendingImages.cs | 92/92 | clean |
| ShapeStyles/DrawingPresetGeometry.cs | 81/81 | clean |
| ShapeStyles/DrawingStyleFidelity.cs | 36/36 | clean |
| ShapeStyles/FormulaShapeFidelity.cs | 15/15 | clean (recursion over groups limited by the model's depth) |
| ShapeStyles/UnknownPresetShapes.cs | 18/18 | clean |
| ShapeStyles/Vocabulary.cs | 31/31 | clean |

Test: `OpenXmlDrawingAuditTests.A_shape_fill_colour_that_is_not_hex…` (fails without the fix).

### Drawing

| File | Lines read | Outcome |
|---|---|---|
| Drawing/XlsxVmlAnchor.cs | 38/38 | **medium**: `x:Anchor` with huge pixel offsets multiplied by 15 in `int`: the position overflowed to negative. Now `long` arithmetic and a limited position |
| Drawing/XlsxAnchorLayout.cs | 116/116 | **medium**: `Order` built the list of all the anchors of the drawing and searched the index in it for every object (quadratic: 10,000 images = 10⁸ steps). Now an index per drawing. **minor**: `int.TryParse`/`long.TryParse` without the invariant culture |
| Drawing/VmlSignatureLineCodec.cs | 98/98 | clean |
| Drawing/VmlStyle.cs | 34/34 | clean (saturating conversions in .NET 10) |
| Drawing/XlsxCellAnchors.cs | 37/37 | clean |
| Drawing/XlsxVmlDocuments.cs | 54/54 | clean |
| Drawing/XlsxVmlShapes.cs | 23/23 | clean |

Test: `OpenXmlDrawingAuditTests.A_vml_anchor_with_an_enormous_pixel_offset…` (fails without the fix); the order
of the anchors is covered by the existing XLSX tests (350 green).

### CellStyles, Tables, Embedding

| File | Lines read | Outcome |
|---|---|---|
| CellStyles/Codecs.cs | 275/275 | **medium**: `SpreadsheetColours.ToArgb` wrote as it is every colour not of 6 digits (`#ABC` → `rgb="ABC"`, `rosso` → `ROSSO`) in fonts, fills, borders and differential formats: invalid `styles.xml`. When reading, an ARGB of eight non-hexadecimal characters became a model colour. Now ARGB is always eight hexadecimal digits |
| CellStyles/ExcelBooleanFormat.cs | 19/19 | clean |
| Tables/XlsxTables.cs | 203/203 | **blocking**: `GetPartById` on the `r:id` of a sheet naming no relationship threw `ArgumentOutOfRangeException`: workbook rejected (`XLSX003`). Now `TryGetPartById`. Same fix, outside the folder, in `XlsxReader.cs:163`, `XlsxPivotMapper.cs:371`, `XlsxPivotSourceResolver.cs:20`, `XlsxChartMapper.cs:64` (the test fails until the last one is fixed too). Header and total rows limited to the height of the table. Minor, not fixed: `Qualified` does not quote names starting with a digit |
| Embedding/OoxmlOleReplacement.cs | 17/17 | clean |

Test: `OpenXmlWorkbookPartsAuditTests` (both fail without the fix).

### Files at the root (first batch)

| File | Lines read | Outcome |
|---|---|---|
| OoxmlVectorPaths.cs | 82/82 | minor, not fixed: non-hexadecimal colour written as it is (as in `SheetShapes`) |
| PptxDeckChartMapper.cs | 49/49 | clean |
| PptxFlavour.cs | 24/24 | clean |
| PptxMediaTiming.cs | 48/48 | minor, not fixed: volume read without limits (`vol="500000"` → 5 in the model) |
| XlsxMetrics.cs | 57/57 | clean |
| XlsxValidationExtension.cs | 38/38 | clean |
| XlsxPivotSourceResolver.cs | 57/57 | `GetPartById` fixed (see Tables) |
| XlsxSignatureLineMapper.cs | 74/74 | clean |
| XlsxShapeMapper.cs | 86/86 | clean |
| XlsxConditionalMapper.cs | 90/90 | clean |

### Files at the root (second batch)

| File | Lines read | Outcome |
|---|---|---|
| XlsxCommentMapper.cs | 112/112 | **blocking**: `authorId` `uint` cast to `int`: `4294967295` became −1, passed the check `index < authors.Count` and `authors[-1]` threw: workbook rejected. Now an unsigned comparison. **medium**: `authors.IndexOf` for every comment when writing (quadratic with many authors); now a dictionary |
| XlsxDrawingMapper.cs | 105/105 | clean |
| XlsxScenarioMapper.cs | 100/100 | minor, not fixed: `NumberStyles.Any` accepts thousands separators and currency symbols in scenario values |
| XlsxVectorMapper.cs | 104/104 | clean |
| XlsxSingleValueSyntax.cs | 112/112 | declared risk, not fixed: `Export` recurses once for every nested `@(`; thousands of levels in a single formula could exhaust the stack on save (the formula parser, outside the area, should already limit its depth) |

Test: `OpenXmlWorkbookPartsAuditTests.A_comment_author_index…` (fails without the fix).

### Files at the root (third batch)

| File | Lines read | Outcome |
|---|---|---|
| XlsxChartMapper.cs | 161/161 | **medium**: the source sheet was derived with `IndexOf('!')` and `Trim('\'')`: a name with an apostrophe (`'L''anno'!…`, now written correctly) came back as `L''anno` and the chart lost its source; a `!` inside the quoted name broke the reference. Now `LastIndexOf` and quotes removed with `''` → `'` |
| XlsxFormulaMapper.cs | 127/127 | **medium**: formula objects are described with fields separated by `|`; a sheet or an ID with `|` (allowed by Excel) shifted the fields and the object disappeared silently on reading back. Now name and ID are encoded (`%7C`, `%25`) |
| XlsxFormulaSyntax.cs | 116/116 | clean (regex with timeout) |
| XlsxSparklineMapper.cs | 130/130 | clean (colours go through `SpreadsheetColours`, now validated) |
| XlsxImageMapper.cs | 168/168 | clean |

Test: `OpenXmlChartAuditTests` (two new cases, fail without the fix).

### Outcome of the round of the shared folders (session interrupted at a 5-file boundary)

Coverage: Packaging 9, Diagrams 7, Charts 5, Conditional 5, DrawingML 3, Shapes 3, ShapeStyles 8, Drawing 7,
CellStyles 2, Tables 1, Embedding 1 (51 files, 6,135 lines) and 20 files at the root (2,190 lines), plus the touched lines of
`XlsxReader.cs`, `XlsxPivotMapper.cs` and `PptxDiagramExpander.cs`. No new loss code. Test project
`PulseOffice.Formats.OpenXml.Tests`: 2,117 green.

- Blocking (5): cyclic SmartArt data (stack overflow, `DiagramDataModel.cs`); non-hexadecimal duotone
  (`DrawingDuotone.cs:31`); Deck image with a missing relationship (`DeckObjectCodecs.cs:341`); sheet with an `r:id` without a
  relationship (`XlsxTables.cs:37`, `XlsxReader.cs:163` and two others); `authorId` beyond `int` (`XlsxCommentMapper.cs:42`).
- Severe (6): zip bomb by sum of parts (`OoxmlPackageRepair.cs`); exponential `forEach`s (`DiagramLayoutTree.cs`);
  huge size hanging the fitting (`DiagramLayoutEngine.cs:63`); inline chart points without a total cap
  (`ChartSpaces.cs:435`); `new Uri` on the linked image (`DeckObjectCodecs.cs:306`); overflowing diagram IDs.
- Medium (14): quadratic behaviour in `ActualName`, `Override`, anchor order, comment authors; duplicate `Override`s;
  sheet name in chart formulas and its reading back; invalid colours in charts, shapes and `styles.xml`;
  zero-based x14 priority; EMU beyond `int`; VML anchor; formula objects with `|`.
- Minor (6 fixed, 6 declared): non-finite thresholds, Deck rotation and size, culture in the anchor `TryParse`s,
  table headers; not fixed: colours of `SheetShapes`/`OoxmlVectorPaths`, media volume,
  `NumberStyles.Any` in scenarios, `Qualified` of structured references, `id + 1000` of OLE frames,
  negative `sibSp`, recursion of `XlsxSingleValueSyntax.Export`.

**Still to be read** (18 files at the root, about 5,600 lines): `PptxAnimationBehaviourCodec.cs`,
`PptxAnimationMapper.cs`, `PptxCommentMapper.cs`, `PptxDeckTableMapper.cs`, `PptxInteractionMapper.cs`,
`PptxMediaMapper.cs`, `PptxReader.cs`, `PptxTransitionMapper.cs`, `PptxWriter.cs`, `XlsxFormControlMapper.cs`,
`XlsxFormat.cs`, `XlsxMetadataMapper.cs`, `XlsxPivotMapper.cs`, `XlsxQueryTableMapper.cs`, `XlsxReader.cs`,
`XlsxShapeStyleMapper.cs`, `XlsxStyleMapper.cs`, `XlsxWriter.cs`.

Live tests (coordinator): open a PPTX with SmartArt whose data has a cycle (before, the app closed);
save to XLSX a chart on a sheet `L'anno` and reopen it in Excel and in PulseOffice; open an XLSX with a sheet
whose `r:id` was deleted by hand.

### Last files at the root: Pptx batch (9 files, 1,934 lines)

| File | Lines read | Outcome |
|---|---|---|
| PptxAnimationBehaviourCodec.cs | 382/382 | **minor** (declared): `Point` and the rotation convert to `int`/`long` without a cap a huge but finite scale factor; no crash, a senseless value in the file |
| PptxAnimationMapper.cs | 277/277 | clean (timings limited to one hour, repetitions to 100, native XML validated on reading) |
| PptxCommentMapper.cs | 126/126 | **severe**: the author's initials took the first `char`; a name starting with an emoji (`😀 Mario`) left an unpaired surrogate and the PPTX save failed with `ArgumentException`. Now the first text element (`StringInfo`). Minor declared: `UpdateLastIndex` linear per comment |
| PptxDeckTableMapper.cs | 273/273 | **medium** (performance): format and merge of every cell searched with `FirstOrDefault` over the whole list, quadratic on large tables with many formats; now an index per cell (`TableCellIndex`) built once |
| PptxInteractionMapper.cs | 148/148 | **minor**: `jump=lastslideviewed` contained `jump=lastslide` and became "last slide"; now the parameter is compared exactly |
| PptxMediaMapper.cs | 191/191 | clean |
| PptxReader.cs | 169/169 | clean |
| PptxTransitionMapper.cs | 251/251 | **minor** (declared): `advTm` read with the current culture and without a cap or sign check |
| PptxWriter.cs | 117/117 | clean |

Test: `OpenXmlRootAuditTests` (two cases, fail without the fix).

### Last files at the root: Xlsx batch (9 files, 2,670 lines)

| File | Lines read | Outcome |
|---|---|---|
| XlsxFormControlMapper.cs | 615/615 | **minor** (declared): `DropLines` and `ShapeId` beyond `int` become negative (no crash: they are rewritten with `Math.Max(1, …)`) |
| XlsxFormat.cs | 337/337 | clean (loss codes all already declared) |
| XlsxMetadataMapper.cs | 258/258 | **minor** (declared): `Print_Titles` with negative rows or rows beyond the sheet accepted without a cap |
| XlsxPivotMapper.cs | 498/498 | **blocking**: `dataField/@fld` beyond `int` became negative, passed the check `>= Count` and the negative index rejected the whole workbook; now an unsigned comparison. **Severe** (performance): every record of the cache searched the position of the item with `List.IndexOf`, quadratic: a pivot over 60,000 distinct codes took more than 13 s; now a dictionary, under one second |
| XlsxQueryTableMapper.cs | 201/201 | **blocking**: a defined name with `localSheetId` beyond `int` went through `(int)` negative into `sheets[…]` and rejected the workbook; now an unsigned check and a lower bound |
| XlsxReader.cs | 233/233 | **medium**: a duplicate sheet was renamed `name_N` without checking that `name_N` was free (`A`, `A_3`, `A` → two `A_3`, workbook rejected); now the suffix advances until the name is free |
| XlsxShapeStyleMapper.cs | 174/174 | **minor** (declared): widths and indents beyond `int` EMU truncated by `(int)` |
| XlsxStyleMapper.cs | 164/164 | **medium**: the key of styles with a named style was `HashCode.Combine(...)` in the same dictionary as the simple IDs: a collision assigned to a cell the format of another; now a tuple key. Minor: index of the named styles with `CurrentCultureIgnoreCase` (Turkish), now ordinal |
| XlsxWriter.cs | 190/190 | clean |

Test: `OpenXmlRootAuditTests` (four new cases, fail without the fix).

### Minor declared in the previous rounds: fixed

- Non-hexadecimal colours of `Shapes/SheetShapes.cs` (connectors) and `OoxmlVectorPaths.cs`: now they go through `DrawingColours.Of`, which writes `000000` instead of an invalid `srgbClr` (test on the vector).
- `XlsxScenarioMapper.cs`: `NumberStyles.Any` read `1,234` as 1234 and `$5` as 5; now `NumberStyles.Float`, the rest stays text (test).
- `Diagrams/DiagramAlgorithms.cs`: a negative `sibSp` overlapped the children; now limited to zero.
- `XlsxSingleValueSyntax.Export`: a sequence of `@` or nested `@(` recursed once per character and closed the process with a stack overflow (verified: 20,000 `@` kill the test host); now consecutive `@` merge into one (`@@x` ≡ `@x`) and nesting beyond 64 levels stays as it is (test on a thread with a 256 KB stack).

Still not fixed (minor): media volume, `Qualified` of structured references, `id + 1000` of OLE frames, and the
minor ones declared in this batch (conversions to `int` without a cap in animations, controls, shapes; `advTm` with the current
culture; `Print_Titles` without a cap; linear `UpdateLastIndex`).

## Overall outcome for openxml

Coverage: all of `src/PulseOffice.Formats.OpenXml` read in full — 51 files of the shared folders (6,135 lines) and
38 files at the root (20 in the previous round, 2,190 lines; 18 in this one, 4,604 lines). No new loss code.
Project `PulseOffice.Formats.OpenXml.Tests`: 2,126 green; `PulseOffice.Architecture.Tests`: 38 green.

- Blocking (7): the five of the previous round, plus `dataField/@fld` beyond `int` (`XlsxPivotMapper.cs:404`) and
  `localSheetId` beyond `int` (`XlsxQueryTableMapper.cs:183`), which rejected the whole workbook.
- Severe (8): the six of the previous round, plus the PPTX save broken by a comment author starting with an emoji
  (`PptxCommentMapper.cs:118`) and the quadratic pivot cache (`XlsxPivotMapper.cs:496`).
- Medium (17): the fourteen of the previous round, plus quadratic Deck tables (`PptxDeckTableMapper.cs:73`), duplicate
  sheets renamed onto an existing name (`XlsxReader.cs:157`), style keys by hash (`XlsxStyleMapper.cs:106`).
- Minor: 6 + 2 fixed ("last viewed" jump, culture of the style index) + 4 declared before; those listed above
  remain.
- All the fixed defects have a test that fails without the fix (exception: `sibSp`, fixed without a dedicated
  test because the only effect is the visual overlap).

Live tests (coordinator): save to PPTX a presentation with a comment by "😀 Mario" and reopen it in PowerPoint and
in PulseOffice; open an XLSX with a pivot over tens of thousands of distinct rows and save it (timings); open an XLSX
with two sheets `A` and one sheet `A_3`.
