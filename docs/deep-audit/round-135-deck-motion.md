# Round 135: Deck animations, transitions and slide show

## Coverage (files read in full)

| File | Lines | Outcome |
|---|---|---|
| src/PulseOffice.Formats.OpenXml/PptxTransitionMapper.cs | 255/255 | two defects, fixed |
| src/PulseOffice.Formats/SmilTransitionMapper.cs | 117/117 | one defect, fixed |
| src/PulseOffice.Formats.OpenXml/PptxAnimationMapper.cs | 278/278 | one open defect (triggers) |
| src/PulseOffice.Formats.Odf/OdfAnimationMapper.cs | 215/215 | same open defect (triggers) |
| src/PulseOffice.Presentations/AnimationSchedule.cs | 242/242 | clean |
| src/PulseOffice.Formats/AnimationClickGroups.cs | 17/17 | clean |
| src/PulseOffice.Formats/NativeTransitionTable.cs | 91/91 | clean |
| src/PulseOffice.Presentations/Animation.cs | 84/84 | no trigger shape or rewind in the model |
| src/PulseOffice.Presentations/SlideTransition.cs | 80/80 | clean |
| src/PulseOffice.Presentations/TransitionCatalog.cs | 20-68 | clean |
| src/PulseOffice.Presentations.UI/DeckAnimationPlayer.cs | 65/65 | clean |
| ODP transition style writer and reader (StyleBuilders, StyleTables), AnimationReader | relevant sections | fixed with the extension |

Not reached in this round: the behaviour codecs (ODF and PPTX), preset tables, transition effects and painters, the
animation pane, presenter window, rehearsal timeline and video export. They remain for the next round.

## Defects

### Grave
1. **Morph written as fade through black in PPTX** (PptxTransitionMapper.cs, `Effect`, former line 111): Morph had no
   entry in the effect list, so the fallback was the first entry, Reveal (`p:fade thruBlk="1"`), while the declared loss
   PPTX024 said "fade". Fixed: Morph is now written natively as `p159:morph option="byObject"` in an
   `mc:Choice Requires="p159"` with a plain `p:fade` fallback, as PowerPoint 2019 and later save it, and read back as
   Morph (other options are kept as the native payload). PPTX024 is no longer raised.
2. **Native transitions from p15/p159 in the wrong place** (PptxTransitionMapper.cs, `Append`): any native effect outside
   the p14 namespace was copied into the `mc:Fallback` too (an element older readers cannot understand) and the choice
   always required `p14` even when it contained a p15 element. Fixed: only PresentationML effects go into the fallback,
   and `Requires` names the namespace of the effect (p14, p15 or p159), declared on the slide.
3. **Triggered animations (interactive sequences) flattened into the main sequence** (PptxAnimationMapper.cs:174,
   OdfAnimationMapper.cs:134): effects inside `p:seq nodeType="interactiveSeq"` and ODF `interactive-sequence` are read as
   ordinary main-sequence effects, so a shape that should animate when another shape is clicked animates on the next
   click instead, silently. Both formats carry triggers natively; the gap is the model (`AnimationEffect` has no trigger
   element) and the slide show runtime. **Open**: needs a model field, both mappers and the show's shape click handling.

### Medium
4. **Cut, zoom and morph lost in ODP** (SmilTransitionMapper.cs:14,22,23): all three were written and re-read as a
   crossfade. ODF 1.3 SMIL has no equivalent type. Fixed with our extension attribute `kind` in namespace
   `urn:pulseoffice:transition` on `style:drawing-page-properties`; other applications still see the crossfade, so ODP021
   stays as an information message with reworded text.

### Minor (open)
5. ODF animation durations are capped at 24 hours on reading while PPTX caps at one hour (OdfAnimationMapper.cs:185).
6. Rewind after playing and the "on click and after time" combination of PPTX transitions are not in the model.

## Tests
`tests/PulseOffice.Formats.OpenXml.Tests/Round135TransitionTests.cs` (6 tests). Formats.OpenXml.Tests filtered on
presentation, transition and animation tests: 1254 passed. Formats.Odf.Tests filtered on ODP, transition, presentation
and animation tests: 216 passed.

## Live checks
Open a PPTX with a Morph slide written by PulseOffice in PowerPoint (it should show Morph, and older versions a fade);
open an ODP with a cut or zoom transition in the reference suite (crossfade expected, no error).
