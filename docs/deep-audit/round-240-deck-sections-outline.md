# Round 240: Deck sections, outline and shape text

Audit of Deck sections, the outline view and drawn shape text, starting from the queued items of rounds 229-236.
Nothing was built or run in this round (audit cycle rule); the coordinator builds and tests at the join.

## Defects

| Severity | Defect | Fix |
|---|---|---|
| Medium | A slide could not be dropped into an empty section, in the filmstrip or the sorter | `DeckSlideActions.MoveSlideIntoSection`: the slide moves before the slide the empty section precedes (or to the end), takes the section's name and identifier, and the empty section is consumed; one undo step. `DeckSectionHeaders.DropInto` finds the empty header under the pointer; the sorter checks it before its slide drop target, the filmstrip on release of a left press |
| Medium | Two slides with the same title whose outline blocks were swapped by hand kept their old order | `OutlineSlideMatching` pairs whole blocks (title and body lines) first, then titles alone, then fills gaps by position |
| Medium | A title containing a pilcrow was split into paragraphs when the outline was applied | The outline writes a literal pilcrow doubled; the reader turns a doubled pilcrow back into one and splits only on single ones |
| Medium | Deck link tips ignored links inside drawn shape text | `DeckLinkTips` composes the shape's framed rich text (`IShapeListText.Framed`) over the element bounds and hit-tests it, unrotating the pointer |
| Minor | Empty sections could be removed only one at a time through the header's own context menu | New registry command `deck.section.removeEmpty` (Remove Empty Sections, one undo step), in the Section menu and context menu, translated in the five languages |
| Minor | Shape text column breaks probed a highlight geometry per character up to 4000 characters | Texts over 256 characters are probed per line: an exponential then binary search for the last character on each line top, so a long text costs a few geometries per line |

## Open

- Renaming or removing one particular empty section still goes through the header (double click or its context menu),
  not a registry command: commands have no argument to name a section that holds no slide.
- Dropping a slide on an empty section has no drop indicator; the filmstrip has no other slide drag.
- Long shape text line probing assumes highlight tops are constant along a line and increase from line to line
  (horizontal flows); a line whose characters report different tops falls back to more lines than drawn.
- Mongolian lines inside shape or text box columns and list labels of mirrored Mongolian paragraphs: not addressed.
- Outline matching by whole block still cannot tell apart two blocks that are identical in title and body.
