# Round 242: Folio notes and anchored objects

## Fixed
- Medium: the incremental layout reused pages after a page-count shift even when an object was aligned inside or
  outside horizontally, so after an edit before it such an object stayed on the wrong side of a page whose parity
  changed. `LayoutEngine.Relayout` now treats those objects as page-parity dependent, like mirrored page styles and
  page-anchored objects. Test: `Round242InsideOutsideRelayoutTests`.

- Minor: Folio's exact character hit (link tips, character queries) skipped rotated text frames.
  `LayoutQueries.CharacterAt` now untwists the point into each rotated frame it falls in and searches its lines.
  Test: `Round242RotatedCharacterHitTests`.

## Still open (queued)
- Note continuation notices are still layout-only text (no caret, selection or copy).
- Alignment relative to a paragraph still uses its unwrapped height: the wrapped height depends on where the object
  itself lands, a circular dependency that needs a second placement pass. Word allows only top alignment relative to a
  paragraph, so the effect is limited to ODT files.
