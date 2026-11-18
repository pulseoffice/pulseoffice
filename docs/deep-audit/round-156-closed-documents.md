# Round 156: memory and lifetime of closed documents

Area: what keeps a document reachable after `IDocumentWorkspace.Close` while the window stays open (undo snapshots,
views, subscriptions on long-lived services, caches, timers, recovery snapshots).

Proof: `Round156ClosedDocumentTests` (Desktop.Tests) composes the real shell, opens one document of a module, opens
a second, closes it through `shell.file.close`, collects, and asserts with `WeakReference` that the closed document's
model and view are gone. One theory row per module (sheets, documents, presentations); the class runs in about 6 s.

## Fixed

| Severity | Module | Defect | Fix |
|---|---|---|---|
| Serious | Folio | `FolioView` and `FolioRulers` subscribed to the singleton `IFolioViewLayout.Changed` (through `FolioRulerServices`) in their constructors and never unsubscribed: every closed Folio document, with its editor, layout, history and snapshots, stayed alive until the app quit. | Both subscribe only while attached to the visual tree (view: `OnAttachedToVisualTree`/`OnDetachedFromVisualTree`; rulers: the horizontal strip's attach and detach events), the pattern `FolioCanvas` already used for marks and spotlight. |
| Serious | Ledger | `SheetGridView` subscribed to the singleton `ISheetSpelling.Changed`, and its `FreeformDrawing` to the singleton `IDrawingToolState.Changed`, as soon as the properties were set, for life: the grid, and through it the whole `LedgerView`, workbook and engine, stayed alive after close. | The grid links to both shared services only while attached to the visual tree; `FreeformDrawing` gained `Link`/`Unlink` and its `Tools` setter respects the linked state. |

Before the fixes the documents and sheets rows failed ("the closed document's view is still alive"); after them all
three rows pass.

## Checked clean

- Deck: the closed presentation and its view are released (the presentations row passes unchanged).
- `FolioCanvas`: shared marks, view layout, spotlight and field-name subscriptions are already attach-scoped; the
  caret blink timer stops on detach.
- Folio navigator and page-status sources: they follow the active context and drop the previous editor when it changes.
- `LedgerView`: language, design-mode and tool subscriptions already go through attach/load-scoped links;
  `ActiveSheetContext.Detach` clears the target of the closed workbook; status segments hold no view.

## Open

- The heap was not inspected with a dump (tool use not permitted here), so the round proves view and model release
  for a freshly created document; documents with charts, external links (`ExternalLinkAutoRefresh` timer), macros or
  recovery snapshots were not exercised and should be added to the same test in a later round.

## Status

Closed for the paths covered by the test: no closed Folio, Ledger or Deck document stays reachable.
