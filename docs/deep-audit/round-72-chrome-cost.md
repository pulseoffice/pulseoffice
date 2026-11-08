# Round 72: cost of the shell chrome and side panels

Round 70 ([round-70-render-cost.md](round-70-render-cost.md)) measured the Ledger grid and the Deck thumbnails but not
the ribbon, status bar, side panels, spelling and autosave. This round measures them headless (Debug, Stopwatch).
Numbers are appended to `$TMPDIR/pulseoffice-chrome-cost.txt`, `pulseoffice-folio-scale.txt` and
`pulseoffice-ledger-render.txt`.

## Defects found and fixed

### Ribbon rebuilt its command tree on every caret move (medium)

Every caret move and selection change in Folio, Ledger and Deck calls `IRibbonStateNotifier.Notify`, which makes the
shell project the ribbon again. With 1,353 registered commands the projection cost 8.3 ms (Folio), 11.8 ms (Ledger) and
4.9 ms (Deck) per move, synchronously on the UI thread, before the frame. Evaluating presence and enablement is cheap
(0.05 ms); almost all the time went into `CommandTreeBuilder` rebuilding the same tree (path splitting, linear lookups
of siblings, sorting by layout rank, translating every node) although the set of present commands had not changed.

Fix: `CommandRegistry` keeps a `SurfaceTreeCache` per surface and module. The built tree is reused while the stamp
(surface, module, interface language, registry version) and the ordered list of present command definitions (by
reference) are the same. The version increments on `Register`, `Unregister`, `Rebind` and `Use`; `Rebind` replaces the
definition, so a rebound gesture also changes the reference list. Enablement and checked state are still evaluated on
every projection, since they live on `RibbonItem`, not on the tree. The menu bar (`BuildMenu`) uses the same cache.

After: 0.18 ms (Folio), 0.13 ms (Ledger), 0.10 ms (Deck) per caret move.

### Review panel built a card for every change and comment on every document change (medium)

`ReviewPanel` (shared by Folio, Ledger and Deck) wrapped each item in a `ListBoxItem` with a fully built card and posted a
refill for every `Changed` event of its source. The list was virtualized (14 containers realized), but each refill still
built all cards: with 5,000 comments, opening the panel took 784 ms and a burst of 20 document changes 3,684 ms
(22 refills).

Fix: the list is bound to the `ReviewItem` records with a `FuncDataTemplate`, so cards are built only for realized
containers; source changes are coalesced into one refill posted at background priority; a refill that yields the same
items keeps the current `ItemsSource` (scroll position and selection stay). After: open 32-49 ms, burst of 20 changes
1-6 ms, 3 source reads.

### Navigator had the same eager rows and uncoalesced refills (medium)

`NavigatorPanel` (headings, tables, objects, sheets, slides in the three modules) had the same pattern. Same fix: data
template over `NavigatorItem`, depth indentation set when the container is prepared, coalesced refill, unchanged list
kept. Drag and drop resolves items through `ItemFromContainer`/`ContainerFromItem`. After: 5,000 headings open in
36 ms, a burst of 20 changes costs 1 ms with 2 source reads, 32 rows realized.

## Measured, no defect

- **Folio status bar**: there is no live word count in the status bar (word count is a dialog and a field), so nothing
  is recounted per keystroke. The page segment (`FolioPageStatus` / `FolioPageJump.PageOf`) scans the page lines:
  0.13-0.26 ms per caret move on 500 pages, kept as a regression guard.
- **Spelling**: `FolioProofing` caches results per paragraph text and language and is asked only for painted
  paragraphs; a keystroke rechecks only the edited paragraph.
- **Ledger frozen panes**: a million used rows with 3x2 frozen panes: frame 4.2 ms at the top, 5.6 ms at the bottom,
  scroll 5.8 ms (plain grid 4.3 / 5.5 / 4.6 ms).
- **Recovery snapshot**: `AutosaveCoordinator` runs every 10 minutes (default) and only for modified documents.
  Serializing a 500-page Folio document takes about 350 ms (13 MB of JSON). Not changed in this round: moving it off the
  UI thread needs an immutable copy of the model. Recorded as an open point (minor); closed in
  [round 75](round-75-snapshots.md).

## Tests

- `tests/PulseOffice.UI.Commands.Tests/SurfaceTreeCacheTests.cs`: the same presence reuses the tree; presence change,
  `Rebind` and `Register` build a new one.
- `tests/PulseOffice.Desktop.Tests/ChromeCostTests.cs`: ribbon projection with the composed registry under 2 ms per
  caret move in the three modules (failed before the fix at 8.3 and 11.8 ms against 8 ms).
- `tests/PulseOffice.UI.Controls.Tests/ReviewPanelCostTests.cs` (serial collection `Chrome performance`): 5,000 comments
  and 5,000 headings realize only visible rows, a burst of changes refills once (failed before the fix: 22 refills).
- `tests/PulseOffice.Documents.UI.Tests/FolioScaleTests.cs`: page status per caret move and recovery snapshot of a
  500-page document.
- `tests/PulseOffice.Sheets.UI.Tests/LedgerRenderCostTests.cs`: frozen panes over a million rows.

## Not covered

- `PulseOffice.Architecture.Tests` fails on `DeckModeViews._stillImages` and `DeckFilmstrip._stillImages` (round 70
  constructs `StillImageProbe` in fields); not touched here.
- macOS `sample` was not needed: no hang was found, every cost above is measured by Stopwatch.
