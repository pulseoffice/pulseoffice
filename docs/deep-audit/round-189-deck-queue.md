# Round 189: Deck queue

Five queued Deck items, one commit each. Not built or tested in the worker; the coordinator builds and runs the
tests after the join.

## Done

1. **Links to hidden slides in the show.** `PresentationController` now owns a copy of the playlist and can `Admit`
   a slide; `ShowPlaylist.DetourPosition` places it in deck order among the playlist slides. A hyperlink, a
   "go to slide" action or an image map pointing to a hidden slide (or to any slide outside a custom show) shows it,
   and the show continues with the next playlist slide after it in deck order. Tests:
   `Round189ShowHiddenSlideLinkTests`.
2. **mp4 export draws motion.** `SlideVideoTimeline` splits each slide into shots: transition frames (about 15 per
   second, never fewer than 120 ms of transition), animation frames over the slide's main timeline, then one still
   for the rest of the slide's time; each phase is capped at 300 shots so a long animation stays affordable. The
   still-only slide stays one frame. `DeckVideoShotRequests` turns shots into `SlideFrameRequest`s (the animation
   plan's visuals, the outgoing slide at its final state and the transition frame from the same effect table the show
   uses), and `SlideImageRenderer` draws the two layers with the transition's transform, clip and opacity on black.
   Tests: `Round189VideoMotionTests`.
3. **Show keys.** `ShowKeypad` (core) keeps the screen state and typed digits. B or `.` toggles a black screen,
   W or `,` a white one, any other key or a click resumes; digits then Enter jump to that slide number of the deck
   (hidden slides included, through item 1); after the last slide a black end screen with
   `deck.present.endOfShow` waits, a forward key or click closes, Back returns to the last slide. Rehearsal still
   closes at the end. Tests: `Round189ShowKeypadTests`, `Round189ShowKeysTests`.
4. **PDF export with hidden slides.** `PdfExportProfile.IncludeHiddenLabel` (set by Deck when the deck has hidden
   slides) shows a checkbox in the shared PDF dialog; the layouts' builders take the choice and the preview follows
   it. Key `deck.export.includeHidden` in the five languages. Tests: `Round189DeckPdfHiddenSlidesTests`.
5. **Paste is named Paste.** Deck's text insertion takes an optional history label; the exchange paste passes
   `deck.action.paste` for text appended to a selected box, inserted into it, or placed as a new box. Tests:
   `Round189DeckPasteLabelTests`.

## Open

- The in-app media player (loop, volume, trim, fades) stays queued as its own block.
- Typing at the caret during paste still records the typing step's own name.
- Video export renders transitions at a fixed 15 frames per second and ignores trigger animations (they need a
  click, which a video has none of).
