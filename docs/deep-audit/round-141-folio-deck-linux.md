# Round 141 (part B): Folio, Deck and the exchange between modules, live on Linux

Second Linux round and the first one driven live: the app ran on a private Xvfb display through
`tools/uiprobe-linux` (xdotool and ImageMagick), with the GTK file chooser for save and open. Defects were found
from behaviour, then traced to their cause in the code.

## Method

- Debug build of `src/PulseOffice.Desktop`, started on its own display with a throwaway data folder; every step
  captured and inspected, the app log read after each scenario.
- Folio: a report typed from the keyboard (heading styles by shortcut, autoformatted bullet list, a 3 by 3 table from
  the insert dialog accepted with Enter, cell navigation with Tab, a footnote, a table of contents from the command
  palette), thirty undo steps and thirty redo steps, caret motions.
- Deck: a new presentation, title and subtitle typed, three slides added from the palette, Escape handling, transition
  panel, save as PPTX through the file chooser, close and reopen, slide show started with F5, advanced and closed
  with Escape.
- Exchange: a Ledger column chart copied and pasted into a Deck slide and into a Folio document; the presentation saved
  as PPTX and the document as DOCX, both reopened with the chart intact.
- Tests run per project under `systemd-run --user --scope` with a memory cap; each new test was seen red without its
  fix (compile failure for the new API, or an assertion failure for the Escape test).

## Coverage

| Area | Outcome |
|---|---|
| Folio typing, heading shortcuts, style box | clean |
| Folio list autoformat | clean (first item not capitalised: minor, left) |
| Folio table dialog, Enter to accept, Tab between cells | clean |
| Folio footnote insertion and editing | clean in the body; defect 3 inside a note |
| Folio table of contents | defect 2 |
| Folio undo/redo, 30 steps each way | clean |
| Folio caret motion by paragraph | defect 1 |
| Deck slide add, transitions panel, slide show start/advance/stop | clean |
| Deck keyboard selection | defect 4 |
| Deck PPTX save and reopen | clean |
| Ledger chart into Deck and Folio, PPTX and DOCX round trip | clean |
| App log | one startup warning (defect 5), nothing else |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `FolioKeyMap`, `CaretNavigator` | Ctrl+Up/Down (Alt+Up/Down on macOS) did nothing: there was no paragraph motion, which both word processors used as specification provide. | `CaretMotion.ParagraphUp/ParagraphDown` computed on the story's paragraph order (start of the current paragraph, else of the previous one; start of the next, else end of the story), bound with and without Shift. Tests on the navigator and the key map. |
| 2 | medium | `IndexEditing`, `FolioIndexCommands` | With the caret in a footnote, Insert table of contents put the index section inside the note: the note citation merged with the index title, and the index was never refreshed, since indexes are only looked up in the body. | `IIndexEditing.CanInsert` (caret story is the body); `Insert` refuses other stories; the table of contents and index commands are enabled only there. Test on the model. |
| 3 | medium | `DocumentEditor.InsertNote`, note commands | Insert footnote inside a footnote created a nested note that no layout draws and no format can store; the caret moved into it and typed text vanished. | `IStoryCatalog.RoleOf` gives every story a role (body, note, comment, object, header/footer); `AcceptsNotes` admits the body and text frames only; the model refuses other stories; footnote, endnote and special note commands are disabled elsewhere. Index insertion uses the same role. Test on the model. |
| 4 | medium | `DeckStageKeys`, `SlideView` | Escape after text editing left the object selected and a second Escape did nothing, so the keyboard alone could not get back to the slide (where the arrows change slide). | `SlideView.ClearSelection` empties the selection and raises `SelectionChanged`; Escape on the stage calls it. Headless test seen red before the fix. |
| 5 | minor | `DeckIcons` | `deck.layouts.gallery`, put on the ribbon in round 140, had no icon; only a startup warning said so. | Icon added; a composition test now fails for any ribbon command without an icon. |

## Open items (minor, left)

- New slide has no default shortcut. Ctrl+M is the common one on Windows and Linux, but on macOS Cmd+M is reserved
  for minimising, and a default gesture is the same on every system; a per-system default would need the gesture
  model to carry one.
- Filmstrip thumbnails draw the placeholder prompts ("Title", "Text") of empty placeholders; the specifications show
  empty placeholders as blank in thumbnails.
- The first item of an autoformatted list ("- primo") is not capitalised, while later items are.
- The command palette ranks a fuzzy match ("Ortografia" for "grafico") above exact substring matches of chart commands.
- Ledger's single-series chart takes the chart type name as its title ("Istogramma") rather than the series name.

## Not verified here

- Full-screen slide show: the virtual display has no window manager, so the show window kept its normal size; start,
  advance and exit were verified.
- ODT/ODP save of the same documents through the file chooser and PDF export were not reached in this round's time.
- Long-running operations with dialogs (Esc, Tab order) beyond the table dialog and the command palette.
- Interference note: on the shared display :92 another agent's keystrokes arrived once, because both used the same
  probe state folder; the round moved to a private state folder.
