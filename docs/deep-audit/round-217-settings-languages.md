# Round 217: settings, interface languages and chart mark contrast

## Scope
The queued item on chart marks in series-derived colours on the dark page, and a new area: the settings service and
store, persistence of the settings file, run-time switching of interface language and theme, and the fallback for
missing translation keys.

## Defects fixed
1. **Medium: chart analysis marks unreadable on the dark page.** Trend lines, error bars, the Pareto line, rising and
   falling waterfall bars and stock rising and falling marks used fixed hues; on the dark page token several fell below
   3:1. `ChartInk.Mark` now lightens or darkens a mark against the screen paper with the shared `WcagColorContrast`
   to 3:1 (the non-text contrast level); the print ink returns the original hue, so print and export are unchanged.
   Tests: `Round217ChartMarkContrastTests`.
2. **Medium: concurrent instances discarded each other's settings.** `JsonSettingsStore` wrote its whole in-memory
   dictionary through one shared temporary file name, so the last instance to flush overwrote changes the other made,
   and two simultaneous flushes collided on the locked temporary file. A flush now reads the file on disk and overlays
   only the keys this instance set or removed, through a unique temporary file; when the file is missing or unreadable
   the whole in-memory state is written. Tests: `Round217SettingsStoreTests`.

## Checked without defects
- Corrupt settings file: kept as `.corrupt`, defaults used; values of the wrong type or outside the allowed choices
  fall back to the default in `SettingsService.Get`.
- Missing translation keys fall back to English, then to a visible bracketed key; placeholder parity across the five
  catalogs is guarded by the localization tests.
- Language and theme changes are applied at run time through `LanguageCoordinator` and the theme service; chart
  previews read the page tokens at each render.
- Right-to-left shell readiness: none of the five interface languages is right-to-left.

## Open items
See the round 217 entry in `docs/audit-queue.md`.
