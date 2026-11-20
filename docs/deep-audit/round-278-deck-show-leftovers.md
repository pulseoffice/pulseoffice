# Round 278: Deck slide show leftovers of round 274 (2026-11-20)

## Scope
The six open items round 274 queued for the slide show: indefinite repeats, effect and action sounds, "stop previous
sound", the ODP `presentation:play-full` mapping, kiosk keys and timings, iterated text, and "from current slide" on a
hidden slide.

## Defects
- Medium, fixed: repeat until next click and until end of slide ran one cycle. `ScheduledEffect.LoopUntilMs` (the next
  click's trigger time, or the end of the slide) and `IAnimationScheduler.LoopsAt`; the scheduler evaluates a looping
  effect cyclically, and the show keeps a frame loop (`ShowEffectLoop`) running on the frozen step while an effect loops.
  The step's span stays one cycle, so clicks still reach the next step, where the loop ends.
- Medium, fixed: animation effect sounds were never played; the sounds of the effects starting in a step or entry
  window now play (`PresentationController.SoundsIn`). The sound attached to a non-sound action (hyperlink, go to slide)
  now plays too.
- Medium, fixed: "stop previous sound" had no model value. `SlideTransition.StopPreviousSound`: PPTX
  `p:sndAc/p:endSnd` (the element the queue called `p:stSnd` is the start-sound one), ODP our
  `pulsetransition:stop-sound`; the show stops the playing sound on arrival.
- Medium, fixed: ODP `presentation:play-full="false"` was read as "loop until next sound". ODF 1.3 defines it as
  whether the sound plays to its end or is cut at the next event: it is not a loop. Looping is now our
  `pulsetransition:loop-sound` attribute and `play-full` is written as `true`; files written by earlier builds lose
  the loop flag (declared here, no migration).
- Medium, fixed: in a kiosk every key still worked. Only Esc is accepted now (`ShowLoopClock.AllowsKey`).
- Medium, fixed: the kiosk restart delay and the pause between loops had no model value. `ShowSetup.KioskRestartMs`
  (PPTX `p:kiosk/@restart`, ODP our `pulseshow:kiosk-restart`) and `ShowSetup.LoopPauseMs` (ODP
  `presentation:pause`, PPTX our `pulseshow:loopPause` in the `p:showPr` extension list; OOXML has no native pause).
  The show waits the pause before looping and, in a kiosk, restarts from the first slide after the restart delay
  without pointer input.
- Minor, fixed: `p:iterate/@backwards` was dropped. `AnimationBehaviours.IterateBackwards`: PPTX `backwards="1"`, ODP
  our `pulseanim:iterate-backwards` (ODF has no native attribute).
- Minor, fixed: from current slide on a hidden slide started from the next visible one. `ShowPlaylist.FromCurrent`
  inserts the hidden slide at its place, as PowerPoint does; from the beginning still skips hidden slides.

Tests: `Round278ShowLeftoversTests` (Formats.OpenXml and Presentations), `Round278KioskKeysTests`.

## Still open (queued)
- Iterated text (by word, by letter, backwards) is still revealed as a whole in the show: the renderer has no per-run
  reveal.
- `presentation:play-full="false"` (cut the sound at the next event) has no model value.
- The setup dialog does not edit the loop pause and kiosk restart, and the transition editor offers no "stop previous
  sound" choice.
- The pause between loops leaves the last slide on screen instead of a pause screen.
- None of this has been seen live.
