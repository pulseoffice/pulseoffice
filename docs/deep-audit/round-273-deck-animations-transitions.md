# Round 273: Deck animations, transitions and slide show (2026-11-19)

## Scope reached
- PPTX transitions (`PptxTransitionMapper`): effect table, directions, speed and exact `p14:dur`, advance on click and
  after time, alternate content for extension effects, morph, native payloads.
- PPTX animations (`PptxAnimationMapper`): click groups, with/after previous steps, delays, repeat, rewind (fill
  remove), paragraph targets and build list, interactive sequences on a trigger shape, native preset carriage.
- ODP transitions (`SmilTransitionMapper`, drawing page style reading): SMIL types and subtypes, reverse direction,
  extension kinds, speed and `smil:dur`, automatic advance.

## Defects
- Medium, fixed: a PPTX transition without `p14:dur` read its speed as 300/500/900 ms and an absent `spd` as medium.
  ECMA-376 makes `fast` the default, and PowerPoint plays fast, medium and slow as 0.5, 0.75 and 1 s. Files from
  PowerPoint 2007 and from other suites (which write only `spd`) came back with wrong durations, and the written
  fallback speed did not match the duration. Now read as 500/750/1000 with `fast` by default, and written as the
  nearest speed. Test: `Round273TransitionSpeedTests`.
- Minor, queued: `advClick="0"` without `advTm` is read as advance on click (the model has no "advance only by
  trigger" mode).
- Minor, queued: `repeatCount="indefinite"` and repeat until end of slide/next click have no model value; they survive
  only through the carried native XML and are lost when the effect is edited or crosses to ODP.

## Not covered in this round (queued)
- ODP animation round trip (`OdfAnimationMapper`, `anim:*`, `smil:*` timing, iterate by word/letter).
- Text build by word and letter in PPTX (`iterate`) beyond reading the codec.
- Slide show runtime: rehearse timings, loop, kiosk, hidden slides, custom shows navigation (`PresentationWindow`,
  `DeckShowActions`), and transition sounds (`PptxSounds`, `presentation:sound`).
