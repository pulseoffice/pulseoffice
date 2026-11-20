# Round 286: Deck iterated effects and show leftovers (2026-11-20)

## Scope
The queue items round 283 left open: iterated text in drawn shapes, per-unit motion and timing of iterated effects,
picture bullets of unrevealed paragraphs, the plain pause screen between loops, and the transition sound cut on the
last slide in PPTX.

## Defects
- Medium, fixed: an iterated effect was compressed into the effect's duration and every unit appeared at once.
  PowerPoint starts one unit every iteration delay and lets each unit play the whole effect, so the step lasts the
  duration plus the delay times the units less one. `IAnimationScheduler.Schedule(Slide)` and
  `ScheduleTriggered(Slide, trigger)` count the units of the target (`ITextIterationReveal.UnitCount`) and give the
  effect that span (`ScheduledEffect.SpanMs`); the show, the controller, the slide plan, the editor preview and the
  video export use them. `TextReveal` now carries the unit duration and the effect's opacity curve, and
  `TextIterationReveal.Opacities` gives each character the opacity of its unit, so fade, fly and zoom fade each unit
  in (or out) over the effect's duration; a fading run takes its resolved ink (run colour, role, paragraph colour or
  hyperlink colour from the theme) with partial alpha.
- Medium, fixed: iterated text in shapes drawn as objects (`ShapeObject`) was revealed as a whole. The scheduler
  counts their units and `SlideRenderer` replaces their runs with revealed runs (`#AARRGGBB` colours, which the shape
  rich text conversion now honours instead of dropping the alpha); the bullets of unrevealed list paragraphs become
  transparent.
- Minor, fixed: picture bullets of an unrevealed paragraph stayed visible. The reveal gives the paragraph a
  transparent bullet symbol (`BulletSymbol.Transparent`), and the rich text painter skips the picture of such a bullet.
- Minor, fixed: the pause screen between loops was plain black. `ShowCurtainCaption` shows "The show starts again in
  N s", updated by `ShowLoopClock` (`SecondsLeft`) four times a second; texts in the five languages.
- Medium, fixed: a transition sound cut at the next slide on the last slide had no PPTX carrier. Research: the
  transition's `p:sndAc` has only `p:stSnd` (with `loop`) and `p:endSnd`; there is no play-to-end attribute in
  ECMA-376 or in the PowerPoint 2010 transition extensions, and the cut on other slides is already carried by a native
  `p:endSnd` on the next slide. On the last slide, where no next slide exists, the writer adds a `p:extLst/p:ext` to
  the slide transition with our `urn:pulseoffice:presentation-sound` `soundCut` element; other readers ignore the
  extension, and our reader restores `SoundPlaysToEnd = false`.

Tests: `Round286IteratedEffectTests` (Presentations), `Round286PauseCountdownTests` (Presentations.UI),
`Round286LastSlideSoundCutTests` (Formats.OpenXml); `Round283IteratedTextTests` updated to the new reveal signature.

## Still open (queued)
- Each unit of an iterated fly or zoom fades in place; the per-unit offset and scale are not drawn.
- The animation timeline of the editor (`AnimationTimeline`) still shows an iterated effect with its single-unit
  duration.
- Number labels of unrevealed numbered list paragraphs in drawn shapes stay visible.
- A fading unit of shape text without its own colour uses the shape's text colour, not the automatic contrast ink.
- None of this has been seen live, nor the PPTX extension opened in PowerPoint.
