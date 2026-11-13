# Development plan

This document orders **everything** that has been requested. It is not a wish list: it is
a sequence, and every phase states *why it is there* and *how we know it is finished*.
It must be updated when a phase closes or when a priority changes.

A rule that applies everywhere: something is "done" when it is **proven on the real application**, not
when it compiles. The checks that found this project's real defects have always
been three: opening the files with another office suite installed, reading the log after using
the app, and looking at the application on screen.

---

## Principles that govern the order

1. **What is cross-cutting comes before what is specific.** The visual language touches
   every screen: every screen added before it is decided is a screen to redo.
2. **A module is finished before another is opened**, because the second copies the patterns of the
   first. Ledger is the model for Deck and for Folio.
3. **Folio last**, as decided at the start: it rests on rich-text layout, the
   riskiest subsystem of the whole project, and must be tackled with the suite already standing.
4. **No placeholders left behind.** A command that answers "not yet implemented" is
   visible debt: either it is implemented, or it is removed from the interface.

---

## Actual state today (2026-11-13)

Phases 0 to 6 are closed; phase 7 is under way. The phase sections below are kept as the record of why the order was
chosen; their "state" lines are those of the time they were written.

| Part | State |
|---|---|
| Ledger | Complete against the reference spreadsheet's command census; 534 of 535 functions (DDE excluded) plus the REGEX functions |
| Deck | Complete against the reference presentation program's census (367 of 367) |
| Folio | Complete against the reference word processor's census (471 of 471 wanted entries) |
| Formats | ODF and OOXML for the three modules; corpus audit over 5016 files at LOSS 0, DECL 127 |
| Cross-cutting | Exchange, macros (JavaScript, IronPython, CPython), MCP agent, digital signatures, PDF with embedded fonts |
| Packaging | dmg, NSIS installer, AppImage, signed automatic updates; built by GitHub Actions since 2026-11-13 |

What remains is phase 7: verification on real Windows and Linux machines and in Word, Excel and PowerPoint, plus the
open points listed in `CLAUDE.md`.

---|---|
| Calculation engine | 526 functions out of 535, dynamic arrays, lambda, topological recalculation |
| ODS and XLSX formats | Reading and writing proven both ways with a real reference suite, styles, conditional formatting, charts |
| Shell | Ribbon, menus, palette, settings, modals, themes, workspace |
| Multilingual interface | Five languages, hot switching, proven in three |
| Ledger grid | Selection, formats, frozen panes, copy and paste, search, sorting, filters, conditional formatting, charts |
| Visual language | Semantic palette, icons, ribbon with collapsing groups |

What **does not exist yet**, said without excuses — projects created but empty:

`Documents`, `Documents.UI`, `Text` (Folio) · `Presentations`, `Presentations.UI` (Deck) ·
`Sheets.Pivot` (model only) · `Data.*` — all six providers · `Exchange` ·
`Formats.Pdf` · `Formats.Interchange` · `Scripting.JavaScript`, `Scripting.Python`,
`Scripting.Editor` · `Platform.Windows`, `Platform.Linux` (frame only).

Four commands are **placeholders** and tell the user so: export to PDF, theme, macros,
about.

---

## Visual direction: decided

**Sharp and technical in structure, warm in the moments.** The mix is not an average between
the two roads — which is exactly what made the interface generic — but a split
with a single rule:

> **Precision is for what works. Warmth is for what floats and for what welcomes.**

| | Tools (ribbon, grid, bars, headers) | Floating or welcoming surfaces (modals, pop-up menus, cards, start screen) |
|---|---|---|
| Radii | tight (4) | generous (12) |
| Borders | one-pixel line, always | none: the shadow separates |
| Shadows | none, except the ribbon's lift | diffuse and wide |
| Spacing | compact rhythm (4 · 8) | wide (16 · 24) |
| Typography | dense and small | larger, more line spacing |
| Motion | immediate | 160 ms, with a barely hinted scale |

**Warmth comes from geometry, space and motion, not from hue.** The greys stay
cool: a technical interface with warm greys becomes muddy, and the only colour
of the application must remain the module accent, otherwise it is no longer noticed.

---

## Phase 0 — Visual language, all the way

**Why now**: every screen we add before inherits the current look, and
Deck and Folio are dozens of screens.

- Decide the direction (above) and rewrite palette, radii, shadows and scale accordingly.
- **Theme of the stock controls**: text boxes, drop-down menus, check boxes,
  scroll bars, menus, tooltips. Today they have Avalonia's personality, and it shows:
  it is the single intervention that changes the overall look the most.
- **Transitions**: hover, menu appearance, tab switching. Today everything
  snaps.
- **Dark theme verified on screen**, not only by tests.
- **Contrast** verified against minimum ratios: an elegant and unreadable interface is
  a broken interface.
- Polish the start screen, command palette, settings, find and replace —
  and translate their internal texts, which are the last strings left in Italian in the code.

**Finished when**: the three main screens and the two dialogs look the same
in light and dark, and no control shows the stock style.

**State: closed.** Direction decided and applied, control theme, transitions, dark theme
verified on screen, contrast measured by a test, and the last views translated. Details in
`CLAUDE.md`.

---

## Phase 1 — A Ledger that is really usable

**Why now**: these are the things missing to use the program for an hour straight without
getting irritated. They are also the model that Deck and Folio will inherit.

- **In-cell editing** completed (today the editor exists but the normal path goes
  through the formula bar).
- **Autofill** by dragging the corner of the selection: it is the most used gesture of
  a spreadsheet after typing.
- **Tabs for open documents** in the shell.
- **Settings persistence**: today the stores are in memory, so theme and language
  are lost at every close. It is the first thing to do in the phase, because it makes any
  other preference added useless without it.
- **Recent documents**, autosave and **recovery after a crash**.
- **Printing and PDF export** (`Formats.Pdf`), which today is a placeholder.
- **Global shortcuts** beyond palette and options, and the window that shows them all.
- Sheet **zoom**.

**Finished when**: a real worksheet is built — data, formulas, formatting,
a chart — saved, the app is closed, reopened and everything is found again, and it is printed.

---

## Phase 2 — Complete Ledger

**Requirement set by the user**: Ledger must **do everything a full desktop spreadsheet does**, or as much of
it as is technically achievable. Not a reasonable subset: a real
spreadsheet.

**The same features, not the same program.** The rule that has governed the
project from the start applies here: the reference suite is the *functional specification*, not the model to imitate.
The yardstick is therefore the feature catalogue — what
a spreadsheet must be able to do — while how it is done, what it is called and how it is
shown remain our choices. Where the reference has dated solutions, we do better.

What the reference spreadsheet has and Ledger does not yet, grouped by subsystem:

**Data analysis**
- Real **pivot tables**: the model exists, calculation and interface are missing. It also unlocks
  `GETPIVOTDATA`.
- **Solver** and goal seek (`GOALSEEK`), scenarios, one- and two-variable data tables
  (`MULTIPLE.OPERATIONS`).
- **Subtotals**, consolidation, duplicate removal, text to columns.
- **Statistics** from the Data menu: descriptive, sampling, correlation, covariance,
  ANOVA, t-test, F-test, Z-test, chi-square, regression, moving averages, exponential
  smoothing, Fourier.
- **Queries attached to sheets**: the six providers (ODBC plus the five native ones), credentials
  in the operating system store, and refresh by clicking inside the range,
  as in Excel. **No provider has ever been tested against a real server**: at least
  one real database is needed for each.
- **Statistical charts** beyond the six drawn types: the catalogue foresees about sixty.
**Structure and integrity**
- **Data validation** with messages and criteria, sheet and cell protection, outline
  levels with groupings, print ranges and repeated headers.
- **Cell comments**, notes, and the detective pane (the arrows that
  show precedents and dependents).
- **Cell and page styles** as document objects, not only direct formatting.
- Window **freezing and splitting**, multiple views of the same document.

**Editing**
- Series fill (dates, days, progressions), full paste special with
  operations and transposition, cut/copy on multiple ranges.
- Find and replace across sheets, with regular expressions and search by format.
- Multi-key sorting with custom lists; autofilter, standard filter
  and advanced filter.
- The other blocked functions that unlock by themselves: `STYLE`, `COLOR`, `GOALSEEK` with the
  solver, `WEBSERVICE` and `FILTERXML` with network permissions.

**Finished when**: Ledger handles a real working document — a pivot on a few thousand
rows attached to a database — with no visible differences from the reference spreadsheet.

**State as of 23 September 2026**: almost all of the list above is done, functions included (534
out of 535). The census redone on the reference spreadsheet's actual menu says, however, that the reachable features
are 117 out of the 373 wanted: what is missing is listed command by command in the command map
and organised into twenty-two ordered blocks in
**[completion-plan.md](completion-plan.md)**, which from here on is the operational plan
of this phase. The first three blocks — merged cells, CSV, raster images — come before
anything else, because today we silently lose other people's data.

---

## Phase 3 — Deck

**Why here**: it is the smallest of the three modules and reuses almost everything that exists —
shell, commands, embedded objects, charts, ODF package.

- Presentation model: slides, masters, placeholders, notes.
- Edit view, outline view and on-screen presentation.
- `.odp` format for reading and writing, then `.pptx`, proven with a real reference suite.
- **`Exchange`**: copy and paste between modules, which is the piece that makes real the
  cross-app object model promised from the start.
- Basic transitions and animations.

**Finished when**: a presentation made in Deck opens in other presentation programs without losses, and a
chart created in Ledger is pasted into a slide and stays editable.

---

## Phase 4 — Macros and scripting

- Engines: `JintScriptEngine` (JavaScript), `IronPythonScriptEngine`, `CPythonScriptEngine`
  via pythonnet. CPython discovery is already done and proven; execution is missing.
- **Sandbox** by default: no files, network or processes, time and
  memory caps. It is a decision already taken and must be followed to the letter.
- **Integrated IDE**: completion, errors, colouring that distinguishes keywords, classes
  and API elements, step-by-step execution. The scriptable API is already explicit metadata
  (`ScriptApiCatalog`), and completion and colours come from there.
- Macro recorder.

**Finished when**: a macro is recorded, reopened in the IDE, edited and
run again — in both languages.

---

## Phase 5 — Folio

**Why last**: it depends on rich-text layout, the riskiest subsystem of the
project. The goal is **completeness**, not a subset: 232 dialogs and 288 IDLs
counted in the catalogue.

- Rich-text model and pagination engine (the hard part).
- Paragraph and character styles, lists, tables, sections, columns.
- Headers and footers, notes, cross-references, table of contents, indexes.
- Track changes, comments, version comparison.
- Mail merge.
- `.odt` and `.docx` proven both ways.

---

## Phase 6 — Digital signature

Signing and verification for ODF (XML-DSig per OASIS) and for OOXML: certificate chain,
timestamp, trust indicator in the status bar, and the link with signed macros
already foreseen by the permission model.

**Why not earlier**: it signs what the formats can already write. Bringing it forward means
redoing it at every extension of the formats.

---

## Phase 7 — The other platforms, for real

**Cross-platform is a constraint, not a goal** — but so far it has been verified only on
macOS, and the defects found in this phase prove it: the inert title bar would have
blown up on Windows and Linux, where we draw the window buttons ourselves.

- Real testing on Windows and on Linux: window frame, menus, paths, fonts.
- Packaging and installation for the three systems, file type associations.
- Then preparation for Android and iOS, which was declared from the start.

---

## Ongoing, not a phase

- **The five languages**: every new text is born with its key in all five
  catalogues. The completeness test prevents forgetting it.
- **CLAUDE.md updated at every substantial change.** If the code and the file diverge,
  the file is a bug.
- **Commits and pushes only when the user asks** (rule 2 in `CLAUDE.md`).
- **Restructure instead of patching**, and no gratuitous static methods: composition through
  interfaces and dependency injection.
- **Tests on what is deterministic**: engine, formats, commands. The interface is tested
  by looking at it.
