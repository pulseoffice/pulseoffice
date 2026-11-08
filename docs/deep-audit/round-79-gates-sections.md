# Deep audit round 79: required consent gates, tracked sub-documents

Leftovers R1 and R2 of [round 76](round-76-folio-commands.md).

## Coverage

| File | Read | Outcome |
|---|---|---|
| Data/ExternalQueryGate.cs | 54/54 | **R2 fixed** (allow-all gate removed from production) |
| Sheets.Queries/QueryRefresher.cs, PivotSourceRefresher.cs | constructors and gate use | **R2 fixed** |
| Sheets.UI/LedgerExternalDataCommands.cs | 1-80 | **R2 fixed** |
| Desktop/Composition/DataAccessComposition.cs | 49-95 | composition updated |
| Shell/DocumentRecovery.cs, Scripting.Editor/MacroEditorView.cs | optional security dependencies | fail closed, kept |
| Documents/Editing/Subdocuments.cs | 94/94 | **R1 fixed** |
| Documents/Revisions/TrackedEditing.cs, TrackingDocumentEditor.cs, RevisionResolver.cs, TrackedRows.cs | whole | **D1 fixed** |
| Documents/Clipboard/FragmentPaster.cs | whole | **D1, D2 fixed** |
| Documents/Editing/RangeDeletion.cs | 1-220 | read to check that rejecting a region ending at a section's end drops the section |
| Formats.OpenXml/Wordprocessing/DocxBlockProperties.cs | 276/276 | sections carried by bookmarks: tracked content round-trips |

## Sweep for optional security dependencies (R2)

Searched the whole solution for nullable constructor parameters of consent gates, policies, permissions, link followers,
destination policies and `?? new …` fallbacks. Findings:

- `QueryRefresher`, `PivotSourceRefresher` (`IExternalQueryGate? gate = null` with `?? new UnattendedExternalQueryGate()`)
  and `LedgerExternalDataCommands` (`IExternalQueryGate? = null`, grant skipped): **fail open**. Fixed.
- `UnattendedExternalQueryGate` (allows everything) lived in production code with no production user: removed; tests use
  their own doubles (`ConsentingQueryGate`, `RecordingQueryGate`).
- `IReadOnlySqlPolicy? readOnly = null` falls back to the strict `ReadOnlySqlPolicy`: fail closed, kept.
- `DocumentRecovery(IDocumentPasswordGate? passwords)`: without it protected documents are not snapshotted (`Unprotectable`):
  fail closed, kept.
- `MacroEditorView(IMacroWriteAccess? writeAccess)`: without it the grant button is absent: fail closed, kept.

## Defects

- **R2 (serious, security)** the external query consent gate was optional with an allow-all fallback in `QueryRefresher`
  and `PivotSourceRefresher`, and optional in `LedgerExternalDataCommands`: a composition missing the registration would
  run a document's stored SQL without consent. The gate is now a required constructor parameter (null throws), composed
  from `DataAccessComposition`; `CompositionTests.Without_the_external_query_gate_composition_fails_instead_of_allowing`
  removes the registration and proves resolution of each consumer (query refresher, pivot refresher, Ledger external
  data commands, Folio mail merge commands) throws.
- **D1 (serious)** pasting with change recording on was not recorded: `DocumentClipboard.Paste` called `IFragmentPaster`
  directly, past the tracking editor, so paste, `folio.insert.document` and every fragment insertion bypassed recording,
  including when protection forces recording. Restructured: `IDocumentEditor.Paste` (direct editor delegates to the
  paster, tracking editor to the new `ITrackedEditing.Paste`, which records the pasted span as one insertion);
  `DocumentClipboard` pastes through the editor. Tests: `TrackedPasteTests`.
- **R1 (medium)** `folio.insert.subdocument` and `folio.tools.updateLinks` were not recorded. With recording on:
  - inserting a sub-document records one insertion from the end of the anchor paragraph to the end of the section's last
    paragraph (the section and its paragraph marks); rejecting it removes the whole section, accepting keeps it;
  - refreshing a linked section records a tracked deletion of the old content and a tracked paste of the new one, in one
    undo step; accepting both leaves exactly the new content with its paragraph styles, rejecting both gives the old
    content back with its styles.
  No new change type is needed: the regions cross the section container, and `RangeDeletion` drops a section whose whole
  content a rejected region covers. DOCX writes them as `w:ins`/`w:del` on runs and paragraph marks inside the section
  (the section itself is carried by the block-properties bookmarks), ODT as `text:changed-region` with change marks inside
  the section. Tests: `SubdocumentTests` (7 new), `TrackedSubdocumentFormatTests` (ODT and DOCX round trip, accept and
  reject after reload).
- **D2 (minor)** pasting whole paragraphs into an empty paragraph gave the pasted first paragraph the empty paragraph's
  formatting (a heading pasted into an empty body paragraph became body text). An empty target now takes the pasted
  paragraph's properties, keeping only its own page and column break and page style (they belong to the place).

- **D3 (medium)** DOCX: a tracked deletion of several paragraphs at the start of a section dropped the section on reading:
  the reader joins the paragraphs whose marks are deleted under the last one's id, but the block-properties carrier
  bookmarks kept pointing at the joined (vanished) ids. `DocxParagraphMarkJoins.Retarget` now moves carrier starts and
  ends with the join. Found by `TrackedSubdocumentFormatTests`.

## Tests

Documents.Tests 361/361 (10 new), Formats.OpenXml.Tests 2389/2389 (2 new), Formats.Odf.Tests 802/802,
Sheets.Queries.Tests 34/34 (2 new), Data.Tests 61/61, Documents.UI.Tests 355/355, Sheets.UI.Tests 457/457,
Desktop.Tests `CompositionTests` 28/28 (4 new cases), Architecture.Tests 40/40.
