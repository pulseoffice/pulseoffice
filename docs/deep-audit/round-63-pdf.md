# Deep audit round 63: PDF

Area: `src/PulseOffice.Formats.Pdf`, `src/PulseOffice.Security/PdfSigning.cs`, the TrueType subsetter, the PDF export prompt.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| Formats.Pdf/PdfFile.cs | 133/133 | read |
| Formats.Pdf/PdfEncoding.cs | 125/125 | read |
| Formats.Pdf/PdfExporter.cs | 204/204 | read |
| Formats.Pdf/PdfMatrix.cs | 17/17 | read |
| Formats.Pdf/PdfContentCanvas.cs | 476/476 | defects |
| Formats.Pdf/PdfImages.cs | 130/130 | read |
| Formats.Pdf/PdfNavigation.cs | 181/181 | defects |
| Formats.Pdf/AssemblyMarker.cs | 3/3 | read |
| Formats.Pdf/PdfArchive.cs | 187/187 | defects |
| Formats.Pdf/PdfSecurity.cs | 287/287 | read |
| Formats.Pdf/PdfFontMetrics.cs | 171/171 | read |
| Formats.Pdf/Fonts/PdfFontSet.cs | 245/245 | read |
| Formats.Pdf/Fonts/PdfFontResources.cs | 239/239 | defects |
| Security/PdfSigning.cs | 474/474 | defects |
| UI.Controls/Printing/PdfExportPrompt.cs | 41/41 | read |
| UI.Controls/Printing/PdfExportView.cs | 411/411 | read |
| Typography/Subsetting/TrueTypeSubsetter.cs | 240/240 | defect |

There is no PDF import and no PDF/A beyond PDF/A-2b; tagged PDF is not claimed. 17 files, 3,996 lines, all read in full.

## Defects

| # | Severity | Where | Defect | Status |
|---|---|---|---|---|
| 1 | Medium | PdfSigning.cs `PdfIncrementalUpdate.Finish` | The incremental-update trailer dropped `/Info` and `/ID` from the previous trailer | Fixed: `PdfTrailer.Carried` copies them |
| 2 | Medium | PdfSigning.cs `Sign` | Signing an encrypted PDF wrote plain-text strings and a trailer without `/Encrypt`, which broke the file | Fixed: an encrypted file is refused with `InvalidOperationException`, and the file is left untouched |
| 3 | Medium | PdfSigning.cs `Read`/`Slots` | Signatures from other producers were not seen: an exact `/SubFilter /adbe.pkcs7.detached` and `/ByteRange [` were required | Fixed: tolerant regexes, `ETSI.CAdES.detached` accepted too |
| 4 | Minor | PdfSigning.cs `Date` | The signature date ignored its time-zone offset | Fixed: full PDF date parser |
| 5 | Medium | PdfNavigation.cs `Destinations` | Name-tree keys were sorted by their escaped token and not by byte value (`(a!)` before `(a)`), so lookups failed in strict viewers | Fixed |
| 6 | Minor | PdfNavigation.cs `Outline` | `/Count` included the descendants of closed items | Fixed |
| 7 | Medium | PdfContentCanvas.cs `DrawGlyphs`/`DrawText`/`Select`, PdfNavigation `Number` | A NaN or infinite size, position, rotation, scale or heading top wrote `NaN` into the content stream, which makes the page unreadable | Fixed |
| 8 | Minor | PdfContentCanvas.cs `Round` | The default `ToString` can produce an exponent | Fixed: `0.###` |
| 9 | Minor | PdfFontResources.cs `BaseFont` | The subset tag used `string.GetHashCode` (randomized per process), so the output was not deterministic | Fixed |
| 10 | Minor | PdfArchive.cs `Identifier` | The `/ID` held a random GUID even with `Created` fixed, so the output was not deterministic | Fixed |
| 11 | Medium | TrueTypeSubsetter.cs | A glyph index outside the table was skipped, which shifted every later CID, so wrong glyphs were drawn | Fixed: placeholder slot |

## Tests

`StrictPdfStructure` (xref, offsets, /Size, stream lengths, references, non-finite numbers) runs on plain, compressed, PDF/A and encrypted files; `PdfRound63Tests`; `RobustnessTests.UnGlifoFuoriDallaTabellaTieneIlSuoPostoNelSottoinsieme`; three new `PdfSignerTests`. Every new test failed before the fix (checked by reverse-applying the patch). Formats.Pdf.Tests 62/62, Security PdfSigner+Round24 16/16, Typography 104/104, Architecture 40/40.

## Remaining

- No complex shaping (Arabic and Indic text in isolated forms, ZWJ emoji split apart); colour emoji fonts without outlines are refused with a fallback. This is a typesetting limit.
- Classic PDF 1.7 with no object streams or xref streams (valid).
- Signing over an xref stream produces a hybrid classic section with `/Prev`; a catalogue inside an object stream is not found (clear error). The signature widget is not in the page `/Annots` (invisible signature).
- RGB+alpha images only (no CMYK or 16-bit images in the model).

## Live checks

Export to PDF, PDF/A and password-protected PDF and open each in Preview and Acrobat (bookmarks, links, text selection and search, PDF/A-2b preflight). Sign one of our PDFs and one from another program, then check the signature panel in Acrobat.
