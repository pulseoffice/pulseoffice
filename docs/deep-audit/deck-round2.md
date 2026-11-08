# Deep audit, round 2: Deck

Area: `src/PulseOffice.Presentations`, `src/PulseOffice.Presentations.UI`, and the parts of Drawing and Charts that Deck
uses, ODP and PPTX. Starts from the open points of round 1 (`deck.md`, `drawing.md`).

## Defects

| Severity | File:line | Defect | Status | Test |
|---|---|---|---|---|
| medium | `Presentations.UI/DeckSnapshotProvider.cs:13` | the recovery snapshot ignored a missing native writer and a failed write (`Write` result discarded): the shell kept an empty or truncated recovery file believing it valid, unlike Ledger's `SpreadsheetSnapshotProvider`, which throws. Now aligned with Ledger: both cases throw `InvalidOperationException` with an English message | fixed | `DeckSnapshotProviderTests` (two, fail before) |
| minor (test) | `tests/PulseOffice.Desktop.Tests/GalleryPixelTests.cs:33-69` | the three pixel tests did not await their dispatched body (`Dispatch` returns `Task<Task>`), so they always passed: the theme gallery had diverged from its golden image since `99b2c201` without anyone noticing. Now `.Unwrap()` | fixed | the tests themselves |

## Theme gallery: thinner frames and arcs

The theme thumbnails (Atelier, Brace, Mezzanotte, Orchidea) draw their decorative frames and arcs much thinner than
`Golden/theme-gallery.png` (4,608 pixels differ). The golden image was written in `6da58344`. Bisecting in a separate
worktree was not possible from this isolated agent, so the cause was found by reading the history of the rendering path
(`git diff 6da58344 HEAD` on `SlideRenderer`, `ElementPainters`, `ShapeRenderer`, `ShapeBrushes`, `ContentPainters`,
`DecorationCanvas`) and confirmed by experiment: commit `99b2c201` («shape text in scale with the slide») made
`DrawingContentPainter.Paint` (`src/PulseOffice.UI.Embedding/ContentPainters.cs:114`) render drawing objects under a
`pixelsPerPoint` scale transform instead of at one pixel per point. Restoring the old branch locally makes the test pass
again with the old image; reverting the experiment makes it fail.

Judgement: **intended improvement, not a regression**. Line widths are in points; before `99b2c201` a 3 pt frame was
3 pixels wide in a 150-pixel thumbnail (as thick as on a full slide, i.e. about six times too thick relative to the
slide) and shape text did not scale either. Now strokes keep their proportion to the slide, as in the editor at any zoom,
in the slide show and in the PDF. The golden image was regenerated (`PULSE_GOLDEN_WRITE`) and checked by eye: every
decoration is present, frames and arcs are proportional. The test is active again (it is not skipped on this branch;
the `Skip` added on `deep-audit-round-2` for S2-D5 in `shell-round2.md` should be dropped when merging).

## Thumbnail panel rebuild (round 1 open point, `DeckView.cs:415`)

**Medium (performance), fixed.** Every `deck.Changed` (every confirmed key, every move) cleared the slides panel and
built a new `SlideView` with its frame for every slide (`DeckFilmstrip.BuildFilmstrip`), linear in the number of slides
per edit plus a full relayout. The reuse-by-stamp logic that `SlideSorterStrip` already had was extracted into the
shared `SlideFrameCache<TFrame>` (`src/PulseOffice.Presentations.UI/SlideFrameCache.cs`), used by both the sorter and
the filmstrip: a frame is rebuilt only when its `SlideThumbnailStamp` changes (slide content, position, current flag,
size, master, theme, header/footer…). On reuse the filmstrip refreshes only the cheap parts that the stamp does not
cover (header/footer chrome when it changed, the accessible name with the total count, the live text of the current
slide); section headers are reused by name; the panel's children are replaced only when the sequence changes.
Tests: `SlideFrameCacheTests` (50 slides, one edited: one rebuild; moving the current slide: two; removed slides leave
the cache), `SlideSorterStripTests` unchanged and green, Deck tests of `Desktop.Tests` green (42).

## Video export (round 1 open point, `DeckExportCommands.cs:125`)

Checked, not changed in this round:

- **Errors**: the three encoders (`FfmpegVideoEncoder`, `VideoToolbox`, `MediaFoundationVideo`) turn the expected
  failures into `OperationResult` failures with `VIDEO00x` codes; `AtomicFileWriter.CommitAsync` deletes the temporary
  file in `finally`, so a failed or cancelled encode leaves no partial `.mp4`. The status bar shows the generic
  `deck.export.failed` without the encoder's diagnostic (minor, remaining).
- **Cancellation**: ffmpeg is killed and the output discarded on `OperationCanceledException`; VideoToolbox and Media
  Foundation check the token per frame inside `Task.Run`. The frames, however, are all rendered on the UI thread before
  encoding starts, without checking the token (minor, remaining: the interface stays busy for the whole rendering).
- **Memory (medium, remaining)**: all BGRA frames are still held at once (`List<VideoFrame>`, 100 slides at 1920 px
  ≈ 800 MB). The fix is a restructuring of `IVideoEncoder` in `PulseOffice.Platform` (outside this area) to take a
  frame source rendered on demand (rendering marshalled to the UI thread, encoding on the pool), touching three
  encoders of which one (Media Foundation) cannot be verified on this Mac. Left for a dedicated block.

## Coverage of this round

Re-read in full: `DeckSnapshotProvider.cs`, `DeckFilmstrip.cs` (round 1 Esc fix in `RenameSection` intact),
`SlideSorterStrip.cs`, `SlideThumbnailStamp.cs`, `DecorationCanvas.cs`, `DeckExportCommands.cs` (video part),
`VideoEncoding.cs`, `GalleryPixelTests.cs`; the rendering path diffs since `6da58344`. The systematic re-reading of
every file modified by round 1 was **not completed** in this session and remains open.

## Summary

- Medium, fixed: recovery snapshot silent on write failure; thumbnail panel rebuilt in full on every change.
- Minor (test), fixed: pixel tests never awaited their body; theme gallery golden regenerated (intended change in
  `99b2c201`, not a regression).
- Medium, remaining: video export memory. Minor, remaining: video rendering on the UI thread without cancellation,
  generic failure message.

Live checks for the coordinator: on a presentation of 200+ slides, typing in a title and moving an object must not
stall the slides panel; adding, deleting, hiding, reordering slides and renaming a section must update numbers,
section headers and the highlighted thumbnail; screen reader name of the thumbnails ("slide n of N") after adding a
slide; theme gallery look (frames proportional to the thumbnail).
