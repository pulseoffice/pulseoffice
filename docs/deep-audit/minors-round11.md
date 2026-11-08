# Deep audit, round 11: the minor points left open

Scope: every point declared open, remaining or not fixed in `deck-round8.md`, `deck-round9.md`, `shared-round9.md`,
`ledger-round7.md`, `ledger-commands-round6*.md`, `folio-ui-round5.md`, `formats-round3.md`, plus the two limits of the
2026-11-05 auto-redaction entry of the work log. Changes are uncommitted in the worktree.

## Status of each point

| # | Source | Point | Status |
|---|---|---|---|
| 1 | deck-round9 | `TransitionEditor` «Browse»: async click handler, file-dialog faults unhandled | **Fixed at the root**: new shared decorator `ReportingFileDialogService` (UI.Controls) wraps the app's `IFileDialogService`; a fault of the system picker is logged and reported in the status bar (`status.fileDialog.failed`, five languages) and the call returns "no file". Covers every async picker handler of the three modules. Test `ReportingFileDialogServiceTests` (2) |
| 2 | deck-round9 | `PhotoAlbumView`: thumbnails never disposed, footer count not updated | **Fixed**: a removed photo's bitmap is disposed, all of them on detach (rebuilt on re-attach); the note is kept (`DialogFooter.WithNote(note, out block)`, shared) and refreshed on each rebuild. Test in `DeckDesignDialogTests` |
| 3 | deck-round9 | Presenter ‹ › buttons without accessible name | **Fixed**: name and tooltip `deck.presenter.previousStep`/`nextStep` (five languages). Assertion added to `DeckEditorTests.Penna_puntatore_laser_e_vista_relatore_seguono_la_presentazione`. Fixed dark colours stay a design choice |
| 4 | deck-round9 | Transition sound stored as an absolute local path | **Open, by design for now**: the sound is a link (`xlink:href` in ODP, external relationship in PPTX); making it relative needs the document path at write time in both writers, a format change outside this round |
| 5 | shared-round9 | Glow, shadow, soft-edge radii uncapped; glow opacity not clamped | **Fixed**: `IShapeEffectBounds`/`ShapeEffectBounds` (radius 0..144 pt, NaN/infinity to 0; opacity 0..1) used by `ShapeRenderer` for all three effects. Test `ShapeEffectBoundsTests` |
| 6 | shared-round9 | `FontworkRenderer`: three `FormattedText` per character per repaint | **Open** (performance only, short texts) |
| 7 | shared-round9 | `AngleDial` and chart plots allocate pens per repaint | **Design choice**: a handful of pens per frame on theme-dependent brushes, not measurable; caching would have to follow theme changes |
| 8 | shared-round9 | `FillEditor` «Choose»: picker fault without message | **Fixed** by point 1; also `RasterImageFiles.PickAsync`/`SaveAsync` now dispose the chosen file handle (`await using`), which they leaked |
| 9 | shared-round9 | `CellGridConversions.Table` numeric column stores text | **Fixed**: numeric columns store the number. Test `A_numeric_column_stores_the_number_not_the_displayed_text` |
| 10 | ledger-round7 | `LedgerObjectFileCommands.OpenAsync` indexes `Diagnostics[0]` | **Fixed at the root**: `EmbeddedFileLauncher` threw `ArgumentException` on a nameless packaged file; it now fails with `OPEN005` (five languages), so every failure carries a diagnostic for the three modules. Test `An_embedded_file_without_a_name_fails_with_a_diagnostic_instead_of_throwing` |
| 11 | ledger-round7 | `LedgerDocumentExtraCommands.LinksAsync`: `default` tokens, Deck text key | **Fixed**: the command token flows through, history label `links.history` (five languages) |
| 12 | ledger-round7 | Defined names do not recalculate formulas until the next recalculation | Not re-examined in this round, **open** |
| 13 | ledger-round7 | `Restrict` enumerates the whole used area | Not re-examined, **open** (performance) |
| 14 | ledger-round7 | Advanced-filter probe and consolidation preview at every keystroke | Not re-examined, **open** (performance) |
| 15 | ledger-round7 | `ConditionalRuleEditorView.SwitchKind` forgets typed operands | **Design choice**: operands of one kind rarely fit another |
| 16 | ledger-round7 | Pivot name test failed once in a full run | Not reproduced, **open** (flaky) |
| 17 | folio-ui-round5 R5-L2 | Folio links «update» on an image updated the subdocuments | **Fixed by restructuring**: Folio now uses the shared `IImageLinks` link manager like Ledger and Deck (rule 10): per-link update, change source and break, for images and subdocuments; the old form texts were removed. Tests `FolioLinkCommandTests` (2) |
| 18 | folio-ui-round5 R5-L3 | `AutoTextLibrary.Save` not atomic | **Fixed**: `IAtomicFileWriter.Replace` |
| 19 | folio-ui-round5 R5-L4 | `TableGridPreview` draws every column of sampled lines | **Open** (performance) |
| 20 | folio-ui-round5 | Content controls under forms protection | Already closed on 2026-11-05 |
| 21 | work log 2026-11-05 | Auto-redaction: text inside tracked deletions, authors of saved versions | **Fixed**: `IMetadataRedaction.Redact` takes the text transform and applies it to the paragraphs of every tracked deletion; saved versions (full earlier copies of the document) are removed. Same undo step; status reports both counts. Test `Auto_redaction_reaches_tracked_deletions_and_drops_saved_versions` |
| 22 | folio-ui-round4 | `ContentControlCatalog` walks the document on each call | Already closed in round 4 (P2-D2, revision cache) |
| 23 | formats-round3 | Relationship index rebuilt on every miss | **Design choice**: only damaged files, keeps lookups correct |
| 24 | formats-round3 | `DocxReader.Sections` moves blocks per section | **Open** (only thousands of column changes) |
| 25 | formats-round3 | `PptxWriter.Write` let I/O exceptions escape | **Fixed**: failed result `PPTX106` (five languages) like DOCX/ODT. Test `PptxWriteFailureTests` |

Tests that existed before the fix failed first (9, 10, 21, 25). Points 1, 5 and 17 are new types or a new structure,
so their tests could not run against the old code; 2 and 3 are covered by assertions added to existing headless tests.

## Tests run

Exchange (20), UI.Controls (345), Documents.UI (282), Documents (272), Formats.OpenXml (2,147), Presentations.UI (63),
Localization (32), Architecture (38), Desktop.Tests filtered on `DeckEditorTests.Penna`, `DeckDesignDialogTests`,
`DeckRound9` (14).

## Live checks

- Deck presenter view: hover ‹ and › shows "Previous"/"Next"; a screen reader names them.
- Photo album: remove a photo, the footer count follows.
- Folio: Edit > Links with a linked image: the shared links dialog; Update reloads the image, Break keeps it.
- Folio auto-redaction on a document with a tracked deletion containing an e-mail and a saved version: the deleted
  text is masked, the versions list is empty, ⌘Z restores both.
- Ledger: File > Links, update an image link, the undo label reads "Edit link".
