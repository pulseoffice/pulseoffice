# Deep audit round 80: Deck panes, sections, dialogs, texts, agent

## Coverage
| File | Lines read | Outcome |
|---|---|---|
| `src/PulseOffice.Presentations/ElementBehaviourCloning.cs` | 52/52 (rewritten) | defect 1 |
| `src/PulseOffice.Presentations/SoundLibrary.cs` | 52/52 | `Remove` added for undo |
| `src/PulseOffice.Presentations.UI/Exchange/DeckExchange.cs` | 1-200/228 | capture and paste paths |
| `src/PulseOffice.Presentations.UI/DeckElementActions.cs` | 1-22, 100-175 | clone path |
| `src/PulseOffice.Presentations/SlideSections.cs` | 65/65 | ok |
| `src/PulseOffice.Presentations.UI/DeckEditCommands.cs` | section commands (117-177) | ok |
| `src/PulseOffice.Presentations.UI/DeckSlideActions.cs` | section paths (40-60, 185-260) | ok |
| `src/PulseOffice.Formats.Odf/Presentation/Writing/DocumentWriters.cs` | 1-80, 280-300 | defect 2 |
| `src/PulseOffice.Formats.Odf/Presentation/Reading/SlideReading.cs` | 60-91 | defect 2 |
| `src/PulseOffice.Formats.Odf/Presentation/OdpSounds.cs` | 42/42 | ok (linked sounds declared) |
| `src/PulseOffice.Agent/Tools/CommandTools.cs` | 159/159 | defect 3 |
| `src/PulseOffice.Presentations.UI/Motion/AnimationPane.cs` | 451/451 | defect 5 |
| `src/PulseOffice.Presentations.UI/DeckMotionCommands.cs` | 102/102 | defect 4 |
| `src/PulseOffice.Presentations.UI/DeckInspector.cs` | 160-215 | defect 4 |
| `src/PulseOffice.Presentations/AnimationTimeline.cs` | 30-68 | ordering ok |
| `src/PulseOffice.Presentations.UI/Motion/TransitionEditor.cs` | 354/354 | ok, note |
| `src/PulseOffice.Presentations.UI/Motion/DeckMotionEditors.cs` | 37/37 | ok |
| `src/PulseOffice.Presentations.UI/ObjectDialogs/PositionSizeDialog.cs` | 52/52 | ok |
| `src/PulseOffice.Presentations.UI/DeckInteractionForm.cs` | 68/68 | defect 6 |
| `src/PulseOffice.Presentations.UI/DeckSlideSetupCommands.cs` | 35/35 | ok |
| `src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs` | 228-285 | declared sound losses ok |
| Five resource files | scripted check of every `deck.*` / `command.deck.*.title` key used in `PulseOffice.Presentations.UI` (742 keys) | all present in it, en, de, es, fr |

Not read in this round and still open: the generated presentation tools of `ScriptApiCatalog` for the agent, the
remaining Deck dialogs (`ShapeStyleDialog`, `AltTextDialog`, `ChartEditDialog`, `DeckTextDialogCommands`, the
`Design` slide size view), `AnimationPresetThumbnail`, `TransitionThumbnail`, `TransitionDialogView`.

## Defects
1. Medium, `ElementBehaviourCloning` (round 78's limit): pasting into another presentation kept the source deck's sound
   paths, which pointed to nothing or to a different sound of the target, and its GoToSlide references, which named a
   slide of the other deck. Fixed: `Of` captures the origin deck and its embedded sounds; `Cloned` into another deck
   embeds them under free `Media/soundN` paths (reusing an identical sound already there), remaps the paths and drops
   GoToSlide actions; the sounds are added and removed in the same undo step. Linked (outside) sounds keep their path.
2. Medium, ODP sections: sections were dropped in the default format (declared ODP011). Fixed: written and read as our
   attribute `name` in `urn:pulseoffice:presentation-section` on `draw:page` (`OdpSectionXml`); ODP011 removed.
3. Medium, `AgentCommandPolicy`: `deck.slide.insertFromFile`, `deck.insert.imageLink` and `*.image.externalEdit`
   (which writes a temporary file and launches an external program) needed only the edit level. Now files level.
4. Minor, animation dialog: an object without a name was shown with its CLR type name (`ShapeObject`), and the
   interaction rows of the inspector called every non-text element "media". Fixed with the shared
   `IDeckElementNames` (`Motion/DeckElementNames.cs`) used by the inspector and the dialog.
5. Minor, `AnimationPane`: choosing another preset kept the old subtype and motion path, so the new preset was written
   with a direction or path of the previous one. Fixed in `IAnimationOrdering.Rechosen`.
6. Minor, `DeckInteractionForm`: a hyperlink action with an empty address was created, and clearing the address kept
   an empty action. Now no action is created (or the existing one is removed).

## Noted, not changed
- `TransitionEditor` browse button uses an async lambda event handler (the usual Avalonia pattern); the chosen sound is a
  link, declared for PPTX (PPTX006), kept by ODP.

## Tests
`tests/PulseOffice.Presentations.UI.Tests/DeckRound80PasteTests.cs` (2), `DeckRound80MotionTests.cs` (3),
`tests/PulseOffice.Formats.OpenXml.Tests/DeckSectionsParityTests.cs` (2), `tests/PulseOffice.Agent.Tests/SecurityTests.cs`
(4 cases). Presentations.UI.Tests 97/97, Presentations.Tests 327/327, Formats.Odf.Tests 802/802,
Formats.OpenXml.Tests 2389/2389, Agent.Tests 75/75; Desktop builds with no warnings.
