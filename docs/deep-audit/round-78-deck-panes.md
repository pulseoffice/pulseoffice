# Deep audit round 78: Deck element behaviours and groups

## Coverage
Read in full: `ElementBehaviours.cs`, `ElementIdRemap.cs`, `SlideGroup.cs`, `DeckElementActions.cs` (clone and delete
paths), `Exchange/DeckExchange.cs`, the duplicate path of `DeckClipboardCommands.cs`, the clone path of `SlideFactory`.
Not done in this round and still open: full reading of the animation pane, the transitions pane, sections, Deck dialog
input validation, five-language texts of Deck commands, and the MCP agent paths driving Deck commands.

## Defects
- Medium, `DeckElementActions.AddClones` (duplicate, paste within a deck and across decks): animations and object action
  settings were not carried. Fixed: `IElementBehaviourCloning` (`src/PulseOffice.Presentations/ElementBehaviourCloning.cs`)
  takes the behaviours of the selection (`Of`) and remaps them onto the clones (`Cloned`), with fresh animation ids
  unique on the target slide; carried effects are appended to the target slide's sequence, as the reference editors do.
  `DeckNative.Behaviours` carries them through `PulseOffice.Exchange` (captured before a cut deletes the source). One undo
  step. The model has no trigger-on-another-shape reference, so only effects whose target is copied are carried.
- Medium, `ElementBehaviours`: deleting grouped elements left dangling member ids in `SlideGroup`. Fixed: members are
  pruned, emptied groups disappear (cascading to their parents), restored on undo, in the same step.

## Noted, not changed
- Across decks, an animation or action sound path and a GoToSlide reference keep the source deck's value.

## Tests
`tests/PulseOffice.Presentations.UI.Tests/DeckRound78BehaviourTests.cs` (3 tests). PulseOffice.Presentations.UI.Tests
92/92, PulseOffice.Presentations.Tests 327/327, PulseOffice.Exchange.Tests 25/25.
