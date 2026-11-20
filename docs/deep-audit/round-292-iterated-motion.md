# Round 292: Deck iterated motion and rounded corners (2026-11-20)

## Scope
The queue items round 286 left open on iterated effects (per-unit motion, timeline span, number labels of unseen
paragraphs in drawn shapes, the ink of fading shape text) and the rounded rectangle item of rounds 285 and 287.

## Defects
- Medium, fixed: each unit of an iterated fly or zoom faded in place. `TextReveal` now carries the effect's motion
  (`Motion`, set by the scheduler only when the motion moves or scales: fade stays a pure alpha reveal) and an optional
  `Layer` of units. Units in flight are left out of the still pass; `ITextIterationReveal.Layers` groups them by start
  time and gives each group the motion's visual at its own progress, and `Layered` returns the element with only those
  units visible and without fill, line, effects or text backdrop. `SlideRenderer` draws the still pass, then each layer
  under its opacity and offset or scale.
- Medium, fixed: the animation pane's timeline showed an iterated effect with its single-unit duration.
  `IAnimationTimeline.Build(Slide)` schedules through `Schedule(Slide)`/`ScheduleTriggered(Slide, …)`, which count the
  target's units; the pane uses it.
- Minor, fixed: number labels of unseen numbered paragraphs in drawn shapes stayed visible (inline labels take the
  paragraph ink). `ShapeListText.Framed` gives a paragraph whose runs are all fully transparent a transparent colour.
- Minor, fixed: a fading unit of shape text without its own colour used the shape's stored text colour even when the
  colour is automatic. `Reveal(ShapeObject, reveal, inkHex)` takes the ink the renderer resolves with `IShapeTextInk`.
- Minor, fixed: a model rounded rectangle without preset adjust values drew elliptic corners at `CornerRatio` of each
  side. The sized `IShapeGeometry.Outline(shape, width, height)` draws circular corners of `CornerRatio` times the
  short side, as `roundRect` and the ODF round rectangle do, and `ShapeTextArea` insets the text alike on every side.

Tests: `Round292IteratedMotionTests` (Presentations), `Round292RoundedCornerTests` (Drawing),
`Round292UnseenListLabelTests` (UI.Controls).

## Still open (queued)
- A zoomed unit scales about the element's centre, not its own; a unit's layer also draws its paragraph's label.
- The automatic ink of a revealed unit ignores a picture fill's average colour.
- The unsized `Outline(shape)` keeps elliptic corners for model rounded rectangles.
- Nothing of this has been seen live.
