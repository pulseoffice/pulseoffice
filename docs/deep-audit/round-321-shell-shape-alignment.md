# Round 321 (2026-11-20): Shell leftovers and shape text alignment callers

Area: the Shell (314) queue items (torn-off window set and state, Deck `UpdateContext` routing, frozen status bar
progress), the Folio input help note metrics (316), and every caller of shape text `ToRich` after round 316 gave each
paragraph the shape alignment. No builds or tests were run in the round (audit cycle rule).

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | The module contexts (`IActiveSheetContext`, `IActiveFolioContext`, `IActiveDeckContext`) are singletons that keep the view loaded or selected last. With two windows, activating the other window did not re-attach its document, so ribbon state and commands in the focused window acted on the other window's document (Deck has no `UpdateContext` at all: its context is this target). | `IActiveContextClaim` (Shell), implemented by `LedgerView`, `FolioView` and `DeckView`; `ShellWindow.OnWindowActivated` lets the active document's view claim its module context when more than one window is open. Test: `Round321WindowContextClaimTests` (Desktop). |
| 2 | Medium | A frozen (unfocused) status bar froze background activity progress as well: a save or export running while another window was focused showed no progress and stale activities until reactivation. | `StatusBarView` keeps activities in their own panel refreshed even while frozen; only segments freeze. Test: `Round314FrozenStatusBarTests.A_frozen_status_bar_keeps_background_activity_progress_live`. |
| 3 | Minor | A torn-off window closed maximized reopened normal. | `ITornOffWindowSizing.Remember` saves the normal bounds with the maximized flag; `OpensMaximized` maximizes the next torn-off window. Tests in `Round314TornOffWindowSizingTests`. |

## Reviewed without defects

- Reopening torn-off windows at the next start: the app restores no documents at start (no session restore), so there
  is no window set to rebuild; only the torn-off placement and now its maximized state are remembered. Decision, not a
  defect; a session restore would be a feature of its own.
- Folio input help note metrics: the whole interface (shell window, start view, Ledger, status bar) uses the compact
  metrics and there is no density setting, so Folio matches the interface. Closed.
- `ToRich` callers: `ShapeTextParagraphs.First`/`Apply` (Ledger shape text session, Deck), `ShapeListText.Framed`
  (renderer of Ledger, Folio and Deck shapes), `DeckShapeTextEditing` and `DeckTableCellEditor` (all `FromRich`
  callers round-trip through `ToRich`), `SlideTextSegments` (export tags, alignment unused), and the Deck alignment
  checked state. `Apply` writes the shape alignment back for untouched paragraphs, but `WithAlignments` folds values
  equal to the shape alignment to null, so no explicit paragraph alignments appear. The checked alignment button of a
  centred shape now shows centre instead of left (fixed by 316). Exchange and the PDF export draw through the same
  renderer. Tests: `Round321ShapeTextAlignmentCallerTests` (Drawing).

## Still open (queued)

- Programmatic selection changes in an unfocused window's view still attach to the shared module context until the
  focused window is activated again; per-window module contexts would remove it.
- Not tried in the running app.
