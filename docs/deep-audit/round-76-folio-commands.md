# Deep audit round 76: Folio command files

Scope: `src/PulseOffice.Documents.UI/Commands` and the editing helpers they call.

## Already covered

Rounds 4 and 5 (`folio-ui-round4.md`, `folio-ui-round5.md`, `folio.md`) read all 53 command files in full on
2026-11-04/05 (commit fc4ea63). This round re-reads in full only the files that changed since then (13 files, from
`git diff --stat fc4ea63 HEAD`) plus `FolioCommandSet.cs` (base class) and the tracked-editing code they reach.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| FolioCommandSet.cs | 43/43 | ok |
| FolioObjectLinkCommands.cs | 55/55 (never audited) | ok: locked objects excluded, same-editor check after the dialog |
| FolioChartCommands.cs | 38/38 | ok |
| FolioMasterCommands.cs | 210/210 | ok (insertions go through `editor.Change`, guard nets protection) |
| FolioTextToolCommands.cs | 189/189 | **D1, D2 fixed** |
| TrackingDocumentEditor.cs | 98/98 | **D1 fixed** |
| TrackedEditing.cs | 230/230 | D1 fix lives here |
| FolioMailMergeCommands.cs | 196/196 | **D4 fixed** |
| FolioContentControls.cs | 219/219 | ok (forms-only filling, control protection, re-check after dialogs) |
| FolioAutoTextCommands.cs, FolioFieldCommands.cs, FolioInsertCommands.cs, FolioListCommands.cs, FolioObjectInsertCommands.cs, FolioTableCommands.cs, FolioTableDialogs.cs | changed hunks since fc4ea63 (rest read in rounds 4/5) | ok; all new text keys present in the five languages |
| EditGuard.cs, Subdocuments.cs | read for D3 and R1 | |

## Defects

- **D1 (serious)** `TrackingDocumentEditor.TransformText`: with change recording on, a text transform (change case,
  Chinese conversion, Hangul/Hanja) was recorded as a *format* change that silently replaced the text. The review
  panel showed a format change, DOCX/ODT carried no trace of the old text, and rejecting it after a reload could not
  give the text back. Fixed at the root: `ITrackedEditing.ReplaceText` records each changed span as a tracked
  insertion (inserted after the old text, so it inherits the run's formatting) plus a tracked deletion, in one undo
  step. Tests: `TrackedTextTransformTests` (Documents.Tests).
- **D2 (serious, privacy)** `FolioTextToolCommands` redaction (`folio.tools.redact`, `folio.tools.autoRedact`) went
  through the tracking editor: with recording on, the redacted text survived in a revision (format-change snapshot,
  and after D1 a deletion). Redaction now writes through `IDirectDocumentEditor`, never as a revision; conversions
  still go through the tracking editor. Tests: `FolioTextToolTests` (two new cases).
- **D3 (medium)** With tracked-changes protection (`DocumentRestriction.TrackedChanges` or a change-protection key)
  redaction can neither be recorded (it would keep the text) nor bypass recording (it would break the protection):
  `folio.tools.redact` and `folio.tools.autoRedact` are now disabled when `IEditGuard.ForcesRecording`, and re-check
  after their dialogs. Test: `OscurareNonSiPuoQuandoLeRevisioniSonoImposteDallaProtezione`.
- **D4 (minor)** `FolioMailMergeCommands` took `IExternalQueryGate?` with a fail-open `UnattendedExternalQueryGate`
  fallback: a composition without the gate would run a document's stored SQL without consent. The gate is now a
  required dependency (registered in `DataAccessComposition`).

## Residual (not fixed)

- **R1 (medium)** `folio.insert.subdocument` and `folio.tools.updateLinks` (`Subdocuments.Insert/Refresh`) do not
  become revisions with recording on; `folio.insert.document` does (it pastes). Marking a whole linked section as
  inserted needs tracked regions across a section container, which the revision model does not cover yet.
- **R2 (minor, outside the area)** `LedgerExternalDataCommands`, `QueryRefresher` and `PivotSourceRefresher` keep the
  same fail-open optional gate as D4.

## Tests

Documents.Tests 351/351 (3 new), Documents.UI.Tests 355/355 (3 new), Architecture.Tests 40/40.
