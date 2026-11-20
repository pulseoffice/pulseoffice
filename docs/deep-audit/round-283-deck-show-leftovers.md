# Round 283: Deck slide show leftovers of round 278 (2026-11-20)

## Scope
The four queue items round 278 left open for the slide show: iterated text revealed as a whole, ODP
`presentation:play-full="false"` without a model value, the show setup dialog and the transition editor missing the
loop pause, kiosk restart and "stop previous sound", and the pause between loops leaving the last slide on screen.

## Defects
- Medium, fixed: iterated text (by word, by letter, backwards) appeared as a whole. `ElementVisual` now carries a
  `TextReveal` (unit, backwards, exit, elapsed time, interval, progress). `AnimationScheduler` gives an iterated
  entrance or exit a reveal instead of the whole-element motion while the effect runs; `TextIterationReveal` (core)
  turns the reveal into a per-character mask (one unit starts every interval, and every unit has started by the end
  of the effect, so the reveal never outlasts the step) and makes unrevealed runs transparent, so the text keeps its
  layout. `SlideRenderer` applies it to text elements, which covers the show, the editor preview and the video export.
- Medium, fixed: `presentation:play-full="false"` (the sound is cut at the next event) had no model value.
  `SlideTransition.SoundPlaysToEnd` (default true) reads and writes `play-full`; the show cuts such a sound when the
  next slide arrives (`TransitionSoundCues`, used by `ShowMediaStage.Arrive`). PPTX has no attribute for it: the
  writer gives the next slide a native `p:sndAc/p:endSnd` when that slide has no sound of its own, which PowerPoint
  plays the same way; reading that file back yields "stop previous sound" on the next slide rather than the flag on
  the first, the same behaviour in a different place. A cut sound on the last slide has nothing to carry it in PPTX.
- Medium, fixed: the show setup dialog did not edit the pause between loops and the kiosk restart delay. Two
  seconds fields, enabled for a looping show (pause) and a kiosk (both).
- Medium, fixed: the transition editor had no "stop previous sound" choice. The sound list now starts with "No
  sound" and "Stop previous sound", as in PowerPoint, and a "Stop the sound at the next slide" check box edits
  `SoundPlaysToEnd`.
- Medium, fixed: the pause between loops kept the last slide on screen. `ShowCurtain.Pause` shows a black pause
  screen (announced to screen readers); a click or an advance key skips the pause and restarts at once
  (`ShowLoopClock.SkipPause`).

Tests: `Round283IteratedTextTests`, `Round283TransitionSoundCueTests` and `Round283PauseCurtainTests`
(Presentations), `Round283SoundCutTests` (Formats.OpenXml), `Round283LoopPauseTests` (Presentations.UI), and the
existing Desktop tests of the transition editor and the show setup dialog, extended.

## Still open (queued)
- Iterated text in shapes drawn as objects (`ShapeObject` text, not `TextElement`) is still revealed as a whole.
- Each unit of an iterated effect appears at once; the effect's own motion (fade, fly) is not run per unit, and when
  the units would need longer than the effect the reveal is compressed into the effect's duration.
- Picture bullets of a paragraph not yet revealed stay visible.
- The pause screen is plain black, with no countdown.
- None of this has been seen live.
