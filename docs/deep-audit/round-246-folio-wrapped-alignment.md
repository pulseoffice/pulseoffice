# Round 246: Folio wrapped paragraph alignment

## Fixed
- Medium: an object aligned to the middle or bottom of its paragraph used the paragraph's unwrapped height, so with
  wrapping on it sat above the real paragraph bottom. `ObjectFlow.HeightDependent` detects such objects and
  `PaginationRun.Settled` measures the wrapped height with a dry run of the dynamic line loop (`WrappedHeight`),
  re-places the candidates and repeats, at most four passes or until no object moves by more than 0.5 pt.
  Test: `Round246WrappedAlignmentTests`.

## Open (queued)
- Note continuation notices are still layout-only text with no caret, selection or copy.
- The wider footnote/endnote and anchored-object round-trip audit was not carried out this round.
