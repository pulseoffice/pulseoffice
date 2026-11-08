# Deep audit: `PulseOffice.Formats.Package` and `PulseOffice.Formats`

Method and rules as in [odf.md](odf.md). Package tests in `tests/PulseOffice.Formats.Odf.Tests` (`OdfPackageHardeningTests`, `OdfEncryptionTests`).

## `PulseOffice.Formats.Package`

| File | Lines read/total | Outcome |
|---|---|---|
| `OdfPackage.cs` | 183/183 | **medium** (compression bombs): `ReadBytes`, `ReadXml`, `OpenEntry` and `ReadMimeType` read an entry in full without a cap. The intake check (`IncomingDocumentInspector`) applies only to files opened from the interface and not to packages produced by decryption (`encrypted-package` wrapper), legacy conversion or flat. Fixed at the root: every read goes through `BoundedReadStream`, a cap of 512 MiB per entry (configurable with `OpenRead(stream, largestEntry)`), `mimetype` read for at most 256 characters. Verified that .NET already truncates an entry to its declared size: a false size does not exceed the cap |
| `BoundedReadStream.cs` | new | stream that throws `InvalidDataException` beyond the limit (exceptions are already translated into diagnostics by the readers) |
| `XmlPartLoader.cs` | 29/29 | no defect (DTDs ignored, no resolver, entities limited) |
| `XmlCharacterFilter.cs` | 63/63 | no defect (surrogate pairs preserved, non-XML characters removed) |
| `FlatOdfUnpacker.cs` | 161/161 | no defect (DTDs forbidden, 512 MiB at most, invalid base64 discarded) |
| `Encryption/OdfPackageEncryption.cs` | 308/308 | no defect of its own: entries read with the cap in the stream too (`BoundedReadStream`), decrypted package limited to 1 GiB, inflation limited; the nested `encrypted-package` package is now covered by the `OdfPackage` cap |
| `FlatOdfPacker.cs` | 165/165 | no defect (packs our just-written package; references resolve only inside the archive) |
| `Encryption/OdfCrypto.cs` | 206/206 | **medium** (denial of service): every encrypted entry has its own salt and its own derivation; with PBKDF2 up to 10 million iterations or Argon2 up to the memory cap *per entry*, a package with thousands of entries kept the opening busy for hours. Fixed in `OdfPackageEncryption` with `OdfDerivationBudget` (sum over all entries: 2^28 PBKDF2 iterations, 2^28 KiB·passes Argon2; the reference suite's default values fit with thousands of entries); beyond the budget the status is `Unsupported` |
| `Encryption/OdfDerivationBudget.cs` | new | injectable budget |
| `Encryption/OdfEncryptionData.cs` | 83/83 | no defect (invalid parameters → `InvalidDataException`) |
| `Encryption/OdfUnlockKey.cs` | 74/74 | no defect |
| `Encryption/OdfProtection.cs` | 88/88 | no defect (starting key zeroed where needed; PBKDF2-SHA1 at 100 000 iterations is what the reference suite reads in ODF 1.2) |
| `Legacy/LegacyOdfUpgrader.cs` | 169/169 | **low** (bombs): each entry was limited to 256 MiB but the total was not (many large entries → unbounded memory); added the 1 GiB cap on the converted package, as in decryption. Without a dedicated test (it would need more than 1 GiB of memory) |
| `Encryption/OpenPgp/BouncyCastleOpenPgp.cs` | 198/198 | no defect (only integrity-protected data, plaintext ≤ 4 KiB, key files ≤ 16 MiB, buffers zeroed) |
| `Encryption/OpenPgp/OpenPgpManifestKeys.cs` | 54/54 | no defect |
| `Encryption/OpenPgp/OpenPgpModel.cs` | 42/42 | no defect |
| `Legacy/LegacyVocabulary.cs` | 85/85 | no defect |
| `Legacy/LegacyXmlTranslator.cs` | 486/486 | **high**: `Remap` copied the tree by recursion: a legacy `content.xml` (`.sxc`, `.sxi`) nested tens of thousands of levels deep overflowed the stack and closed the process on opening; fixed with an iterative copy that preserves node order |
| `Encryption/Argon2id.cs` | 240/240 | no defect (limited parameters: passes ≤ 64, lanes ≤ 64, memory ≤ 1 GiB per derivation; the total is now under `OdfDerivationBudget`) |
| `Encryption/Blake2b.cs` | 95/95 | no defect |
| `Encryption/Blowfish.cs` | 116/116 | no defect (key 1–56 bytes) |

`PulseOffice.Formats.Package` read in full: 18 files, 2,940 lines.

## `PulseOffice.Formats`

| File | Lines read/total | Outcome |
|---|---|---|
| `SvgSanitizer.cs` | 86/86 | **medium** (security): external references in CSS were removed with a regular expression on `url(` and `@import`, but CSS escapes (`\75 rl(http://…)`, `@\69mport`, `u\72l(`) bypassed it and the SVG engine would have decoded them. Fixed: in CSS values (`style` element and attributes) backslashes are removed before the filter |
| `LinkedFiles.cs` | 61/61 | no defect of its own. **To verify, outside the area**: whoever calls `Load` on a link read from an untrusted document (UNC paths `\\host\…` on Windows expose NTLM credentials on first access) |
| `Intake/DocumentIntake.cs` | 54/54 | no defect |
| `Intake/DocumentBuffer.cs` | 38/38 | no defect (1 GiB cap also for non-seekable streams) |
| `Intake/IntakeServiceCollectionExtensions.cs` | 17/17 | no defect |
| `Intake/IncomingDocumentInspector.cs` | 160/160 | no defect (entries, total expansion, compression ratio, entities, depth 256) |
| `Intake/IncomingDocumentLimits.cs` | 20/20 | no defect |
| `LambdaParameterQualifier.cs` | 200/200 | **high**: nested `LET`/`LAMBDA` qualified by recursion (`Qualify` → `Call` → `Qualify`), with a scan up to the closing parenthesis at every level: a formula with thousands of nested `LET`s overflowed the stack on saving (ODS and XLSX go through `FormulaDialect.FromInternal`) and the cost was quadratic. Fixed: beyond 64 levels the rest of the call is copied as is |
| `FormulaDialect.cs` | 68/68 | no defect |
| `MediaTypes.cs` | 61/61 | no defect |
| `VectorFallback.cs` | 66/66 | no defect (maximum PNG side 2048 px) |
| `DocumentFormatRegistry.cs` | 107/107 | no defect (every read goes through the intake check) |
| `SmilTransitionMapper.cs` | 116/116 | **low**: `ToSmil` with a transition type without a row (for example `None`) threw `InvalidOperationException` (`First`); callers avoid it today, but a new type would have made saving fail; fixed: fade as fallback |
| `NativeTransitionTable.cs` | 90/90 | no defect (rows are re-parsed at every call: negligible cost, 40 rows) |
| `AnimationAttributeTable.cs` | 87/87 | no defect (linear expressions) |
| `AnimationFilterTable.cs` | 69/69 | no defect |
| `TextFrameLayer.cs` | 65/65 | no defect |
| `FunctionNameTable.cs` | 39/39 | **high**: function prefixes (`COM.MICROSOFT.` and the other vendor prefixes) were removed by recursion, one per level: a name with tens of thousands of repeated prefixes overflowed the stack on opening the ODS (and the XLSX, same table); fixed with a loop, same semantics |
| `SvgRecolour.cs` | 33/33 | no defect (DTDs forbidden) |
| `AltTextMapper.cs` | 55/55 | no defect |
| `SignatureLineImage.cs` | 46/46 | **medium**: the fallback PNG of the signature line was requested at the full size of the frame (up to millions of pixels per side with a huge frame read from the file), with the bitmap allocated on saving; fixed with a maximum side of 2048 px, like `VectorFallback` |
| `SpreadsheetFormat.cs` | 51/51 | no defect |
| `TextDocumentFormat.cs` | 51/51 | no defect |
| `SlideGroupWriting.cs` | 43/43 | no defect |
| `TextDocumentInterchange.cs` | 34/34 | no defect (reading goes through the intake check) |
| `SpreadsheetFormatRegistry.cs` | 32/32 | no defect |
| `ArcAdjustments.cs` | 32/32 | no defect |
| `SlideAssembly.cs` | 31/31 | no defect |
| `PresentationFormatRegistry.cs` | 28/28 | no defect |
| `FontworkWarps.cs` | 26/26 | no defect |
| `TokenMap.cs` | 24/24 | no defect |
| `SlideBackgroundNormalizer.cs` | 20/20 | no defect |
| `PresentationFormat.cs` | 20/20 | no defect |
| `OverlappingMerges.cs` | 20/20 | no defect of its own. **Outside the area (Sheets)**: `MergedRegions` is a list: `Add` and `At` are linear, so reading *n* merges costs O(n²) and `OdsWriter` calls `Merges.At` for every cell written (cells × merges); the model needs a spatial index |
| `ChartLabelFit.cs` | 20/20 | no defect |
| `ConstantFormulas.cs` | 18/18 | no defect |
| `AnimationClickGroups.cs` | 17/17 | no defect |
| `SheetNamePortability.cs` | 15/15 | no defect |
| `FidelityRule.cs` | 13/13 | no defect |
| `ParagraphMargins.cs` | 11/11 | no defect |
| `Imaging/JpegEncoding.cs` | 11/11 | no defect |

`PulseOffice.Formats` read in full: 41 files, 2,055 lines.
