# Round 170: Deck masters and layouts, slide tables, media

Area: slide masters and layouts (placeholder inheritance, layout change across masters), tables in slides (merges, cell
looks, header and banding) and media (embedded and linked, poster, loop, mute, volume) through the ODP and PPTX codecs
and the Deck layout actions.

## Defects

1. **Medium: a layout of the same kind applied from another master did not relayout the slide.**
   `DeckLayoutActions.ApplyLayout` reassigned the master and then called `DeckSlideActions.SetLayout`, which returns
   early when `master.Template(slide)` already equals the target; after the reassignment the new master's fallback
   template of the same kind matched, so the slide kept the old layout id and the old master's placeholder geometry.
   Fix: `DeckSlideActions.Relayout` performs the relayout unconditionally; `SetLayout` keeps the equality check and
   delegates to it, and the cross-master path of `ApplyLayout` calls `Relayout` inside its transaction (one undo step).
   Test: `Round170LayoutAcrossMastersTests`.
2. **Minor: PPTX header-cell vertical alignment was lost on read.** `PptxDeckTableMapper.Read` dropped cell formats of
   the header row (row index `TableCellFormat.HeaderRow`) when the table had no own look, although the writer emits
   them and the ODP reader keeps them. Test: `Round170PptxTableHeaderAlignTests`.

## Checked without defects

- Merged cells: PPTX `gridSpan`/`rowSpan`/`hMerge`/`vMerge` and ODP spans with covered cells, header-row merges.
- Media: loop, mute and volume in PPTX timing nodes and ODP `draw:param`; linked versus embedded media; poster frames;
  rotation and hidden state handled by the shared element writers.
- Layout rename, duplicate and delete are refused on read-only documents and are single undo steps.

## Open items

- Media start time, trim (start and end) and fade are not in the model (`MediaElement`), so PPTX `p14:trim`/`p14:fade`
  and the ODP equivalents are not carried; this is a missing feature rather than a regression and needs its own block.
- ODP volume is stored as whole decibels (`VolumeDB`), so a volume crossing ODP is rounded to the nearest decibel.
- Table banding columns, first/last column and last row flags are applied to the look on PPTX read but not kept as
  flags in `DataTableStyle`, so a PPTX table re-exported carries them only as direct cell formatting.
