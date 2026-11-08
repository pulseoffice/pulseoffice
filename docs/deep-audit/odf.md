# Deep audit: ODF formats (Formats.Odf, Formats.Package, Formats)

Size of the area: Formats.Odf 153 files / 23,861 lines; Formats.Package 19 / 2,845; Formats 41 / 2,055.

## Coverage

| Part | Status |
|---|---|
| Interrupted work of the previous agent (10 files + `XmlPartLoader`, `OdfPackageHardeningTests`) | read in full, verified, committed |
| Formats.Package | XML loading, encryption, Flat ODF, legacy upgrade: read at the changed points |
| Formats.Odf, Formats | **cross-cutting pass by patterns**, not line-by-line reading: XML loads (XXE, DTD), numeric conversions without culture or without a cap, `(int?)XAttribute` casts that throw `FormatException`, empty `catch`, paths and temporary files, comments |

The full file-by-file reading is **not complete**: it remains to be done for Formats.Odf (in particular `Odt/`, `Presentation/`, `Spreadsheet/`, `Charts/`) and for Formats.

## Defects found and fixed

| Severity | Where | Defect | Fix |
|---|---|---|---|
| serious | `Formats.Package/OdfPackage.cs`, `FlatOdfPacker.cs`, `Encryption/OdfPackageEncryption.cs` | XML parts loaded with `XDocument.Load` without settings: DTD and nested entities expanded (billion laughs) | shared `XmlPartLoader` (DTD ignored, `MaxCharactersFromEntities`), replaces `LegacyXmlLoader` |
| serious | `Encryption/Argon2id.cs` | the manifest could request 4 GiB of Argon2 memory | cap at 1 GiB |
| serious | `Encryption/OdfPackageEncryption.cs` | decrypted package without an overall expansion cap | 1 GiB cap on the total; `Inflate` does not throw beyond the per-entry cap |
| serious | `TextCodecs/OdfParagraphText.cs:99` | `text:s text:c` without a cap (2 billion spaces: memory exhausted) | limited to 100,000 as in `OdtBodyReading` |
| serious | `CellStyles/NumberStyles.cs` | format digits (`min-integer-digits`, `decimal-places`, exponent range) without a cap, negative ones cause `ArgumentOutOfRangeException`, huge ones exhaust memory; second decimals read with the current culture | cap 0–64 and invariant culture |
| medium | `Shapes/DeckObjectCodecs.cs` (Deck tables), `OdfDatabaseRangeMapper.cs:190` | `(int?)XAttribute` throws `FormatException` on a malformed value and stops reading | invariant `int.TryParse` with a cap |
| medium | `OdfImageMapCodec.cs` | malformed polygon coordinates stopped reading | malformed points discarded |
| medium | `Formats/SvgSanitizer.cs` | `set`/`animate` could rewrite `href` with `javascript:` | removed |
| minor | `Formats/SvgRecolour.cs` | `XDocument.Parse` with DTD | DTD prohibited |
| minor | `OdfMetaMapper.cs` | `editing-cycles` with current culture and negative | invariant, negatives discarded |

Tests: `OdfPackageHardeningTests`, `OdsSeventeenthAuditTests.UnNumeroDiCifreFuoriMisuraNelFormatoNonFermaLaLettura`,
`OdpPresentationStyleTests.UnaSequenzaDiSpaziSproporzionataVieneLimitata`. `PulseOffice.Formats.Odf.Tests`: 582 green.

## Remaining

- Full reading of every file of Formats.Odf and Formats (see coverage).
- No defect requires live tests; to be retried by hand only: opening an ODT encrypted with Argon2 by the reference suite.

## Full reading of `Formats.Odf/Odt` (round 2)

| File | Lines read/total | Outcome |
|---|---|---|
| `Odt/OdtLogicalViewBox.cs` | 32/32 | no defects |
| `Odt/OdtReadLosses.cs` | 37/37 | no defects |
| `Odt/OdtLegacyFormFields.cs` | 54/54 | no defects (out-of-range dropdown index handled by the converter) |
| `Odt/OdtContours.cs` | 74/74 | minor: `NaN` accepted as a contour coordinate (see below) |
| `Odt/OdtWriteSession.cs` | 85/85 | minor: `AddMedia` linear per image (quadratic on thousands of images), left |
| `Odt/OdtWatermarks.cs` | 100/100 | no defects |
| `Odt/OdtFoundation.cs` | 116/116 | no defects |
| `Odt/OdtContentControls.cs` | 126/126 | no defects (the time of `current-date` is lost by choice: the model keeps the date) |
| `Odt/OdtReadContext.cs` | 126/126 | no defects |
| `Odt/OdtIndexes.cs` | 194/194 | no defects (out-of-range levels harmless: used only for comparison) |
| `Odt/OdtStylesWriting.cs` | 268/268 | no defects |
| `Odt/OdtVocabulary.cs` | 303/303 | no defects |
| `Odt/OdtDrawings.cs` | 325/325 | minor: the SVG path is born from the identifier with non-alphanumeric characters replaced, so `a-b` and `a_b` collide (left: generated identifiers do not do that) |
| `Odt/OdtStylesReading.cs` | 374/374 | **serious**: `text:start-value` without a cap (`2000000000` with format `a` makes `LetterNumbers` allocate 77 million characters per label); fixed: 0–65535 like the reference suite |
| `Odt/OdtProperties.cs` | 434/434 | **medium**: `style:drop-cap` with lines and characters without a cap; fixed: 1–255 and 0–255 |

Test: `OdtOutOfRangeValueTests` (failed before the fix, green after). Left outside the area, to be reported: `LetterNumbers.Render`
in `Documents.Layout/Flow/ListLabeling.cs` allocates `value/26` characters without a cap even for a value set from the interface.
| `Odt/OdtWriter.cs` | 455/455 | no defects (the file's `SpinCount` held by `DocumentProtection.MaxSpinCount`) |
| `Odt/OdtReader.cs` | 507/507 | no defects |
| `Odt/OdtBodyWriting.cs` | 835/835 | **serious**: a cell with a date format and a number outside the calendar (`99999999`) made `DateTime.FromOADate` throw and saving the whole document failed with ODT100; fixed: the out-of-calendar value is not written and the cell stays text. Minor left: `OdtCellNumbers.Values` reads the text first with the interface's current culture, not with the document language |
| `Odt/OdtBodyReading.cs` | 945/945 | **serious**: repeated rows and cells (`number-rows-repeated`, `number-columns-repeated`) had a cap only per element, not an overall one: a few repeated rows, or nested tables, multiplied millions of cells; fixed with a budget of 100,000 copies per document in `OdtReadContext.TakeRepetition`. **Serious**: `text:start-value` on the list item without a cap (same allocation in `LetterNumbers`); fixed 0–65535 |

Coverage of `Formats.Odf/Odt`: **19 files out of 19, 5390 lines out of 5390**, all read in full.

Defects in `Odt/`: 3 serious (list start and list item without a cap; repetitions without a budget; save stopped by an out-of-calendar date),
1 medium (drop cap), 3 minor left (contour with `NaN`, SVG path collision, culture of cell values).
Test: `OdtOutOfRangeValueTests` (5 cases, all failed before the fix). No live test needed.

## Full reading of `Formats.Odf/Presentation` (round 2)

| File | Lines read/total | Outcome |
|---|---|---|
| `Presentation/OdfSeedNames.cs` | 10/10 | no defects |
| `Presentation/Reading/OdpReadContext.cs` | 10/10 | no defects |
| `Presentation/Reading/OdpHiddenShapes.cs` | 19/19 | no defects |
| `Presentation/OdpElementVisibility.cs` | 26/26 | no defects |
| `Presentation/OdfTextBackdrops.cs` | 33/33 | no defects |
| `Presentation/OdpTextDefaults.cs` | 33/33 | no defects |
| `Presentation/Reading/OdpMissingObjects.cs` | 34/34 | no defects |
| `Presentation/Writing/OdpSlideGroupWriter.cs` | 35/35 | no defects |
| `Presentation/OdpSounds.cs` | 42/42 | no defects (sounds are read only from the package) |
| `Presentation/OdfFreeTextShape.cs` | 51/51 | no defects |
| `Presentation/OdpElementRotation.cs` | 53/53 | no defects |
| `Presentation/OdpGridXml.cs` | 54/54 | no defects |
| `Presentation/OdfGraphicStyles.cs` | 55/55 | no defects |
| `Presentation/Reading/TextReading.cs` | 73/73 | no defects |
| `Presentation/Reading/OdpGroupFlattener.cs` | 81/81 | **blocking**: recursive `Collect` without a cap: an ODP with `draw:g` nested 50,000 times brought the process down with a stack overflow (cannot be caught); fixed: beyond 64 levels groups are not collected as members, the iterative flattening stays |

Same **blocking** defect found in `Odt/OdtDrawings.cs` (`OdtDrawingReader.Group` → `Member` → `Read`) and, as a precaution, in
`OdtInlineReader.Walk` (nested spans): now held by `OdtReadContext.MaximumDepth` (64), the same cap as `OdtBlockReader`.
Test: `OdfDeepGroupTests` (ODP and ODT: test process brought down by a stack overflow before, green after).

Third round on `Presentation/`:

| File | Lines read/total | Outcome |
|---|---|---|
| `Presentation/Writing/Foundation.cs` | 95/95 | no defects |
| `Presentation/Writing/StyleBuilders.cs` | 139/139 | no defects |
| `Presentation/OdfHeaderFooter.cs` | 159/159 | no defects |
| `Presentation/OdfPrintMasters.cs` | 185/185 | **medium**: the notes body size (`pulse:font-size` or the `…-notes` style) was read without a cap: `NaN`, `Infinity`, `1e300`, `0`, `-5` entered the model and in PPTX became out-of-schema `sz` (`int` overflow); fixed at the root in `NotesMaster.FontSize` (finite, between 1 and 4000 pt, otherwise 12) |
| `Presentation/Reading/SlideReading.cs` | 259/259 | no defects |

| `Presentation/Reading/PageReaders.cs` | 321/321 | no defects (`Position` without `z-index` is quadratic in the number of shapes: slow, not dangerous) |
| `Presentation/Reading/StyleTables.cs` | 323/323 | **low**: `draw:opacity="NaN%"` on the background image passed `Math.Clamp` and left `Opacity = NaN` in the model (image drawn with undefined opacity, PPTX without `alphaModFix`); fixed at the root in `BackgroundImageLayout.Opacity` (between 0 and 1, `NaN` → 1) |
| `Presentation/Writing/DocumentWriters.cs` | 323/323 | no defects |
| `Presentation/Writing/ElementWriters.cs` | 338/338 | no defects |
| `Presentation/OdfMasterPages.cs` | 430/430 | no defects (the opacity of the master's background image goes neither into ODP nor into PPTX, but it cannot be changed from the interface) |

Test: `OdpPageAuditTests` (six cases red before, green after). `Presentation/` read in full.


### Spreadsheet

| File | Lines read/total | Outcome |
|---|---|---|
| `Spreadsheet/OdfDynamicArrays.cs` | 18/18 | no defects |
| `Spreadsheet/OdsGroupedObjects.cs` | 20/20 | no defects |
| `Spreadsheet/OdsLinkedPictures.cs` | 23/23 | no defects |
| `Spreadsheet/OdsDefaultCellStyle.cs` | 28/28 | no defects |
| `Spreadsheet/OdfCalculationSettingsMapper.cs` | 50/50 | **high**: the iteration's `table:steps` read without a cap: an ODS with `steps="2147483647"`, iteration active and a non-converging circular reference kept the engine calculating for two billion steps on opening (same hole from XLSX's `iterateCount`, where a `uint` beyond `int.MaxValue` became negative); fixed at the root in `CalculationSettings.MaxIterations` (between 1 and `MaximumIterations` = 32767, Excel's maximum) |

Test: `OdsCalculationSettingsAuditTests` (red before, green after).
| `Spreadsheet/OdsViewSettingsMapper.cs` | 68/68 | no defects |
| `Spreadsheet/SheetBindings.cs` | 80/80 | no defects |
| `Spreadsheet/OdsObjectLinks.cs` | 83/83 | no defects |
| `Spreadsheet/TrackedChanges.cs` | 122/122 | **medium**: the address of a recorded change (`table:row`, `table:column`) was bounded only from below: a row 5,000,000 or a column 2,000,000,000 entered the log as changes on cells the sheet does not have (accepting or rejecting them wrote off the sheet, and the change's mere `ToString` threw); fixed: invalid addresses are discarded |
| `Spreadsheet/ExternalData.cs` | 156/156 | no defects |

Test: `OdsSpreadsheetAuditTests` (two cases red before, green after).
| `Spreadsheet/StreamingContent.cs` | 164/164 | no defects |
| `Spreadsheet/PageStyles.cs` | 269/269 | **medium**: `style:scale-to` read without an upper cap (`Infinity%`, `1e300%` became an infinite print scale, `1%` below the minimum of the reference suite, Excel and our dialog); fixed: between 10 and 400 %, like the interface and the XLSX writer. `scale-to-X`/`scale-to-Y` (fit to N pages) stay uncapped, harmless |
| `Spreadsheet/SheetWriting.cs` | 373/373 | **open, not fixed**: `OdsRowIndex` adds to the index every cell of every validation up to the last used row; a validation on the whole sheet (`A1:XFD1048576`, common from XLSX) with data at row 100,000 requires 1.6 billion insertions on save. The real fix is writing the empty validated cells as repeated cells, i.e. restructuring the row writer: left to a separate block |

Test: `OdsSpreadsheetAuditTests` (three scale cases red before, green after).
| `Spreadsheet/SheetPartReaders.cs` | 643/643 | **high**: two header rows with `number-rows-repeated="2000000000"` made `Sum` (checked) throw `OverflowException`: the ODS did not open at all; and the row and column indices in `TableRows`/`TableCells`, in streaming loading and in column widths were added without checks and turned negative beyond `int.MaxValue`. Fixed at the root: `TableRows.Advance` saturates the index, repeated print rows and columns have the sheet's cap. **Medium**: an array formula anchored at the bottom of the sheet got a range beyond the last row or column; now the range stops at the sheet edge, like merges |

Test: `OdsSpreadsheetAuditTests` (two cases red before, green after; full `PulseOffice.Formats.Odf.Tests` suite green, 604 tests). `Spreadsheet/` read in full.


### Shapes

| File | Lines read/total | Outcome |
|---|---|---|
| `Shapes/OdfCellEnd.cs` | 16/16 | no defects |
| `Shapes/OdfFillImageInliner.cs` | 19/19 | no defects |
| `Shapes/OdfExtrusion.cs` | 28/28 | **low**: `draw:extrusion-depth` read without a cap (`1e300cm`, `Infinitycm` became `int.MaxValue` twips, `NaNcm` zero depth); fixed: between 0 and 100 cm, otherwise 1 cm |
| `Shapes/OdfLiteralEnhancedPath.cs` | 42/42 | no defects |
| `Shapes/OdfFontworkGeometry.cs` | 55/55 | no defects |
| `Shapes/OdfArcGeometry.cs` | 64/64 | **medium**: `draw:start-angle`/`end-angle` `NaN`, `Infinity` or `1e308grad` entered the model as a non-finite angle (undrawable arc, out-of-schema PPTX/XLSX `adj`); fixed: a non-finite angle goes back to the default value |
| `Shapes/OdfComputedGeometry.cs` | 100/100 | no defects |
| `Shapes/OdfTableCellLooks.cs` | 104/104 | **medium**: `fo:font-size` of a Deck table cell without a cap (`NaN`, `0`, `1e300`); fixed: between 1 and 4000 pt |
| `Shapes/SheetImages.cs` | 104/104 | no defects |
| `Shapes/Geometry.cs` | 122/122 | **medium**: `rotate (NaN)` or `rotate (Infinity)` in `draw:transform` left `RotationDegrees = NaN` in the model; fixed in `OdfDrawRotation.Read` (non-finite rotation = 0). Note: `EnhancedPathWriter` ignores arcs, but all callers flatten first (`ArcFlattener`) or pass paths without arcs: not reachable |
| `Shapes/SheetShapeWriting.cs` | 171/171 | no defects |
| `Shapes/SheetPrimitiveShapeReader.cs` | 129/129 | no defects |
| `Shapes/OdfShapeTextRuns.cs` | 154/154 | **medium**: size of a shape text run without an upper cap (`1e300pt`, `Infinitypt` entered the model; `NaN` and negatives were already filtered by `ShapeTextEditing`); fixed: between 1 and 4000 pt |
| `Shapes/SheetShapeReading.cs` | 342/342 | **high**: `draw:g` nested without limit in a sheet: the recursive reading of `SheetGroupShapeReader` overflowed the stack and closed the process on opening the ODS; fixed: beyond 64 levels the inner groups are discarded, as in `OdpGroupFlattener` |
| `Shapes/DeckObjectCodecs.cs` | 727/727 | **high**: `table:table-row-group` nested without limit in a Deck table (recursive `ExpandedRows`): same stack overflow; fixed with the same cap of 64 levels |

Test: `OdfShapesAuditTests` (all red before — the two nesting ones closed the test process —, green after). `Shapes/` read in full (2,177 lines).

### TextCodecs

| File | Lines read/total | Outcome |
|---|---|---|
| `TextCodecs/OdfLanguageCodec.cs` | 47/47 | no defects |
| `TextCodecs/OdfTextFrames.cs` | 143/143 | inner spacings read with `OdfValues.Length`: covered by the fix below |
| `TextCodecs/OdfFieldElements.cs` | 42/42 | no defects |
| `TextCodecs/OdfTextReading.cs` | 239/239 | **high**: paragraph with elements nested without limit (span, `text:a`, `text:meta`…) in Deck text: recursive reading (`OdfParagraphText.Append`, `RunReader.Inline`, deep copies of `XElement`) overflowed the stack and closed the process on opening the ODP; fixed: beyond 64 levels the paragraph is read as plain text (`IOdfParagraphText.Shallow`) and text collection is iterative. **Medium**: bullet `fo:font-size` `Infinity%`/`1e300%` became an infinite relative size; fixed: finite, up to 400 % |
| `TextCodecs/OdfTextStyleTables.cs` | 122/122 | **medium**: paragraph and span font size without a cap (`NaN`, `Infinity`, `1e300pt`, negative, zero); fixed: between 1 and 4000 pt, otherwise inherited; a percentage span no longer becomes 18 pt. Line spacing `Infinity%`/`1e300%` and spacings `NaNcm`/`Infinitycm`/`1e300cm` entered non-finite: fixed at the root in `OdfValues` (outside the area, bare minimum: finite lengths within ±10,000 pt, line spacing up to 1000 %, non-finite points = default value) |
| `TextCodecs/OdfFontFaceNames.cs` | 31/31 | no defects |
| `TextCodecs/OdfParagraphText.cs` | 154/154 | **high** (see above): recursive `Append` made iterative; `Read` was quadratic (ancestors of every descendant) and is now a linear visit |
| `TextCodecs/OdfDateStyleCodec.cs` | 181/181 | **high**: `number:decimal-places="2000000000"` on the seconds of a time field requested a string of two billion `f` (memory exhausted on opening); fixed: at most 7 decimals, like .NET. **Low**: `MMMMM` (or `ddddd`) in a field format became the day; fixed: the longest token of the same letter |
| `TextCodecs/OdfTextWriting.cs` | 299/299 | no defects (values arrive finite from reading) |
| `TextCodecs/OdfTabStops.cs` | 38/38 | **low**: negative tab position accepted; fixed: not below zero |

Test: `OdfTextCodecsAuditTests` (all red before — the nesting closed the test process —, green after; `PulseOffice.Formats.Odf.Tests` suite green, 637 tests). Remaining, outside the area: writing an XML with 50,000 levels is quadratic in `XElement.Save` (prefix resolution), only in the test; `SlideReading.cs:193` still reads the font size with `OdfValues.Points` without an upper cap.

### CellStyles

| File | Lines read/total | Outcome |
|---|---|---|
| `CellStyles/Naming.cs` | 169/169 | **high**: a cell style name with `_d800_` (surrogate) made `Decode` throw `ArgumentOutOfRangeException`: the ODS did not open; fixed: surrogates are not decoded |
| `CellStyles/NumberSectionComposer.cs` | 25/25 | no defects |
| `CellStyles/Properties.cs` | 337/337 | **medium**: `style:rotation-angle` `NaN`/`Infinity` gave `int.MinValue` on x64 (zero on ARM: it depended on the processor); fixed: non-finite angle ignored. **Medium**: border width without a cap (`1e300pt`, `Infinitypt`, negative); fixed: between 0 and 72 pt. **Medium**: cell font size `Infinity%`, `1e300%`, `1e300pt` entered huge or infinite; fixed: at most 4000 pt. **Low**: a language made only of separators (`-`) made the writer throw; fixed |
| `CellStyles/NumberFormatLocale.cs` | 48/48 | no defects |
| `CellStyles/NumberStyles.cs` | 701/701 | **high**: `number:display-factor="Infinity"` made it throw `OverflowException` (the ODS did not open) and `1e300` added a hundred commas to the code; fixed: finite factor, at most 5 commas. **Medium**: `number:denominator-value` and the text of `number:currency-symbol` entered the code unfiltered: `;` and `]` injected sections and colours into the format (and from there into XLSX); fixed: denominator digits only (at most 9), symbol without `[ ] ; "`. **Medium**: a format with two elapsed hours (`[h]… [hh]`) made saving throw (`Duplicate attribute`); fixed |

Test: `OdfCellStylesAuditTests` (red before, except rotation and `NaN` border, green on ARM because of the conversion's behaviour; green after).

### ShapeStyles

| File | Lines read/total | Outcome |
|---|---|---|
| `ShapeStyles/Reading.cs` | 296/296 | **medium**: fill, line, shadow and glow opacity outside 0–1 (`-50%`, `1e300%`) or non-finite (`NaN%`, `Infinity%`); fixed with `OdfMeasure.ParseFraction`. **Medium**: shape text size without a cap or minimum (`NaN`, `Infinity`, `1e300pt`, negative); fixed: between 1 and 4000 pt. **Medium**: text and hatch rotation, border and gradient stop positions non-finite or out of range (writing turned them into `int.MinValue` on x64); fixed. **Low**: arrow scale without a cap (a one-kilometre marker on a thin line = a scale of millions); fixed: between 0.1 and 10 |
| `ShapeStyles/DashPatterns.cs` | 71/71 | **high**: `draw:dots1`/`draw:dots2` `1e300`/`Infinity` became two billion dashes per repetition (endless drawing loops), infinite relative lengths; fixed: at most 100 dashes, relative lengths up to 100 times the width, non-negative |
| `ShapeStyles/Writing.cs` | 243/243 | no defect of its own (values now arrive finite) |
| `ShapeStyles/Vocabulary.cs` | 143/143 | **medium**: `ParsePercent`, `ParseNumber` and `OdfGradientAngles.Parse` accepted `NaN` and `Infinity`: fixed at the root, a non-finite value goes back to the default. **Open, low**: colours (`draw:fill-color`, `svg:stroke-color`…) enter without validation; `Colour` puts them back in `#rrggbb` form but a non-hexadecimal value becomes a meaningless colour |
| `ShapeStyles/Inheritance.cs` | 43/43 | no defects (cycles stopped by `seen`) |

Test: `OdfShapeStylesAuditTests` (15 cases, all red before, green after).

### Charts and Conditional

| File | Lines read/total | Outcome |
|---|---|---|
| `Charts/ChartReading.cs` | 214/214 | **medium**: in the inline data of Deck charts a shorter row or a series appearing late shifted the values of the following rows under the wrong categories (series were not filled with the missing ones); `table:number-columns-repeated` ignored; `1e999` entered as infinity. Fixed: every series has a value for every category (missing = `NaN`), repeated cells expanded up to 1024 columns, non-finite values = missing. **Low**: every series looked up its own style by walking all styles of the document (quadratic on charts with many series); now an index (`ChartStyleIndex`) built once |
| `Charts/ChartDocuments.cs` | 298/298 | no defects |
| `Charts/OdfChartVocabulary.cs` | 114/114 | no defects |
| `Conditional/RuleCodecs.cs` | 333/333 | **medium**: `NaN`/`Infinity` thresholds of bars, scales and icons entered the model (and XLSX as `cfvo val="NaN"`); fixed: a non-finite value counts as 0. The custom icon index has no cap but the catalogue bounds it: harmless |
| `Conditional/ConditionForms.cs` | 114/114 | **medium**: read/write asymmetry of condition text: a comma inside a text (`contains-text("a,b")`) split the operand in two, inner quotes were neither doubled nor read doubled (as the reference suite writes them), `1,000` was written without quotes (`NumberStyles.Any`) and became two operands. Fixed with `OdfConditionLiterals`: arguments split respecting quotes, `""` as a quote, numbers only in invariant form |

Tests: `OdfChartsAuditTests`, `OdfConditionalAuditTests` (red before, green after; suite green, 678 tests).

### Forms and Tables

| File | Lines read/total | Outcome |
|---|---|---|
| `Forms/Controls.cs` | 175/175 | no defect of its own (numbers go through `FormPropertyBag`) |
| `Forms/Vocabulary.cs` | 74/74 | no defects |
| `Forms/Settings.cs` | 189/189 | **high**: an inverted range (`ScrollValueMin` > `ScrollValueMax`) entered `NumericRange`, whose `Math.Clamp` throws `ArgumentException` as soon as the control is used; negative or zero step accepted; fixed: ordered bounds, positive step. **Medium**: numeric properties without a cap or finiteness check (`FontHeight`, `MaxTextLen`, `DecimalAccuracy`, `LineCount`, `RepeatDelay`: `NaN`, `1e300`, negatives became `int.MinValue` or huge); enumerations (`Align`, `Border`, `ButtonType`, `Orientation`, `ImageScaleMode`) converted without checking that the value exists; `EchoChar` beyond `char.MaxValue` or a surrogate. Fixed with `SettingBounds` and `EnumSetting<T>` |
| `Tables/CellValueCodec.cs` | 39/39 | **medium**: `office:value` read with `NumberStyles.Any` (`1,000` = 1000, `(5)` = -5, currency symbols) and without a finiteness check (`NaN`, `Infinity`) in the values of scenarios and recorded changes; fixed: invariant form, finite values only |
| `Tables/TableCells.cs` | 72/72 | **high**: row or column groups nested without limit (`table:table-row-group`) read by iterator recursion: the stack overflowed and closed the process on opening the ODS; fixed with an iterative visit. The same overflow was in `OutlineAxis` (`OdfLayoutMapper.cs`, outside the area, bare minimum for the test to pass): that one is now iterative too and the index saturates |
| `Tables/AnchoredFrames.cs` | 30/30 | no defects |
| `Tables/OdfBooleanValue.cs` | 15/15 | **low**: `NaN` counted as true; fixed |
| `Tables/OdfLinkSyntax.cs` | 21/21 | no defects (verified: `C:\…` and `\\server\…` stay absolute also on macOS, because `Uri` recognizes them as `file:`) |
| `Tables/OdfHrefs.cs` | 13/13 | no defects (paths are resolved only inside the package) |
| `Tables/QualifiedRanges.cs` | 80/80 | no defects |

Tests: `OdfFormsAuditTests`, `OdfTablesAuditTests` (red before — the nesting closed the test process —, green after; suite green, 691 tests). **Open, outside the area**: `OdfScenarioMapper.Read` repeats the `for offset < cell.Repeat` loop up to `int.MaxValue` times for a cell with a huge `number-columns-repeated` (freeze on opening) and ignores repeated rows; it must be bounded to the width of the scenario's range.

### Validation

| File | Lines read/total | Outcome |
|---|---|---|
| `Validation/ValidationForms.cs` | 263/263 | **low**: in `cell-content-is-in-list` an entry like `"a"&"b"` (concatenation) passed as the literal text `a"&"b`; fixed: only a string without undoubled inner quotes is literal, otherwise the list stays a formula |

Test: `OdfValidationAuditTests` (the concatenation case red before, green after; suite green, 693 tests).

### Summary of the round (TextCodecs, CellStyles, ShapeStyles, Charts, Conditional, Forms, Tables, Validation)

Read in full 38 files, 5,416 lines. Defects: **high** 9 (nesting in Deck text, second decimals, surrogates in style names, infinite scale factor, uncapped dashes, inverted control range, nested row groups in `TableCells` and `OutlineAxis`, a `[h]…[hh]` format that blocked saving — counted among the medium ones in the detail); **medium** about 20; **low** 7. All fixed, except the declared open ones: shape colours without validation (low), `OdfScenarioMapper` with uncapped column repetitions (outside the area), `SlideReading.cs:193` without a cap on the size (outside the area). No new loss code (ODF245–254 not used): no loss between formats emerged. Touched outside the area, minimally: `OdfValues.cs` (finite lengths, points and line spacing) and `OdfLayoutMapper.cs` (iterative `OutlineAxis`). Live tests to do after the merge: open in Deck an ODP produced by the reference suite with bulleted lists, formatted date/time fields, custom dashes and charts; in Ledger an ODS with form controls (scroll bar), formats with currency and fractions, «contains text» conditional formatting with commas.

## Round of the files at the root of `PulseOffice.Formats.Odf`

| File | Lines read/total | Outcome |
|---|---|---|
| `OdfScenarioMapper.cs` | 103/103 | **high**: a cell with a huge `number-columns-repeated` made reading loop up to `int.MaxValue` times (freeze on opening); **medium**: `number-rows-repeated` ignored (scenario rows lost). Fixed: repeated rows and columns bounded to the scenario's range, cap of 2^20 cells |
| `OdfLength.cs` | 35/35 | no defects |
| `OdfUnits.cs` | 51/51 | no defects |
| `OdfTableStyleMapper.cs` | 52/52 | **low**: `tab-color` accepted without validation (any text became the tab colour and went back into the file); fixed. Note: the style lookup walks all styles for every sheet (linear per sheet, acceptable) |
| `OdfDeckChartMapper.cs` | 54/54 | no defects (path normalized by `OdfHrefs`, read only inside the package) |
| `OdfValues.cs` | 87/87 | **medium**: `Seconds` and `XmlDuration` accepted negative, infinite or huge durations (`-5s`, `1e300s` → negative duration or `int.MaxValue`); `PT1M30S` was not read. Fixed: finite, non-negative durations, at most one hour; full ISO form via `XmlConvert` |
| `Presentation/Reading/SlideReading.cs:193` (outside the round, reported) | — | **medium**: placeholder body size without a cap (0, negative, `1e300`); fixed with `OdfFontSizeBounds` (1–4000 pt), as elsewhere |
| `OdfConditionalMapper.cs` | 57/57 | no defects |
| `OdfShapeStyleMapper.cs` | 57/57 | no defects |
| `OdfDeckShapeMapper.cs` | 66/66 | no defects |
| `OdfInteractionMapper.cs` | 66/66 | no defect of its own (running the referenced macro goes through the macro sandbox) |
| `OdfSignatureLineCodec.cs` | 71/71 | no defects |
| `OdfVersionMapper.cs` | 79/79 | **medium** (security): the title of a version read from the file became a package entry name (`Versions/../x`), and on save a path leaving the folder was rewritten (zip slip in the produced file). Fixed: name without `/`, `\\`, `:`, `.`/`..`, on reading and on writing |
| `OdfQueryBindingMapper.cs` | 82/82 | **low**: negative row count of the last query accepted; fixed (≥ 0). Note outside the area: SQL and connection of an untrusted file with `refresh=OnOpen` — to verify that the refresh on opening asks for confirmation (Ledger, data composition) |
| `OdfShapeMapper.cs` | 91/91 | no defects |
| `OdfStyleMapper.cs` | 92/92 | no defects |
| `OdfProtectionMapper.cs` | 93/93 | no defects (the unknown digest is kept with the ODF prefix) |
| `OdfFormulaSyntax.cs` | 99/99 | no defects (regular expressions with a timeout) |
| `OdfChartMapper.cs` | 102/102 | no defects (object read only inside the package) |
| `OdfMetaMapper.cs` | 104/104 | **medium** (time zone): creation and modification dates written with the document's local time but without an offset, and read back as the machine's local time: opening the file in another time zone changed the instant. Fixed: the offset is written (`xsd:dateTime` with `zzz`), which the reference suite accepts |
| `OdfFormulaMapper.cs` | 112/112 | no defects |
| `OdfSparklineMapper.cs` | 125/125 | **medium**: infinite or huge line width accepted (infinite pen when drawing); **low**: colours not validated. Fixed: finite width 0.01–100 pt, colours `#RRGGBB` or the default |
| `OdfMediaMapper.cs` | 128/128 | **low**: `VolumeDB` = `NaN` gave volume `NaN`; fixed (full). Files linked outside the package stay links, as in the reference suite |
| `OdfSheetMetadataMapper.cs` | 135/135 | **high**: two `table:content-validation` with the same name made `ToDictionary` throw and the file did not open; fixed (the first wins). **Medium**: repeated rows ignored (the validation covered only the first row) and repeated columns up to `int.MaxValue` off the sheet; fixed: range extended to the repeated rows and bounded to the sheet |
| `OdpWriter.cs` | 137/137 | no defects |
| `OdfMacroMapper.cs` | 142/142 | no defect of its own (sources read only from the package; the signature is verified elsewhere) |
| `OdfLayoutMapper.cs` | 151/151 | no defects (`OutlineAxis` already iterative since the previous round) |
| `OdfFunctionNames.cs` | 158/158 | no defects |
| `OdfImageMapper.cs` | 172/172 | no defect of its own (the SVG read is cleaned with `SvgSanitizer` at rasterization time) |
| `OdfFormControlMapper.cs` | 179/179 | no defects (duplicate declarations grouped) |
| `OdfImageMapCodec.cs` | 179/179 | **low**: non-finite measures (`1e400cm`) gave infinite points in the map area; fixed: non-finite measure = 0, area with non-finite points discarded |
| `OdfAnimationBehaviourCodec.cs` | 203/203 | **high**: `anim:par`/`anim:seq` groups nested without limit read by recursion: the stack overflowed and closed the process on opening the ODP; fixed: beyond 64 levels nodes are discarded |
| `OdfFontFaces.cs` | 208/208 | **low**: weight of the embedded font without limits (negative, 99999); fixed (1–1000), without a dedicated test |
| `OdfAnimationMapper.cs` | 214/214 | no defect of its own (`XElement.Parse` of the native preset prohibits DTDs; durations and repetitions already bounded) |
| `OdfDatabaseRangeMapper.cs` | 227/227 | **medium**: an invalid, negative or out-of-range `field-number` became column 0 or a column outside the filter (the filter silently changed column); fixed: the condition is discarded. **Low**: «top/bottom N» count `NaN`, infinite or negative accepted; fixed (10) |
| `OdsReader.cs` | 244/244 | **low**: unlike `OdpReader`, `ArithmeticException` (for example the `OverflowException` of a conversion) was not among the exceptions turned into the `ODF003` diagnostic, and escaped the reader; added |
| `OdpReader.cs` | 247/247 | no defect of its own |
| `OdsFormat.cs` | 253/253 | no defects (static read-only descriptors: data, not behaviour) |
| `OdfPivotMapper.cs` | 290/290 | **low**: infinite numeric grouping step (`Infinity`, `1e400`) accepted; fixed (finite and positive) |
| `OdsWriter.cs` | 556/556 | **medium** (performance): for every cell written the list of all validations of the workbook was rebuilt (`ValidationNameFor`), cost cells × validations with one allocation per cell; fixed with `OdfSheetValidationNames`, built once per sheet |

Verified, already in order: cap on the list start in `Odt/OdtStylesReading.cs:321` and `Odt/OdtBodyReading.cs:105` (0–65535).
Tests: `OdfRootAuditTests`, `OdpPageAuditTests` (scenario and durations/bodies red before, green after).
