# Deep audit round 59: Deck slideshow and exports

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Presentations/AnimationTimeline.cs | 68/68 | ok |
| src/PulseOffice.Presentations/AnimationSchedule.cs | 241/241 | D2 fixed |
| src/PulseOffice.Presentations/PresentationController.cs | 128/128 | ok (bounded steps, empty deck guarded) |
| src/PulseOffice.Presentations/Animation.cs | 84/84 | ok |
| src/PulseOffice.Presentations/RehearsalTimeline.cs | 18/18 | ok (negative times clamped) |
| src/PulseOffice.Presentations.UI/DeckVideoFrameSource.cs | 86/86 | ok: frames are rendered one at a time on demand, nothing is held for the whole deck |
| src/PulseOffice.Presentations.UI/DeckLinkResolver.cs | 27/27 | ok |
| src/PulseOffice.Presentations.UI/DeckPublisher.cs | 106/106 | D3 fixed |
| src/PulseOffice.Presentations.UI/DeckExportCommands.cs | export paths 100-195 | D1, D3 fixed |
| src/PulseOffice.Presentations.UI/PresentationWindow.cs | 330-670 (navigation, interactions, animator, auto-advance) | ok: hyperlinks only http/https/mailto, run-macro not executed by the show |
| src/PulseOffice.Formats.OpenXml/PptxTransitionMapper.cs | reading paths 140-185 | D1 fixed |
| src/PulseOffice.Formats.Odf/OdfValues.cs, OdfAnimationMapper.cs (timing readers) | timing helpers | ok: already bounded to one hour / one day |
| src/PulseOffice.Platform/VideoEncoding.cs, VideoToolbox.cs (frame timing) | timing loops | ok given bounded durations (ffmpeg repeats each frame duration*fps times) |

Not read in this round: PresenterWindow.cs, TransitionStage.cs, DeckAnimationPlayer.cs, DeckPrintable/PDF writer, media playback, CustomShow*, ShowSetup*.

## Defects

- **D1 (serious)** `PptxTransitionMapper.FromTransition`: `advTm` read without bound or culture; a file with `advTm="2147483647"` made the mp4 export
  repeat a frame for 24 days (ffmpeg path writes `duration*fps` frames) and a negative value was kept. `p14:dur` was unbounded too.
  Fixed: both clamped to [0, 1 h] as the ODF reader already does, invariant parsing. Tests: `PptxTransitionTimingBoundsTests`.
- **D1b (serious)** video slide duration was a private static in `DeckExportCommands`; now `IVideoSlideDuration`/`VideoSlideDuration`
  (DI, max 10 minutes per slide in a video). Tests: `VideoSlideDurationTests`.
- **D2 (medium)** `AnimationScheduler.Groups`: the window of a click step (and of the automatic entry group) started at the first effect's
  start, after its delay, so the show played the effect immediately on click: the delay was lost. `ScheduledEffect` now carries
  `TriggerMs` and groups start at the trigger. Tests: `AnimationScheduleTests.TheDelayOf*` (failed before the fix, verified).
- **D3 (medium)** HTML export built the whole page (all slides as base64 PNG) in one string and then a second byte copy; with ~120 slides
  that is hundreds of MB. `IDeckPublisher.WriteHtml(deck, TextWriter)` now streams slide by slide into the atomic writer.

## Live checks

- Slideshow: a click effect with a 2 s delay waits 2 s after the click; a slide whose first effect is "after previous" with delay waits.
- Export a PPTX with a huge `advTm` to mp4: the slide lasts at most 10 minutes; HTML export of a large deck keeps memory flat.
