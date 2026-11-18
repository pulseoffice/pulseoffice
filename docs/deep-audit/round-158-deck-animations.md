# Round 158: Deck animations and timing, ODP and PPTX

Area: Deck animations, transitions and timing across ODP and PPTX (rule 9).

## Defects

1. **PPTX after-previous timing (medium).** `PptxAnimationMapper` wrote every effect of a click group into one
   parallel node with no offset, so an "after previous" effect started together with the effect before it; a main
   sequence whose first effect was "with previous" or "after previous" still waited for a click. Fix: the shared
   `AnimationSteps` service (`PulseOffice.Formats`) splits a click group into steps at every "after previous" effect;
   each step is a `p:par` delayed by the summed length (delay + duration x repeat) of the steps before it, and an
   automatic first group adds an `onBegin` condition on the main sequence. Test: `Round158SequenceTimingTests`.
2. **ODP after-previous timing (medium).** `OdfAnimationMapper` had the same flat structure and the same wrong start
   for an automatic first group. Fix: the same steps as nested `anim:par` with cumulative `smil:begin`; an automatic
   first group begins at `0s`, later click groups at `indefinite`, the first group of an interactive sequence at `0s`.
   Test: `Round158OdpSequenceTimingTests`.

Both readers find effect nodes by their preset class anywhere under the timing root, so the extra step level is read
back unchanged; per-effect delays stay relative to their step in the model.

## Verification

Fail-before was verified for all four Round158 tests (the two mappers restored to their previous version). Pass-after
was not run: by the user's rule for this cycle, the coordinator runs the tests once after the join.

## Checked without defects

Native animation passthrough (`CurrentNative`) lapses when category, preset, subtype, duration, repeat or motion path
change, so edited effects are rewritten from the model; ODP `smil:repeatCount` and PPTX `repeatCount` round-trip.

## Open

Not audited in this round: preset tables by native identifier, triggers on shapes, motion paths, text-by-paragraph
builds, slide transitions and their options, the animation pane commands and their undo.
