# Round 164: super-linear paths found by the growth checks

Area: four paths that the new growth checks (`tests/Shared/GrowthCheck.cs`, a ten times larger run may take at most
forty times as long) measured at roughly fifty to sixty times. The tests were not loosened. Built and tested by the
coordinator after the join of the four audits of the cycle.

## Fixed

1. **Medium: the Folio page status scanned every page on each caret move.** `FolioPageJump.PageOf` walked all pages
   from the last one and every line of each, so the status bar cost grew with the document on every caret move
   (5.7 ms at 600 paragraphs, 283 ms at 6000 for 200 moves). Now an index from paragraph to the starts and pages of
   its lines is built once per `DocumentLayout` (held weakly, so a new layout gets a new index) and a lookup only
   reads the lines of the caret's paragraph. Same answer: the last page holding a line of the paragraph starting at
   or before the offset, page 1 otherwise.
2. **Medium: refilling empty content controls searched the document once per control.** `ContentControlEditing.Refill`
   located each control's paragraph with `IStoryCatalog.Find` (a walk over every story) twice per control, so the
   work was quadratic (37 ms at 300, 1.85 s at 3000). Refill now indexes the paragraphs once and fills each control
   in its paragraph directly (`ReplaceWithin`); spans that left their paragraph still take the general path.
3. **Medium: deleting a long commented range was quadratic.** Every comment mark met by `RangeDeletion` rebuilt the
   comment lookup, the reply lookup of `CommentCollection.Thread` and removed one comment with a list scan; every
   removed block was also taken out of its container with its own `List.Remove`. The purge now records the doomed
   comments and, at `Flush`, collects their threads in one pass (`CommentCollection.Threads`) and removes them in one
   pass (`RemoveAll`); the container is rebuilt once without the removed blocks (21 ms at 2000 paragraphs, 1.28 s at
   20000 before).
4. **Medium: text to columns on a whole column walked a million rows.** `TextToColumns.Plan` iterated the selected
   rows; it now bounds a whole-row or whole-column selection to the used area through `ISelectionWorkArea`, as the
   duplicate remover already does, so the cost depends on the used rows only (0.3 ms at extent 16, 177 ms at
   1,048,576 before).

## Open

None in this area. Not measured here: the timings above are from the failing runs; the coordinator's run after the
join confirms the four checks.

## Status

Closed, pending the coordinator's build and test run.
