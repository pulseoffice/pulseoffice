# Deep audit: shell (Shell, Desktop, Printing, Formats.Pdf)

Previous round on the branch: d2d858f0 (Interchange), 58fca88e (Shell), without a complete coverage log.

## Coverage (read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Desktop/Program.cs | 69/69 | log initialized before everything; no defect |
| src/PulseOffice.Desktop/App.axaml.cs | 269/269 | minor: async handlers of Activated and OpenStartupAsync without try/catch (l. 116, 132), covered by InterfaceFaultGuard |
| src/PulseOffice.Shell/ShellSettings.cs | 196/196 | no defect |
| src/PulseOffice.Shell/RecentDocuments.cs | 169/169 | corrupt file tolerated; no defect |
| src/PulseOffice.Shell/DocumentRecovery.cs | 262/262 | severe: see D1 |
| src/PulseOffice.Shell/UnsavedChangesGuard.cs | 111/111 | no defect |
| src/PulseOffice.Shell/DocumentWorkspace.cs | 233/233 | listeners unhooked on close; no defect |
| src/PulseOffice.Shell/RecoveryCoordinator.cs | 109/109 | severe: see D1 |
| src/PulseOffice.Shell/AutosaveCoordinator.cs | 79/79 | no defect |
| src/PulseOffice.Shell/InterfaceFaultGuard.cs | 75/75 | absorbs the dispatcher's exceptions, emergency copies; no defect |
| src/PulseOffice.Shell/DocumentArrivals.cs | 109/109 | medium: see D3 |
| src/PulseOffice.Shell/DocumentOpenRouter.cs | 94/94 | medium: see D2 |
| src/PulseOffice.Shell/DocumentPasswordGate.cs | 204/204 | key zeroed after use, 16 MB cap on the key file; no defect |
| src/PulseOffice.Shell/CommandExecutor.cs | 91/91 | guards closed on every branch, listener unhooked in the finally; no defect |
| src/PulseOffice.Shell/QuitCoordinator.cs | 59/59 | no defect |
| src/PulseOffice.Shell/QuitGate.cs | 27/27 | re-entry prevented by _asking; no defect |
| src/PulseOffice.Shell/ModuleDocumentService.cs | 365/365 | minor: an encrypted template opened as new (ReadAsync) does not go through the password gate and fails with the generic error (left) |
| src/PulseOffice.Shell/ShellWindow.cs | 567/567 | per-tab closing protected by _closing; decorations not touched (as instructed); no defect |
| src/PulseOffice.Shell/StartView.cs | 506/506 | minor, fixed: the folder /Users/mario2 became "~2" (home prefix without a separator boundary) |
| src/PulseOffice.Shell/DocumentSharing.cs | 166/166 | no defect |
| src/PulseOffice.Shell/SignedDocumentTracker.cs | 105/105 | no defect |
| src/PulseOffice.Shell/DocumentOperations.cs | 96/96 | no defect |
| src/PulseOffice.Shell/DocumentTabsView.cs | 128/128 | minor D26, fixed |
| src/PulseOffice.Shell/ApplicationMenu.cs | 76/76 | no defect |
| src/PulseOffice.Shell/CommandPaletteSource.cs | 56/56 | no defect |
| src/PulseOffice.Shell/CommandRecorder.cs | 83/83 | JS strings escaped; no defect |
| src/PulseOffice.Shell/DocumentFileService.cs | 29/29 | no defect |
| src/PulseOffice.Shell/DocumentFormatFamily.cs | 31/31 | no defect |
| src/PulseOffice.Shell/ExchangeDragDrop.cs | 75/75 | no defect |
| src/PulseOffice.Shell/FormDesignModeOnOpen.cs | 24/24 | no defect |
| src/PulseOffice.Shell/LanguageCoordinator.cs | 50/50 | no defect |
| src/PulseOffice.Shell/OutlinePresentation.cs | 8/8 | no defect |
| src/PulseOffice.Shell/RecentCommands.cs | 87/87 | no defect |
| src/PulseOffice.Shell/RecentDocumentsCoordinator.cs | 68/68 | goes through the router (dedup D2); no defect |
| src/PulseOffice.Shell/ShellPresenter.cs | 12/12 | no defect |
| src/PulseOffice.Shell/SignedSaveGuard.cs | 37/37 | no defect |
| src/PulseOffice.Shell/StatusZoomSource.cs | 26/26 | no defect |
| src/PulseOffice.Shell/TemplateLibrary.cs | 85/85 | minor: preview cache without a cap (one entry per modification date), left |
| src/PulseOffice.Shell/UnattendedOpening.cs | 32/32 | no defect |
| src/PulseOffice.Shell/UserIdentitySettings.cs | 43/43 | no defect |
| src/PulseOffice.Shell/WindowPlacementCoordinator.cs | 73/73 | no defect |
| src/PulseOffice.Shell/WindowPlacementStore.cs | 67/67 | non-finite values discarded; no defect |
| src/PulseOffice.Shell/About/AboutDialog.cs | 35/35 | no defect |
| src/PulseOffice.Shell/About/AboutView.cs | 70/70 | no defect |
| src/PulseOffice.Shell/Exchange/HtmlTableGrids.cs | 46/46 | no defect |
| src/PulseOffice.Shell/Navigation/ModuleNavigatorSource.cs | 78/78 | no defect |
| src/PulseOffice.Shell/Navigation/ModuleReviewSource.cs | 106/106 | no defect |
| src/PulseOffice.Shell/StylePanels/ModuleStylesSource.cs | 51/51 | no defect |
| src/PulseOffice.Shell/Surfaces/CommandSurfaceFactory.cs | 257/257 | no defect |
| src/PulseOffice.Desktop/Composition/ShellComposition.cs | 222/222 | no defect |
| src/PulseOffice.Desktop/Composition/ShellWindowComposition.cs | 100/100 | singletons without per-document state; no defect |
| src/PulseOffice.Desktop/Composition/ShellPasswordCommands.cs | 52/52 | severe: see D4 |
| src/PulseOffice.Desktop/Composition/ShellDocumentCommands.cs | 320/320 | severe: see D5 |
| src/PulseOffice.Desktop/Composition/ShellCommands.cs | 329/329 | severe: see D6, D7; minor D8 |
| src/PulseOffice.Desktop/Composition/ShellPrintCommands.cs | 309/309 | no defect |
| src/PulseOffice.Formats.Pdf/PdfEncoding.cs | 125/125 | no defect (WinAnsi only for the standard fonts; Unicode text goes through the embedded fonts) |
| src/PulseOffice.Formats.Pdf/PdfExporter.cs | 204/204 | no defect |
| src/PulseOffice.Formats.Pdf/PdfFile.cs | 133/133 | UTF-16BE text strings outside ASCII, dates with time zone; no defect |
| src/PulseOffice.Formats.Pdf/PdfArchive.cs | 168/168 | medium: see D9 |
| src/PulseOffice.Formats.Pdf/Fonts/PdfFontSet.cs | 245/245 | fsType permissions respected, per-character fallback, missing U+ declared (PDF012); no defect |
| src/PulseOffice.Formats.Pdf/Fonts/PdfFontResources.cs | 239/239 | complete ToUnicode, tagged subset; no defect |
| src/PulseOffice.Formats.Pdf/PdfContentCanvas.cs | 476/476 | no defect |
| src/PulseOffice.Formats.Pdf/PdfNavigation.cs | 181/181 | no defect |
| src/PulseOffice.Formats.Pdf/PdfImages.cs | 130/130 | no defect |
| src/PulseOffice.Formats.Pdf/PdfSecurity.cs | 287/287 | compliant AES-256 R6 (hash 2.B, Perms, random IVs); no defect |
| src/PulseOffice.Formats.Pdf/PdfMatrix.cs | 17/17 | no defect |
| src/PulseOffice.Formats.Pdf/PdfFontMetrics.cs | 171/171 | minor: widths of the 14 standard fonts approximated beyond ASCII (€ measured as a space), left |
| src/PulseOffice.Printing/PrintJob.cs | 317/317 | medium: see D10 |
| src/PulseOffice.Printing/PageCanvas.cs | 183/183 | minor: see D12 |
| src/PulseOffice.Printing/ClippedPageCanvas.cs | 151/151 | medium: see D11 |
| src/PulseOffice.Printing/ScaledPageCanvas.cs | 73/73 | medium: see D11 |
| src/PulseOffice.Printing/CompositePrintDocument.cs | 44/44 | no defect |
| src/PulseOffice.Printing/TextWrapper.cs | 71/71 | minor: a very long word measured prefix by prefix (quadratic), left |
| src/PulseOffice.Printing/BandPaginator.cs | 80/80 | no defect |
| src/PulseOffice.Printing/HeaderFooterFields.cs | 51/51 | no defect |
| src/PulseOffice.Formats.Pdf/AssemblyMarker.cs | 3/3 | no defect |
| src/PulseOffice.Desktop/Composition/ShellSendCommands.cs | 178/178 | no defect |
| src/PulseOffice.Desktop/Composition/ShellTemplateCommands.cs | 132/132 | minor: "Save as template" overwrites a template with the same name without asking, left |
| src/PulseOffice.Desktop/Composition/ShellRemoteCommands.cs | 230/230 | medium: see D13 |
| src/PulseOffice.Desktop/Composition/ShellSignatureCommands.cs | 256/256 | medium: see D14 |
| src/PulseOffice.Desktop/Composition/ShellMacroRecorderCommands.cs | 110/110 | minor: stopping the recording with no document with macros active, the recorded steps are lost without warning, and they are saved into the document active at the moment of stopping, left |
| src/PulseOffice.Desktop/Composition/TextReviewEditWatch.cs | 42/42 | minor D25, fixed |
| src/PulseOffice.Desktop/Composition/SharingProviders.cs | 57/57 | no defect |
| src/PulseOffice.Desktop/Composition/ShellScannerCommands.cs | 147/147 | medium: see D15 |
| src/PulseOffice.Desktop/Composition/ShellFormCommands.cs | 69/69 | no defect |
| src/PulseOffice.Desktop/Composition/LedgerToolsComposition.cs | 71/165 (only lines 95–165) | Ledger singletons without per-document state (WorkbookCheckpoints, AutomaticRefreshHistory, SheetSnapshots verified); no defect |
| src/PulseOffice.Desktop/Composition/ShellInterfaceCommands.cs | 185/185 | no defect |
| src/PulseOffice.Desktop/Composition/ShellFontCommands.cs | 83/83 | no defect |
| src/PulseOffice.Desktop/Composition/ShellAutoCorrectCommands.cs | 60/60 | no defect |
| src/PulseOffice.Desktop/Composition/ShellDictionaryCommands.cs | 31/31 | no defect |
| src/PulseOffice.Desktop/Composition/CommandingComposition.cs | 140/140 | no defect |
| src/PulseOffice.Desktop/Composition/DataAccessComposition.cs | 80/80 | no defect in this branch; it still registers `OracleDatabaseProvider`, removed on main by 13e89fdb: at merge time main's version wins |
| src/PulseOffice.Desktop/Composition/DeckDesignComposition.cs | 166/166 | no defect |
| src/PulseOffice.Desktop/Composition/DeckEditingComposition.cs | 187/187 | no defect |
| src/PulseOffice.Desktop/Composition/DocumentFormatsComposition.cs | 125/125 | no defect; note: CSV reader built with `new CsvValueReader()` and `new CsvSniffer(…)` instead of the registered `ICsvNumberParser` |
| src/PulseOffice.Desktop/Composition/DrawingComposition.cs | 149/149 | no defect |
| src/PulseOffice.Desktop/Composition/FontComposition.cs | 111/111 | no defect (Folio's font collector registered in FolioServiceCollectionExtensions) |
| src/PulseOffice.Desktop/Composition/LedgerCoreComposition.cs | 115/115 | no defect |
| src/PulseOffice.Desktop/Composition/LedgerDataComposition.cs | 156/156 | no defect |
| src/PulseOffice.Desktop/Composition/LedgerToolsComposition.cs | 94/165 (lines 1–94; lines 95–165 read in the previous round) | no defect; now read in full |
| src/PulseOffice.Desktop/Composition/ObjectsAndScriptingComposition.cs | 109/109 | no defect (macro dispatcher with a read-only guard) |
| src/PulseOffice.Desktop/Composition/PlatformIntegrationComposition.cs | 64/64 | no defect |
| src/PulseOffice.Desktop/Composition/PrintingAndCloudComposition.cs | 114/114 | no defect |
| src/PulseOffice.Desktop/Composition/ProofingComposition.cs | 154/154 | no defect; note: the dictionary sources use `TimeProvider.System` and not the registered one |
| src/PulseOffice.Desktop/Composition/ScannerServiceProvider.cs | 27/27 | no defect |
| src/PulseOffice.Desktop/Composition/SecretVaultProvider.cs | 25/25 | no defect |
| src/PulseOffice.Desktop/Composition/SettingsComposition.cs | 90/90 | no defect |
| src/PulseOffice.Desktop/Composition/SharedControlsComposition.cs | 106/106 | no defect |
| src/PulseOffice.Desktop/Composition/SignatureComposition.cs | 70/70 | no defect |
| src/PulseOffice.Desktop/Composition/RichClipboardFormatsProvider.cs | 22/22 | no defect |
| src/PulseOffice.Desktop/Composition/VideoEncoderProvider.cs | 25/25 | no defect |
| src/PulseOffice.Desktop/Composition/WindowChromeProvider.cs | 22/22 | no defect |
| src/PulseOffice.Desktop/Composition/PulseMenuLayout.cs | 848/848 | minor D27 |
| src/PulseOffice.Desktop/Composition/PulseRibbonLayout.cs | 720/720 | no defect (visibility and position rules without duplicates or shadowing) |
| src/PulseOffice.Desktop/Composition/PulseContextLayout.cs | 105/105 | no defect (a command in several context menus is intended) |

## Defects

- **D1 (severe, fixed)** — `RecoveryCoordinator.cs:80`: after recovery the recovery copy was deleted immediately,
  and the recovered document received a new identifier. A second crash before saving or before the next
  autosave (up to 10 minutes) lost the recovered work. Now `DocumentRecovery.RestoreAsync` adopts
  the copy's identifier (the copy stays on disk, removed only from `Pending`) and the copy is discarded when the document
  is saved or closed. Test: `DocumentRecoveryTests.IlDocumentoRipristinatoTieneLaSuaCopiaFinoAlSalvataggio`.

- **D2 (medium, fixed)** — `DocumentOpenRouter.cs:51`: the "already open" check looked only at the documents already in the
  workspace; two requests for the same file while the first was still in progress (password prompt, slow read,
  double click on the recents, agent) opened two copies of the same document. Now the router keeps the paths being opened.
  Test: `DocumentOpenRouterTests.Due_aperture_dello_stesso_file_mentre_la_prima_e_in_corso_ne_aprono_uno_solo`.
- **D3 (medium, fixed)** — `DocumentArrivals.cs:76`: the filter caught only IO/access/operation/not supported; a
  broken file throwing something else (InvalidDataException, XmlException) interrupted the opening of the following files and ended up
  in the dispatcher. Now it is logged and processing continues (except out of memory and cancellation).
  Test: `DocumentArrivalsStartupTests.Un_file_rotto_fra_quelli_arrivati_non_impedisce_di_aprire_gli_altri`.
- **Minor App.axaml.cs:116 and :132 (verified)** — the exceptions of the asynchronous handlers go back to Avalonia's context and
  reach `Dispatcher.UnhandledException`, which `InterfaceFaultGuard` absorbs; with D3 opening files no longer throws them.

- **D4 (severe, fixed)** — `ShellPasswordCommands.cs:39`: "Protect with password" on a DOCX/XLSX/PPTX document
  (or with a save that does not write, for example cancelled at the signature confirmation or failed for IO on an
  unmodified document) announced "password set" with a positive outcome and left the protection on the document, while the
  file on disk stayed in the clear. Now the command verifies on the saved file (`IDocumentPasswordGate.IsProtected`) that the
  encryption matches the choice; otherwise it restores the previous protection and announces nothing (the save's
  "ODF only" warning remains). Test: `ShellPasswordCommandsTests`.
- **D5 (severe, security, fixed)** — `ShellDocumentCommands.cs:309`: to open or compare a version saved
  in the document its content was written to `Path.Combine(temporary folder, version.Name + extension)`; the name
  comes from the file (`VersionList.xml`, a title that need only match a `Versions/…` entry of the package, and a
  ZIP entry may be called `Versions/../../…`), so a hostile document could make arbitrary bytes be written outside the
  folder. Now the name goes through `SafeFileName` (separators, forbidden and control characters replaced, names made only of
  leading dots rejected, length cap). Test: `SafeFileNameTests` (before, `Path.Combine` with `../../…` escaped).
- **D6 (severe, fixed)** — `ShellCommands.cs:111` "Save a copy": it called "Save as" (which adopts the new
  path) and then put back the old path with `MarkSaved`, marking as saved a document whose original file
  had not been written; with the dialog cancelled it still cleared the modified flag and announced "copy
  saved". At the next close the changes were lost without a question. Now `IDocumentFileService.SaveCopyAsync`
  writes the copy without adopting it (dialog title "Save a copy" in the five languages) and the command announces only
  a written copy. Tests: `ModuleDocumentServiceTests.Salva_una_copia_…`, `ShellFileCommandsTests.Salva_una_copia_annullato_…`.
- **D7 (severe, fixed)** — `ShellCommands.cs:153` "Reload": it closed the active document without asking anything,
  losing the unsaved changes. Now it goes through `IUnsavedChangesGuard.ConfirmCloseAsync`.
  Test: `ShellFileCommandsTests.Ricarica_chiede_prima_di_buttare_le_modifiche`.
- **D8 (minor, fixed)** — `ShellCommands.cs:137` "Save all" also counted as saved the documents whose
  save had been cancelled or had failed.
- **D9 (medium, fixed)** — `PdfArchive.cs:65`: the XMP metadata escaped only `& < > " '`; a title or an author with
  control characters (the vertical tab U+000B that Word leaves in titles, NUL) produced XML that was not well
  formed: unreadable metadata and a non-compliant PDF/A. Now control characters become spaces and those not allowed in
  XML are removed. Test: `PdfMetadataTests`.
- **D10 (medium, fixed)** — `PrintJob.cs:82` `PageRangeParser`: a range written in the print dialog as
  `1-2147483647` (or `-2000000000`) made the loop run up to the given number, over two billion steps with the
  interface frozen, with an integer overflow at the end. Now the bounds are clamped to `0…pages+1` before the loop.
  Test: `PageRangeBoundsTests`.
- **D11 (medium, fixed)** — `ClippedPageCanvas`, `ScaledPageCanvas`: the two decorators did not forward `MarkHeading`,
  `MarkTarget`, `MarkLink`, `MarkNote` (the interface's empty default methods remained): whatever goes through a clipped
  or scaled canvas (drawing objects, fitted Ledger pages, imposition) silently lost the PDF's bookmarks,
  links and comments. Now they forward them, with transformed coordinates and clipped links.
  Test: `CanvasMarksForwardingTests`.
- **D12 (minor, fixed)** — `PageCanvas.cs:26` `PrintColor.FromHex` threw `FormatException` on non-hexadecimal
  digits (a malformed colour from a file): now black, as for the wrong length. Test: `PrintColorHexTests`.
- **D13 (medium, fixed)** — `ShellRemoteCommands.cs:81`: opening the same remote file from the server again
  downloaded it into a new temporary folder; the router's "already open" check looks at the local path, so there were
  two copies of the same remote document, and the second upload could overwrite the first. Now
  `IRemoteDocuments.Following` (outside the area, `src/PulseOffice.Remote`, bare minimum) finds the document already
  downloaded by connection and remote path and brings it to the front. Test: `RemoteDocumentsLookupTests`.
- **D14 (medium, fixed)** — `ShellSignatureCommands.cs:61`: "Digital signatures" signed the file on disk even with
  unsaved changes: the old version was signed while believing the one on screen was being signed, and the following save
  broke the signature. Now, as the reference suite does, it asks to save first. Test: `ShellSignatureCommandsTests`.
- **D15 (medium, fixed)** — `ShellScannerCommands.cs:130`: the scanned image (a long operation) was inserted into the
  document active at the end of the scan, not into the one it started from: switching tab in the meantime it ended up in the
  wrong document, even if read-only. Now it goes into the requesting document, if it is still open and editable.
  Test: `ShellScannerCommandsTests`.
- **DI registrations** — `ServiceConfiguration` builds with `ValidateOnBuild` and `ValidateScopes` (missing ones fail
  at startup). Double registrations of single services checked: IUserIdentity, IZoomLadder, IZoomChoice, IThemePalettes,
  IThemePaletteGallery, ISortLevelsDialog, IVersionTextDiff are `AddSingleton` in Desktop plus `TryAdd` in the modules, with the
  same implementation or with the shell's one winning: no defect.

- **D25 (minor, fixed)** — `TextReviewEditWatch.cs`: once the last document was closed, it stayed hooked to its history
  (the closed document was not collected) and the pending spelling review stayed open on a document that no longer
  existed. Now it also listens to `DocumentClosed`: it discards the review and moves to the active document, or to none. Test:
  `TextReviewEditWatchTests`.
- **D26 (minor, fixed)** — `DocumentTabsView.cs:87`: the "×" button of the tabs had only the tooltip, and a
  screen reader announced it as "×". Now it has the accessible name "Close "title"" (key `document.closeNamed`
  in the five languages). Test: `DocumentTabsViewTests`.

- **D27 (minor, fixed)** — `PulseMenuLayout.cs:550`: the rule `shell.scan.` → "Insert › Scanner" was dead, because
  the same rule at line 336 precedes it (the first wins). The dead-rule check skipped prefixes and wildcards by
  construction; now `CommandLayout.UnusedRules` also reports a prefix covered by a previous one, and
  `CompositionTests` also checks that there are no conflicting shortcuts (until now only written to the log
  at startup: none today). Tests: `CommandLayoutTests.UnPrefissoCopertoDaUnPrefissoPrecedenteEMorto`,
  `CompositionTests.Shell_commands_surfaces_and_the_three_modules_compose`.

## Typography (round of 2026-11-02)

| File | Lines read | Outcome |
|---|---|---|
| OpenType/EmbeddedOpenType.cs | 106/106 | no defect (EOT sizes checked before slicing) |
| MicroType/MtxLimits.cs | 20/20 | no defect |
| MicroType/MtxContainer.cs | 28/28 | no defect |
| MicroType/MicroTypeExpressDecoder.cs | 78/78 | no defect (index and cast exceptions translated into malformed data) |
| MicroType/FontBuffer.cs | 39/39 | no defect |
| MicroType/LzcompDecoder.cs | 115/115 | no defect (window and copies checked, output limited to 64 MB per block) |
| MicroType/AdaptiveHuffmanTree.cs | 140/140 | no defect |
| MicroType/BitReader.cs | 71/71 | no defect |
| MicroType/ByteSink.cs | 72/72 | no defect |
| MicroType/CtfFont.cs | 121/121 | no defect |
| MicroType/CtfCursor.cs | 106/106 | no defect |
| MicroType/SfntAssembler.cs | 77/77 | no defect |
| MicroType/TripletEncoding.cs | 43/43 | no defect |
| MicroType/GlyphDecoders.cs | 148/148 | no defect (points and components limited) |
| MicroType/GlyphProgramDecoder.cs | 87/87 | no defect |
| MicroType/CtfTableTranslators.cs | 93/93 | no defect |
| MicroType/PredictedMetricsTranslators.cs | 164/164 | medium D28 |
| OpenType/OpenTypeReader.cs | 209/209 | no defect (tables and faces limited, names only inside the table) |
| OpenType/BigEndianSource.cs | 51/51 | no defect |
| OpenType/MacRomanText.cs | 15/15 | no defect |
| OpenType/OpenTypeFace.cs | 88/88 | no defect |
| OpenType/FontFileExtractor.cs | 67/67 | minor D29 |
| OpenType/Metrics/FontTableData.cs | 55/55 | no defect (reads outside the data return zero) |
| OpenType/Metrics/GlyphCoverage.cs | 58/58 | no defect |
| OpenType/Metrics/FontMetricsFace.cs | 227/227 | medium D30 |
| OpenType/Metrics/PairKerning.cs | 254/254 | no defect (`kern` and GPOS tables read within bounds) |
| OpenType/Metrics/FontFileLocators.cs | 161/161 | minor D31 |
| Sources/FontSource.cs | 24/24 | no defect |
| Sources/FontServiceOptions.cs | 62/62 | no defect (HTTPS addresses only) |
| Sources/FontCacheStore.cs | 169/169 | no defect (folder names cleaned, atomic write) |
| Sources/CachedFontSource.cs | 100/100 | no defect |
| Sources/OnlineFontSource.cs | 137/137 | no defect (downloads HTTPS only, 32 MB per file, 64 MB per list) |
| Sources/GoogleFontsSource.cs | 138/138 | no defect (file name from METADATA.pb escaped in the URL) |
| Sources/FontsourceSource.cs | 79/79 | no defect |
| Sources/SystemFontSource.cs | 146/146 | no defect (unreadable folders already skipped) |
| Sources/DocumentFontSource.cs | 80/80 | no defect |
| Embedding/DocumentFontEmbedder.cs | 118/118 | no defect; note: after the `await` with `ConfigureAwait(false)` it assigns `document.Fonts` off the interface thread (a reference assignment, no damage seen) |
| Subsetting/TrueTypeSubsetter.cs | 235/235 | medium D32 |
| Catalog/ClassicFaceSelector.cs | 37/37 | no defect |
| Catalog/RecentFonts.cs | 54/54 | no defect (note: temporary file with a fixed name, two instances together can collide; the error is swallowed and only the last recent is lost) |
| Catalog/FontFamilyInfo.cs | 95/95 | no defect |
| Catalog/FontCatalog.cs | 138/138 | minor, left: `MergeAsync` updates `_stable` outside the semaphore; an `AcquireAsync` concurrent with `RefreshAsync` can lose the "downloaded" promotion until the next round (theoretical race, not reproduced) |
| Catalog/FontClassifiers.cs | 145/145 | no defect |
| Licensing/FontLicense.cs | 37/37 | no defect |
| Licensing/FontLicenseClassifier.cs | 71/71 | no defect |

Typography is read in full (45 files, 4,558 lines).

### Typography defects

- **D28 (medium, fixed)** — `PredictedMetricsTranslators.cs:145`: every group of a compressed VDMX table copied
  the whole rest of the table into a new array to read its bits: quadratic time and memory. An embedded font
  (EOT in a PPTX or DOCX) with 16,000 empty groups and an 8 MB tail required 128 GB of copies (5 s; with a 64 MB block,
  the maximum allowed, about 40 s per font). Now the bits are read on the original block. Test:
  `CompactTableTests.UnVdmxConMoltiGruppiVuotiNonRicopiaLaTabellaPerOgniGruppo`.
- **D29 (minor, fixed)** — `FontFileExtractor.cs:30`: extracting a face from a TTC collection allocated the sum
  of the declared lengths of the tables, which can overlap: 512 tables each covering a whole 1 MB file
  asked for 512 MB. Now a face larger than twice the file is rejected as invalid data. Test:
  `RobustnessTests.UnaCollezioneConTabelleSovrappostePerTuttoIlFileNonMoltiplicaLaMemoria`.
- **D30 (medium, fixed)** — `FontMetricsFace.cs:167`: the format 4 `cmap` was walked segment by segment without a
  cap: 32,000 overlapping segments from 0 to 65,534 are two billion steps, 6 s for every read of the metrics (font
  catalog, PDF export). Legitimate segments do not overlap, so now the walk stops after 65,536
  codes, just as format 12 already has its cap. Test: `RobustnessTests.SegmentiCmapSovrappostiNonCostanoMiliardiDiPassi`.
- **D31 (minor, fixed)** — `FontFileLocators.cs:47,129`: the search for font files for the metrics (PDF, Folio
  layout) used `SearchOption.AllDirectories`, which stops at the first unreadable subfolder: `IndexedFontLocator`
  discarded the whole font folder, `DirectoryFontLocator` threw the exception on every search. Now unreadable
  folders are skipped and the others are read. Test: `FontWidthChoiceTests.UnaSottocartellaIlleggibileNonNascondeICaratteriDelleAltre`.
- **D32 (medium, fixed)** — `TrueTypeSubsetter.cs:42`: the subset for the PDF copied the outline of every glyph from the
  `loca` without looking at the total; with overlapping positions (every even glyph covering the whole `glyf`) and a composite glyph
  referencing them all, a 200 KB embedded font produced 200 MB, and a larger one exhausted memory
  on PDF export. Now beyond twice the `glyf` (plus 64 KB) the font is rejected as invalid data and
  the export says so. Test: `RobustnessTests.GlifiChePuntanoTuttiAllIntereTabellaNonMoltiplicanoIlSottoinsieme`.

## Formats.Interchange (round of 2026-11-02)

Commit d2d858f0 of the previous round had no log: here the complete reading, file by file.

| File | Lines read | Outcome |
|---|---|---|
| Text/Rtf/RtfTokenizer.cs | 126/126 | no defect (words and parameters limited, `\bin` cut at the end of the data) |
| Text/Rtf/RtfDocumentReader.cs | 982/982 | medium D17, severe D16 (through the assembler) |
| Text/Html/HtmlTokenizer.cs | 147/147 | no defect (comments, scripts and styles skipped, attributes limited to 64) |
| Text/Html/HtmlDocumentReader.cs | 697/697 | severe D18; links only http, https, mailto, ftp, file and anchors (no `javascript:`), images only `data:` |
| Text/Assembly/TextDocumentAssembler.cs | 415/415 | severe D16 |
| CsvFormat.cs | 255/255 | medium D19 |
| CsvValues.cs | 87/87 | no defect (note: dates are tried with ~25 patterns rebuilt for every non-numeric field, slow but linear) |
| CsvSniffer.cs | 82/82 | no defect |
| CsvNumbers.cs | 52/52 | no defect (infinities and NaN rejected) |
| CsvOptions.cs | 27/27 | no defect |
| HtmlFormat.cs | 190/190 | no defect (texts and styles escaped; font name without quotes) |
| Text/Html/HtmlTableReader.cs | 305/305 | medium D20 |
| Text/Html/CssDeclarations.cs | 124/124 | minor D21 |
| Text/Html/HtmlCellBorders.cs | 46/46 | no defect (widths limited to 0.25–12 pt) |
| TabularSources.cs | 252/252 | severe D22; HTML with time-limited regular expressions, XML without DTD or resolver |
| ExternalCellValues.cs | 22/22 | no defect |
| Text/Plain/CodePages.cs | 26/26 | no defect |
| Text/Plain/TextEncodingDetector.cs | 66/66 | no defect; note: HTML does not look at `<meta charset>` (an HTML in Shift_JIS not valid as UTF-8 is read as 1252; from the clipboard HTML is always UTF-8) |
| Text/Plain/PlainTextFormat.cs | 123/123 | no defect |
| Text/Rtf/RtfPicture.cs | 68/68 | no defect |
| Text/Rtf/RtfState.cs | 103/103 | no defect |
| Text/Rtf/RtfVocabulary.cs | 237/237 | no defect (HYPERLINK field links filtered as in HTML) |
| Text/Rtf/RtfDocumentWriter.cs | 525/525 | medium D23 |
| Text/Html/HtmlDocumentWriter.cs | 567/567 | minor D24; minor remaining: a note inside a note is not written (the list is snapshotted before the walk) |
| Text/Epub/EpubDocumentWriter.cs | 216/216 | minor D24 |
| Text/Outline/ListNumbering.cs | 59/59 | no defect |
| Text/Outline/ParagraphSemantics.cs | 64/64 | no defect |
| Text/TextInterchangeFormats.cs | 45/45 | no defect |
| Text/TextInterchangeServiceCollectionExtensions.cs | 40/40 | no defect |
| AssemblyMarker.cs | 3/3 | no defect |

Interchange is read in full (30 files, 5,986 lines). Remaining as notes, not fixed: `file:` links read
from HTML and RTF are allowed as in the reference suite (on Windows a clicked `file://server/…` contacts that server);
`LedgerExchange.cs:65` (outside the area, Sheets.UI) parses the clipboard HTML in full just to know whether there is a
table, and then again to paste it.

### Formats.Interchange defects

- **D16 (severe, fixed)** — `TextDocumentAssembler.cs:93`: every text fragment with the same format as the previous one was
  appended by recreating the whole string (`Text + text`), and `Scope.Length` re-summed all the runs at every bookmark: quadratic
  time. A pasted HTML with `<i></i>a` repeated, or an RTF with `{}a` repeated, froze the app for minutes (400,000
  repetitions: 8 s; 600,000 RTF groups: 19 s). Now the run under construction is a `StringBuilder` and the length is kept
  in step. Tests: `HostileInputTests.MoltiFrammentiDiTestoConLaStessaFormaNonCostanoUnTempoQuadratico`,
  `MoltiGruppiRtfVuotiFraLeLettereNonCostanoUnTempoQuadratico`.
- **D17 (medium, fixed)** — `RtfDocumentReader.cs:471–494`: `\li`, `\ri`, `\fi`, `\sb`, `\sa`, `\expndtw`, `\charscalex`
  passed without a cap (up to 2·10⁹ twips, i.e. indents of hundreds of kilometres and scales of two billion percent) into the
  model and hence into the layout. Now limited to ±31,680 twips (22 inches, Word's maximum) and the scale to 1–600 %.
  Test: `HostileInputTests.RientriEScaleRtfSmisuratiVengonoLimitati`.
- **D18 (severe, fixed)** — `HtmlDocumentReader.cs:212`: every `<col span="1000">` added a thousand widths without
  limit: 200,000 `col` elements (5 MB of HTML from the clipboard) asked for 200 million entries and exhausted memory.
  Now the widths stop at 1024 columns, the table cap. Test:
  `HostileInputTests.MoltiElementiColConSpanEnormeNonEsaurisconoLaMemoria`.
- **D19 (medium, fixed)** — `CsvFormat.cs:155`: a text starting with `=`, `+`, `-`, `@` (or tab, newline)
  was exported as it is, and Excel or other programs execute it as a formula when opening the CSV (formula injection:
  `=HYPERLINK(…)`, `=cmd|…`). We do not alter the data (the reference suite does not, and PulseOffice reads the field back as text),
  but now `CheckFidelity` declares it with **ODF259**. Test: `CsvTests.UnTestoCheSembraUnaFormulaVieneSegnalatoPrimaDellEsportazione`.
- **D20 (medium, fixed)** — `HtmlTableReader.cs:113`: the cap of one million cells did not count empty rows: an
  HTML pasted into Ledger with millions of `<tr>` created a list for each one (hundreds of MB). Now the table is full
  also at 1,048,576 rows, the sheet maximum. Test: `HostileInputTests.MilioniDiRigheHtmlVuoteNonCresconoSenzaLimite`.
- **D21 (minor, fixed)** — `CssDeclarations.cs:82`: `em` was tried before `rem`, so `2rem` ended up in `em` with
  the number "2r" and returned nothing: measures in `rem` (common in website HTML) ignored. Test:
  `HostileInputTests.LeUnitaRemSiLeggonoComeLeEm`.
- **D22 (severe, fixed)** — `TabularSources.cs:198`: XML external data built rows × columns without a cap and
  searched the columns in a list (quadratic). A downloaded XML with 40,000 records, each with a different element,
  asked for 1.6 billion cells: almost two minutes and memory exhausted. Now the columns are searched in a set and beyond
  16,384 columns, 1,048,576 rows or ten million cells an `InvalidDataException` is raised, which the external data dialog
  already shows as an error. Test: `TabularSourcesTests.UnXmlConUnElementoDiversoPerRecordNonEsplodeInMemoria`.
- **D23 (medium, fixed)** — `RtfDocumentWriter.cs:381`: the instruction of DOCVARIABLE, REF and SEQ fields was written without
  escaping `\`, `{`, `}` and non-ASCII characters: a variable name with a curly brace broke the group structure
  (the rest of the paragraph ended up elsewhere) and accented letters became `?`. A hyperlink with `"` was
  truncated at the quotes; now it becomes `%22`. Tests: `HostileInputTests.LArgomentoDiUnCampoConParentesiNonRompeLRtf`,
  `UnCollegamentoConVirgoletteSopravviveAllRtf`.
- **D24 (minor, fixed)** — `HtmlDocumentWriter.cs:564`, `EpubDocumentWriter.cs:215`: the control characters that
  an RTF can bring into the text (`\u1`) ended up as they are in the XHTML and the EPUB, which became malformed XML and
  were rejected by readers. Now characters not allowed in XML are removed. Test:
  `TextEpubTests.CaratteriDiControlloNelTestoNonRendonoMalformatoLXhtml`.

## Remaining to be read

Nothing of the assigned area: Shell, Printing, Formats.Pdf, `src/PulseOffice.Desktop/Composition` (DI registrations and surface
data), Typography and Formats.Interchange are read in full. Outside the area, touched minimally:
`src/PulseOffice.UI.Commands/CommandLayout.cs` (rules covered by a previous prefix, D27), `src/PulseOffice.Shell/DocumentTabsView.cs`
(D26), `src/PulseOffice.Localization/Resources/*.json` (keys `document.closeNamed` and `diagnostic.odf259`).

### Remaining in the tests

`HeadlessIsolationTests` already fails before this round: `ShellFileCommandsTests`, `ShellPasswordCommandsTests`,
`ShellScannerCommandsTests`, `ShellSignatureCommandsTests` (written in the previous round) create Avalonia controls outside
the shared headless session; the dispatcher binds to the xUnit thread and in the complete Desktop.Tests suite it also makes
`ContextRefusalTests.In_una_sezione_protetta_scrivere_e_cambiare_elenco_si_rifiutano_con_un_motivo` fail (which
passes on its own). Moved into the session with `Session.Dispatch`, the first three pass, but `ShellScannerCommandsTests` stays
hung in the complete suite (it passes on its own): the fix was withdrawn and is still to be done. The new tests of this round
(`TextReviewEditWatchTests`) already run in the session.

## Live tests

Recovery after a crash: recover, force-quit the app before saving, restart: the copy must be offered again.
- Open the same file twice in a row (fast double click on the recents, or with an encrypted ODT while the
  password prompt is open): a single tab.
- Drag onto the window a broken file together with a good one: the good one opens, the broken one gives an error in the log.
- "Protect with password" on a DOCX: "ODF only" warning, no password-set message; on an ODT: encrypted file.
- "Save a copy" on a modified document: the copy is written, the tab stays modified with its path;
  cancelling the dialog changes nothing.
- "Reload" with changes: the save/discard/cancel question appears.
- Open a version from "Versions" in a document with unusual version names: it opens in the temporary folder.
- Export to PDF a document whose title contains a vertical tab (from Word): readable metadata (Document
  Properties in a PDF reader).
- Print with the range "1-2147483647": the dialog responds immediately.
- Reopen from the remote server a file already open: you go back to its tab.
- "Digital signatures" with unsaved changes: warning to save first.
- Long scan switching tab in the meantime: the image goes into the starting document.
- Paste into Folio a huge HTML copied from a website (many empty tags between the letters) and an RTF from Word: pasting stays
  fast; an HTML with thousands of `<col span>` does not freeze the app.
- Export to CSV a sheet with a text cell `=1+1` (typed with the apostrophe): the fidelity dialog warns
  that other programs may execute it as a formula.
- Export to RTF a document with a variable field whose name has curly braces, and reopen it: text intact.
- Export to EPUB a document pasted from an RTF with control characters: the book opens in Books/Calibre.
- Close the last document with the spelling panel open on an error: the panel closes.
- VoiceOver on the document tabs: the "×" button is announced as "Close "title"".
- Insert menu in Ledger: the scanner entry stays under "Image" (the "Scanner" rule was dead).
- External data from an XML with a different element for every record: error message, no hang.

