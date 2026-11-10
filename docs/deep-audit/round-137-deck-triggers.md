# Round 137: Deck triggered animations, rewind, click-or-time advance

Closes the open defects of round 135 (round-135-deck-motion.md).

## Defects fixed

### Serious
1. **Triggered animations flattened into the main sequence** (round 135: PptxAnimationMapper.cs:174,
   OdfAnimationMapper.cs:134). Effects of a PPTX `p:seq` with `nodeType="interactiveSeq"` and of an ODF
   `anim:seq presentation:node-type="interactive-sequence"` were read as main-sequence effects, so they played on the
   next click instead of on the click of their trigger shape.
   - Model: `AnimationEffect.TriggerShapeId` (null = main sequence) and `IsTriggered`.
   - PPTX: effects are grouped per trigger shape into `p:seq concurrent="1" nextAc="seek"` with an `interactiveSeq`
     `p:cTn` (`restart="whenNotActive"`, `evtFilter="cancelBubble"`), `p:stCondLst`/`p:nextCondLst` holding
     `p:cond evt="onClick"` with `p:tgtEl/p:spTgt` of the trigger, and `p:endSync`; the first click group starts at
     delay 0, later groups at `indefinite`. The main sequence is omitted when only triggered effects exist. Reading takes
     the trigger from the nearest `interactiveSeq` ancestor's start condition.
   - ODP: one `anim:seq` per trigger with `smil:begin="<shape-id>.click"` and `presentation:node-type="interactive-sequence"`;
     reading takes the `.click` event base from the ancestor sequence and resolves it like effect targets.
   - Slide show: `SlideAnimationPlan` (main schedule plus one schedule per trigger) and `PresentationController.Trigger`
     (each click on the trigger plays the next group, wrapping like `restart="whenNotActive"`; state resets on slide change).
     `PresentationWindow` plays the trigger on click (before interactive actions, after image maps and media), and every
     frame takes element visuals from `PresentationController.VisualAt`, so a triggered entrance target stays hidden
     until its trigger is clicked. Triggered effects are no longer click steps.
   - Editing: the animation pane shows "Start on click of …" on triggered rows (no click number) and offers a trigger
     chooser ("As part of click sequence" or any shape of the slide). Rows keep list order, so drag and reorder still
     address the right effect. Deleting the trigger shape removes its sequence (undoable with the element); duplicated
     slides remap the trigger; copied elements keep it only when the trigger is copied too; layout changes drop a
     trigger whose shape is gone.
   - Media bookmark triggers: the model has no media bookmarks, so a PPTX interactive sequence started by
     `onMediaBookmark` is read as triggered by the media shape (click of the shape) rather than flattened into the main
     sequence. No declared loss was added; this stays a residual note.

### Minor
2. **Duration caps differ by format**: ODF read delays and durations up to 24 hours (anything longer fell back to the
   default), PPTX clamped at one hour. Both now clamp to `AnimationEffect.LongestTimingMs` (one hour), also for the
   behaviour-derived natural duration in ODF.
3. **Rewind and click-or-time advance missing**:
   - `AnimationEffect.Rewind`: PPTX `fill="remove"` on the effect `p:cTn` (also on natively carried effects), ODF
     `smil:fill="remove"`; the show undoes the effect when done (an entrance hides again). Pane checkbox "Rewind when done
     playing".
   - `AdvanceMode.OnClickOrAfterTime` and `SlideTransition.AdvancesAfterTime`: PPTX `advClick="1"` with `advTm`
     (a file with `advTm` and no `advClick` is now read as this mode, which is what the schema default means); ODF has no
     native combination, so it writes `presentation:transition-type="automatic"` with the duration plus our
     `pulsetransition:advance-on-click="true"` (namespace `urn:pulseoffice:transition`), which other readers ignore and
     still advance after the time. The transition editor has a third choice "On click or after time"; the show's auto
     advance and video export treat it like after-time.

No new declared-loss codes were needed.

## Tests
- `tests/PulseOffice.Formats.OpenXml.Tests/Round137TriggerTests.cs` (9): PPTX shape, PPTX and ODP round trips of
  trigger and rewind, ODP then PPTX, reading a hand-written interactive sequence, the three advance modes in both formats,
  the unified one-hour cap.
- `tests/PulseOffice.Presentations.Tests/Round137TriggerRuntimeTests.cs` (7): triggered effects are not steps, trigger
  click reveals its target, reset on slide change, no triggers without animations, rewind, timeline order and numbers,
  deleting the trigger shape.
- Runs: Formats.OpenXml.Tests filtered on presentation, PPTX, animation, transition, deck and round 13x tests: 1330
  passed. Formats.Odf.Tests filtered on ODP, presentation, animation, transition and deck: 223 passed.
  Presentations.Tests: 347 passed. Presentations.UI.Tests filtered on animation, transition, motion, show,
  presentation and localization: 131 passed. Architecture.Tests: 43 passed. Localization.Tests: 32 passed.

## To check live
- In the animation pane, set an effect's trigger to a shape: the row shows "Start on click of …"; in the show, clicking
  that shape plays it, a click elsewhere advances the main sequence only.
- Open a PowerPoint deck with a trigger in PulseOffice, save as PPTX and ODP, and reopen in PowerPoint and in the
  reference suite: the trigger must still fire on the shape click.
- "Rewind when done playing" and "On click or after time" in both programs.
