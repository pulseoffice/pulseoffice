# Round 225: shape text columns and vertical modes, Ledger connector ends, interface colours, settings reload

Area: five items of the audit queue (rounds 214, 217, 218, 220). Code was written without building or running tests;
the coordinator builds and runs the tests at the join.

## Fixed

1. **Settings written by another instance are reloaded.** `JsonSettingsStore` now implements
   `IReloadingSettingsStore`: an injected `ISettingsFileMonitor` (`FileSystemSettingsFileMonitor` in the app,
   `NoSettingsFileMonitor` by default) calls `Reload`, which reads the file and takes every value that differs, or a key
   that disappeared, except the keys this instance changed and has not flushed yet. Reloaded keys raise `Reloaded`;
   `SettingsService` turns them into `Changed` events through `ISettingsChangeDispatch`, which the desktop app posts to
   the UI thread (`UiThreadSettingsChangeDispatch`). An instance's own flush finds nothing different and raises nothing.
   Tests: `Round225SettingsReloadTests`.
2. **Shape text columns break at real line boundaries.** `ShapeTextColumns.Place` now takes the composed lines
   (`ShapeTextLine` top and bottom) instead of one line height measured on the base font; each column takes whole lines
   while they fit, a line taller than the column gets a column of its own, the last column takes the rest.
   `FormattedShapeTextLines` measures the line bands from the formatted text through its highlight geometry (every
   character up to 4000 characters, then only characters after white space). Tests: `Round225ShapeTextColumnTests`.
3. **Ledger can choose a connector's ends.** New command `ledger.connector.sites` (Insert › Draw menu by its prefix,
   object context menu) opens the shared `ConnectorSitesDialog` (`PulseOffice.UI.Controls.Shapes`, a form with start and
   end sites: automatic, left, top, right, bottom); `ISheetConnectors.Resite` reroutes the connector, and the change is
   one undo step. Texts in the five languages. Tests: `Round225SheetConnectorSitesTests`.
4. **Interface colours from the palette.** Trace arrows and page breaks on the Ledger grid used fixed blues, greens and
   reds: they now use the Info, Success and Danger tokens. The notification badge drew white text on the Danger token,
   below 3:1 in the dark and high-contrast palettes: the new `OnDanger` token (white, black, black) is used. The start
   page's Create button filled the raw module colour behind white text (the green fell to 3.3:1) and its tile glyphs
   lightened by a fixed amount: both are now made readable with `WcagColorContrast` against white (`Palette.OnAccent`)
   and the surface token. `ReadableAccentPalette` reads the same `OnAccent`. `Round225HardCodedColourTests` scans the
   interface projects for colour literals (`Color.FromRgb`, `Color.FromArgb`, `Color.Parse("#…")`, named `Colors` and
   `Brushes` other than transparent) and fails when a file has more than its frozen count, so a new literal needs a
   token or an explicit allowance. Tests: `Round225AccentInkTests`, `Round225HardCodedColourTests`.
5. **East Asian, Mongolian and right-to-left stacked vertical modes.** `TextFlow` gained `EastAsianVertical`,
   `MongolianVertical` and `StackedRightToLeft`. PPTX maps them to `eaVert`, `mongolianVert` and `wordArtVertRtl`; ODF
   writes `style:writing-mode="tb-lr"` for Mongolian (standard), and `tb-rl` or `style:direction="ttb"` with our
   `urn:pulseoffice:shape-text` `text-flow` value (`east-asian`, `stacked-rl`) for the other two, so other readers see
   plain vertical or stacked text. East Asian vertical text is drawn as vertical text with the Latin rotated; Mongolian
   text is drawn with its lines mirrored, first line on the left, in single-column shape text (per line band) and in
   text boxes (mirrored line tops; hit testing now picks the line by position rather than by order); right-to-left
   stacked text boxes place their first stack at the right. The Deck text frame dialog and the shared shape style editor
   offer the new modes. PPTX226 and ODP217 now say only what is still approximated: East Asian characters are turned
   with the line instead of kept upright, declared only when the vertical text contains such characters
   (`EastAsianUprightText`). Tests: `Round225VerticalModesTests`; the round 213 tests now expect no declaration for
   Latin text.

## Open items

Listed in `docs/audit-queue.md` (Deck, settings and theme entries marked 225).
