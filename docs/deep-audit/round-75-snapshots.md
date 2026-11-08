# Deep audit round 75: recovery snapshots off the UI thread

Follows the open point of [round 72](round-72-chrome-cost.md): the crash-recovery snapshot of a 500-page Folio document
cost about 350 ms of UI thread (13 MB of JSON) at every autosave, and Ledger and Deck serialized their native packages
on the UI thread too.

## Design

- **Two phases.** `IDocumentSnapshotProvider.Capture(OpenDocument)` runs on the UI thread and returns an
  `IRecoveryCapture` holding a private copy of the model; `IRecoveryCapture.WriteAsync(Stream)` serializes that copy on
  a background thread. `DocumentRecovery` captures, then runs serialization, encryption (password-protected documents),
  the temporary file, the atomic `File.Move` and the manifest inside `Task.Run`; it comes back to the caller's context
  only for the identifier bookkeeping and `Discard`. The serial queue of round 70 (concurrent snapshots never overlap
  or overwrite each other) and the atomic temporary-file swap are unchanged.
- **Copies by Prototype.** The models are mutable, so the capture is a structural copy that shares every immutable
  record and copies only the mutable containers:
  - Folio: the existing `TextDocument.Clone()` (paragraph contents are immutable `TextParagraph` records, shared).
  - Ledger: new `IWorkbookCopier`/`WorkbookCopier` in `PulseOffice.Sheets`; cells are immutable `Cell` records, so a
    sheet's cell dictionary is copied with the dictionary copy constructor (`Worksheet.CopyCellsTo`, internal) and the
    occupied-row/column indices with `OccupiedIndices.CopyTo`; catalogs gain `CopyTo` (`StyleCatalog`,
    `PageStyleCatalog`, `NumberFormatCatalog`), the rest uses existing mementos (`ChangeLog`, `ExternalDataCatalog`,
    `AutoFilterState`) and public adders. `WorkbookCheckpoints` (undo) is a partial in-place memento, not a full
    independent copy, so it is not a duplicate.
  - Deck: `Presentation.Copy()`, `Slide.Copy()`, `GridSettings.CopyTo` (elements, masters, themes, animations are
    records and are shared).
- **Smaller Folio JSON.** The codec no longer builds a `JsonNode` tree per level (each `SnapshotWriter.Put` used
  `SerializeToNode`, re-serializing every subtree once per nesting level): `SnapshotWriter` writes straight into the
  `Utf8JsonWriter`. `DefaultValueOmission` (a contract modifier) skips a property whose value equals the value a
  freshly constructed instance would get when the property is absent: it builds two prototypes per type (default and
  sample constructor arguments) and omits only properties whose default does not depend on the constructor arguments,
  never `required` ones; read-only computed properties (ignored on read) are not written. Text is written with the
  relaxed encoder (accents unescaped). No indentation anywhere, manifest included.

## Measurements (this Mac, Debug build)

| Case | Before (UI thread) | After: UI-thread capture | After: background serialization | Size |
|---|---|---|---|---|
| Folio, 500 pages (6,000 paragraphs) | 373 ms | 0.4 ms | 22 ms | 13.2 MB → 5.0 MB |
| Ledger, 1,000,000 cells | 2,808 ms | 16.5 ms | 1,772 ms | 6.05 MB (unchanged ODS) |
| Deck, 200 slides | 57 ms | 0.05 ms | 39 ms | 25 KB (unchanged ODP) |

## Tests

- `tests/PulseOffice.Shell.Tests/RecoveryBackgroundSerializationTests.cs`: the capture runs on the caller, the write is
  not awaited on it, an edit made while the write is pending does not reach the snapshot; the manifest has no
  indentation. Shell test providers moved to the new interface (`DelegateCapture`).
- `tests/PulseOffice.Documents.UI.Tests/FolioScaleTests.cs` (serial collection): capture under 60 ms, serialization
  under 1.5 s, file under 6 MB for 500 pages.
- `tests/PulseOffice.Documents.UI.Tests/FolioRecoveryCaptureTests.cs`: the clone writes byte-identical JSON; written →
  read → written is byte-identical (an omitted default that read back differently would show here); the capture is
  frozen against later edits; the same over a sample of the external ODT/DOCX corpus (skipped when the corpus is absent).
- `tests/PulseOffice.Documents.UI.Tests/FolioSnapshotCompactnessTests.cs`: defaults and computed properties are not
  written, accents are not escaped.
- `tests/PulseOffice.Sheets.UI.Tests/LedgerRecoverySnapshotCostTests.cs` (serial collection): a million-cell capture
  under 150 ms; the copy is independent of later edits (cells, merges, sheets, styles); every ODS part except
  `meta.xml` is identical whether written from the workbook or its copy, over the external ODS corpus.
- `tests/PulseOffice.Formats.Odf.Tests/DeckRecoveryCopyTests.cs`: 200-slide capture under 20 ms, independence from
  later edits, identical ODP parts from the copy over the external ODP corpus.

## Notes and limits

- Ledger's serialization of a million cells still takes about 1.8 s, now on a background thread; the ODS writer itself
  was not changed (out of scope).
- The native writers are singletons used both by saving (UI thread) and by the background snapshot; they keep no
  per-call state in fields (checked: `OdsWriter` builds a `ContentBuilder` per call, `OdpWriter`'s fields are stateless
  helpers).
- Folio documents whose body holds no paragraph are rejected by `FolioSnapshotValidator` on restore, as before; the
  corpus test skips them.
