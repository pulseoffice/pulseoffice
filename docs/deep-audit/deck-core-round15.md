# Deep audit round 15: Deck core

Area: `src/PulseOffice.Presentations` (the `.UI` projects were reviewed in rounds 8 and 9). The whole core was already
read once in the first Deck audit (`deck.md`); this round re-read in full the central files on inheritance, timing,
show navigation and slide operations, hunting the specific risks of the round.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| AnimationSchedule.cs | 241/241 | clean: sums saturated in `long`, repeat and duration at least 1, progress modulo the iteration |
| Animation.cs | 84/84 | clean |
| AnimationTimeline.cs | 68/68 | clean: shares guarded against a zero total |
| PresentationController.cs | 128/128 | clean: step counts fixed at start, every path terminates |
| Presentation.cs | 191/191 | clean: master 0 cannot be removed, a missing master falls back to master 0 |
| SlideMaster.cs | 70/70 | clean: inheritance is flat (slide → master by id → layout by kind → blank), no chain that can loop |
| SlideSections.cs | 65/65 | clean |
| Slide.cs | 265/265 | clean |
| ElementIdRemap.cs | 27/27 | clean |
| SlideFactory.cs | 166/166 | **two defects, fixed** (below) |

Readers checked for the caps on file values: repeat counts are capped at 100 in both mappers
(`OdfAnimationMapper.RepeatCount`, `PptxAnimationMapper.RepeatCount`), delays and durations at `MaxTimingMs` or one day.
Video export already streams frames through `IDeckVideoFrameSource` with cancellation (`DeckExportCommands.cs:156`),
so the round-1 "all frames in memory" point is no longer current.

## Defects

- **Medium, fixed** — `SlideFactory.cs:84` (`Relayout`) and `SlideFactory.cs:37` (`Clone`): changing the layout of a
  slide, or duplicating or importing it, silently dropped its native diagrams (`Slide.Diagrams`), so a SmartArt slide
  re-exported to PPTX lost the diagram data and kept only the flattened shapes. Now `Relayout` keeps every intact
  diagram whose members survive, and `Clone` copies intact diagrams with members and point keys remapped to the copies.
- **Medium, fixed** — `SlideFactory.cs:54` (`Clone`): `ToDictionary` over element ids threw `ArgumentException` when a
  slide held two elements with the same id, so "Duplicate slide" failed. Such slides came from ODP files with two
  media frames sharing `xml:id`/`draw:id`, because `MediaReader` used the file id as is. Root fixed in the reader
  (`src/PulseOffice.Formats.Odf/Presentation/Reading/PageReaders.cs:158`, outside the area, minimal: an id already
  present on the slide is replaced by a generated one) and `Clone` made tolerant.

Tests: `tests/PulseOffice.Presentations.Tests/DeckCoreRound15Tests.cs` (three), all verified to fail before the fix.
`PulseOffice.Presentations.Tests` 310/310; `PulseOffice.Formats.Odf.Tests` filtered on Odp/Media/Deck 189/189.

## Remaining

- Diagrams whose text was edited (`TextEditedDiagrams`) are not carried by `Clone`; duplicating such a slide exports
  the flattened shapes only.
- Animation ids of repeated duplicates grow by one slide prefix per generation (`slide6.slide5.anim1`): harmless.

## Live checks

Open a PPTX with SmartArt, change the slide layout and duplicate the slide, save as PPTX and reopen it in PowerPoint:
the diagram must remain editable. Duplicate a slide of an ODP with two media frames sharing an id.
