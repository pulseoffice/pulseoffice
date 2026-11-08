# Deep audit round 77: Deck commands and editing

## Coverage
Every Deck command/action/dialog file in `src/PulseOffice.Presentations.UI` already appears as read in full in earlier
logs (deck.md, deck-round2/8/9.md, ledger-deck-round3.md, round-37b-drawing-use.md), so this round did not re-read them
in full. It checked targeted paths: slide duplicate/import (`DeckSlideActions`, `SlideFactory.Clone`), element
paste/duplicate (`DeckElementActions.AddClones`), element deletion with animations (`ElementBehaviours`), master
delete/assign/replace (`DeckDesignActions`), command enabled states in `DeckSlideCommands`.

## Defects
- Medium, `SlideFactory.Clone`: a slide holding a bare id (`x`) and a slide-prefixed one (`s1.x`) produced two
  elements with the same id in the duplicate, and both animations pointed at one of them. Fixed: ids of elements and
  groups are mapped once, made unique, and animations and interactions follow the map. Test
  `DeckCommandsRound77Tests` (failed before the fix).
- Minor, `DeckSlideActions.ImportSlides`: no read-only guard unlike its siblings (the command already forbids
  read-only state, but the action is reached after an async file pick). Guard added.

## Noted, not changed
- Deleting grouped elements leaves dangling member ids in `SlideGroup`; writers skip missing members, no data loss.
- Paste/duplicate of elements does not carry their animations and interactions (design choice so far).

## Tests
PulseOffice.Presentations.Tests 327/327, PulseOffice.Presentations.UI.Tests 89/89.
