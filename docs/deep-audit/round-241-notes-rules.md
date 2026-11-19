# Round 241: notes, object layout, Ledger notes and table rules

Fixed:
- Medium: Ledger's annotated span mapping through the shown text matched characters greedily from the left, so a
  digit repeated in a literal prefix of the number format ("Lot 1: 1,234.50") pulled the highlight onto the prefix.
  The mapping now uses a longest common subsequence aligned from the end (late matches preferred), with a size cap
  beyond which no highlight is drawn.
- Medium: XLSX table-reference rule forms with the same range and the same flat formulas could swap their structured
  forms when Excel renumbered priorities and reordered the rules. Each entry now also records its rank among the rules
  with the same range and flat formulas, ordered by priority; the reader picks by that rank before priority and ordinal.

Still open (queued): Folio note continuation notices without caret, selection or copy; the incremental layout's
page-parity stability check ignoring inside/outside objects; paragraph-relative alignment using the unwrapped height;
the exact character hit skipping rotated text frames. Nothing of this round was built or run (audit cycle rule).
