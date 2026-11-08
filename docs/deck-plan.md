# The plan for Deck: towards parity with the reference presentation program and PowerPoint

> Deck must be **as powerful as PowerPoint** and read/write **ODP and PPTX 100 %** (or with the
> loss declared). The goal is parity of **capabilities** — not copying the menus, names or code
> of other office suites: Deck's ribbon and menus are **our own**, as for Ledger.
> Reference and measurement: the command census of the reference presentation program and its coverage script.
> Coverage today: **2 of 387 (0.5 %)**.

The previous "six blocks" (model, masters/themes, formats, transitions/animations, slide show,
media/sounds/events) built **the invisible foundations**: the document, the two formats, playback.
They were necessary and are done and tested. **They were not functional parity**, and it was a
mistake to imply otherwise. From here on the blocks are the ones the user sees and uses.

## Order of the blocks

### D1 — Deck's command surface (foundation)
The command registry for the Presentations module and its projection onto **ribbon, menus, palette and
shortcuts**, with the same infrastructure as Ledger (`ICommandLayout`, analogues of `PulseRibbonLayout`/
`PulseMenuLayout` but **specific to Deck**). Deck's ribbon tabs — designed by us, not copied from
the reference program — presumably: **Home** (clipboard, font, paragraph, arrange), **Insert**
(table, image, shapes, chart, text box, fontwork, media, symbol, header/footer),
**Drawing** (shapes, align, arrange, group), **Transitions**, **Animations**, **Slide Show**
(set up, rehearse, custom, present), **View** (modes, master, show, zoom), **Review**
(spelling, comments, language). The slide-editing and inspector commands that today
live as buttons in `DeckView` become declared commands.
*Completion criterion*: opening a Deck populates the ribbon and menus with real Deck commands, and
startup reports no command without title/icon/rule. **This is the block that fixes the rejected UX.**

### D2 — Rich text formatting
`TextParagraph`/run grows to what ODP and PPTX describe: family, size, bold, italic,
underline, strikethrough, superscript/subscript, colour, highlight, spacing, small caps; for the
paragraph: alignment (four), indents, line spacing, spacing before/after, **multi-level** bulleted and
numbered lists, tabs, direction. Commands on the ribbon (Home) and persistence in both
formats with `CheckFidelity`. Text selection inside the box (not only the whole paragraph).
*Criterion*: bold/colour/list on a selection, saved and reread in ODP and PPTX.

### D3 — Inserting objects
Place on the slide, anchored and manipulable, the objects the project **already has** as
`EmbeddedObject`: raster and SVG **image**, **table**, **chart**, **shape** (all families),
**text box**, **fontwork**, **formula**, **QR**, **comment**. Reuse of Ledger's renderers
(`IDrawingObjectRenderer`). Persistence in both formats.
*Criterion*: insert a shape, an image and a table, move them, save them and reread them in ODP and
PPTX.

### D4 — View modes
**Sorter** (thumbnail grid, reorder by dragging, sections), **Outline** (text outline
⇄ slides), **Notes** (notes page), **Master** (slide/notes/handout as editable
views). The shell hosts several views of the same document; switching is a command.
*Criterion*: reorder in the sorter, write two titles in the outline that become slides.

### D5 — Object manipulation
Exact position and size, **arrange** (z-order), **align and distribute**, **group/ungroup**,
rotate and flip, connectors, handles on the canvas with multiple selection (reuse of Ledger's `ObjectManipulator`
and `ObjectAlignment`). Every operation undoable.
*Criterion*: align three shapes and group them, with ⌘Z undoing.

### D6 — Full slide show and presenter view
**Slide show setup** (from/to, loop, without animations, pen/laser, monitor), **rehearse timings**
(records the time per slide), **custom slide shows**, **show/hide slide**,
**sections**. The **presenter view**: notes, next slide, timer, navigator, on a second
screen. Automatic advance from the timings.
*Criterion*: a custom slide show of two slides out of three, with the presenter view
showing notes and time.

### D7 — Presentation-specific exports
**PDF** with options (slides, notes, n-up handouts), per-slide **images** (PNG/JPG),
**video** (mp4 with timings and animations), **HTML**. They rest on `IPageCanvas`/`PulseOffice.Formats.Pdf`,
already written.
*Criterion*: a 4-up handout PDF and an mp4 with timed advance.

### D8 — Finishing touches
Fields (number, date, time, author, title, count, file name), **headers and footers**, rulers,
grid and guides with snapping, Deck options, shortcut customisation, gallery,
symbols and special characters, hyperlinks, text autocorrection.
*Criterion*: a footer with slide number and date on all slides, saved in both formats.

## How we work (rules already in force)

- **In verifiable blocks**, each closed with the application open in front of us and tested live,
  not only by tests.
- **Rule 7**: every feature goes through ODP **and** PPTX, or the loss is declared in `CheckFidelity`.
- **Rule 1**: design patterns and DI, no gratuitous statics; the code is ours.
- **Ribbon and menus do not copy other suites or PowerPoint**: structure, names and code are Deck's own.
- Coverage is **measured**: the census script prints it and fails if the map cites one of
  our commands that no longer exists. The number grows only when a capability is truly reachable
  by the user, not when only the engine exists.
