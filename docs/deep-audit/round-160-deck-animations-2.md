# Round 160: Deck animations, second pass

Scope: presets carried by native identifier through ODP and PPTX, triggers on shapes, motion paths, text-by-paragraph
builds, slide transitions and their options, the animation pane commands and their undo. Tests were written but not run
in this round (the coordinator runs them after joining the four audits).

## Fixed

### Medium
1. **A repeated effect on the same shape vanished in PPTX** (`PptxAnimationMapper.Groups`): within one click group the
   writer skipped any effect with the same shape, paragraph, category and preset as an earlier one. The rule exists for
   diagram members that share one graphic frame, but it also dropped a second "spin" with previous and a delay on the
   same shape, which ODP kept. The writer now skips only an effect of a different model element mapped to the same
   shape id.
2. **Sound added to or changed on an effect read from ODP was lost** (`OdfAnimationMapper.Native`): the native path only
   removed `anim:audio` when the sound was cleared, so a new sound was never written and a changed one kept the old
   file. The audio element is now kept only when it already names the current sound, otherwise replaced.
3. **Text-by-paragraph builds had no PPTX build list** (`PptxAnimationMapper.ToTiming`): effects aimed at one paragraph
   wrote `p:spTgt/p:txEl/p:pRg` with no `p:bldLst`, the declaration PowerPoint expects for paragraph builds. The timing
   now ends with one `p:bldP build="p"` per shape animated by paragraph.

## Checked clean
- Native identifier carriage: ODF and OOXML effects keep their native XML while `NativeAnimation.Describes` holds; the
  writer refreshes begin, fill, node type, preset class, id, subtype, targets and paragraph on both sides.
- Triggers: interactive sequences in both formats, trigger shapes resolved on read, triggers on missing shapes fall
  back to the main sequence in PPTX.
- Motion paths, repeat (including indefinite), rewind, delay and duration caps.
- Transitions: kind and direction through SMIL and PPTX, exact duration through `smil:dur` and `p14:dur`, the three
  advance modes, transition sound and loop in both formats.
- Animation pane: add, edit, move, remove and preset change all go through `ReplaceAnimations`, one undo step each with
  a no-op check; transitions to one or all slides are one undo step.

## Open
- ODF has no native "loop sound until next"; it is carried as `presentation:play-full="false"`, which round-trips in
  PulseOffice but means something else to other readers. Left as is pending a check of how other applications store it.
- Fix 3 is not yet verified in real PowerPoint.

## Tests
`tests/PulseOffice.Formats.OpenXml.Tests/Round160AnimationTests.cs` (3 tests).
