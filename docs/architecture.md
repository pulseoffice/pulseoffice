# PulseOffice — Architecture

Design document. It does not describe what is already implemented (see `CLAUDE.md` for the status),
but the structure the code must converge towards.

## Principles

1. **Replicate the features, not the interface.** Established office suites are the specification of the *what*
   (the feature census drawn from them); the *how* is ours.
2. **The core does not know a UI exists.** No `Model`, `Engine` or `Formats` project
   references Avalonia. This is what makes mobile possible tomorrow and unit tests possible today.
3. **A single source of truth for every concept.** Commands are declared once, and menus,
   ribbon, palette, shortcuts and search derive from there. Settings are a schema from which
   the UI is generated. No hand-duplicated lists.
4. **Compact density as a design constraint**, not as a theme applied afterwards.
5. **No data loss in the round-trip.** What we cannot interpret, we preserve.

## Module names

The names *in code* are functional and never change: `Documents` (text), `Sheets` (spreadsheets),
`Presentations` (presentations). The *commercial* names live only in UI strings, so
rebranding does not touch a single line of code. Required accent colours: text **blue**,
spreadsheets **green**, presentations **red**.

## Solution structure

`PulseOffice.slnx` (new XML format). Solution folders = folders on disk.

### Foundations — no UI dependency

| Project | Responsibility |
|---|---|
| `PulseOffice.Foundation` | Typed units of measure (Twip, Emu, Point), geometry, colour, results, diagnostics, I/O and localisation abstractions |
| `PulseOffice.Document` | Model shared by the three modules: styles with inheritance, metadata, text runs, tables, fields, bookmarks, comments, revisions |
| `PulseOffice.Document.Commands` | Command stack, undo/redo, transactions, command merging, journal for crash recovery |
| `PulseOffice.Drawing` | Vector model: shapes, paths, fills, strokes, groups, layers, anchoring. Shared by the three modules |
| `PulseOffice.Text` | Typographic layout: shaping (HarfBuzz), line breaking, bidi, justification, pagination. **The hardest subsystem** |
| `PulseOffice.Charts` | Chart model and rendering, shared by the three modules |

### Application modules — logic

| Project | Responsibility |
|---|---|
| `PulseOffice.Sheets` | Model: sheets, sparse cells, ranges, defined names, conditional formatting |
| `PulseOffice.Sheets.Engine` | Tokenizer, parser, evaluator, dependency graph, topological recalculation, library of the 432 functions |
| `PulseOffice.Documents` | Text document model: nodes, sections, notes, indexes, anchors |
| `PulseOffice.Presentations` | Model: slides, masters, layouts, placeholders, transitions, animation timeline |

### External data

| Project | Responsibility |
|---|---|
| `PulseOffice.Data` | Abstractions: connection descriptor, provider, query, result, credential store |
| `PulseOffice.Data.Odbc` | Generic ODBC: covers any source with an installed driver |
| `.SqlServer` · `.PostgreSql` · `.MySql` · `.Sqlite` | Native ADO.NET drivers, one per project. Oracle is absent: its driver has a licence incompatible with the GPL, it is reached via ODBC |

**Credentials rule**: the connection string saved in the document contains no secrets.
The password lives in the operating system's credential store and is resolved at runtime. A
document forwarded by email must not carry access to the database with it.

**Binding to a sheet** (`QueryBinding` in `PulseOffice.Sheets`): a Ledger range is
bound to a query. The user selects any cell inside the range and triggers the
refresh — the behaviour of Excel's external data ranges. The command is
contextual: `QueryBindingCollection.FindAt(address)` decides whether to show it. When the
number of rows changes, rows are inserted or deleted, so adjacent formulas do not break.

### Formats

| Project | Responsibility |
|---|---|
| `PulseOffice.Formats` | Abstractions: `IDocumentReader`/`IDocumentWriter`, format registry, content-based detection, **fidelity report** |
| `PulseOffice.Formats.Odf` | **Default.** `.odt .ods .odp .odg` + flat variants. ZIP package, manifest, `content.xml`, `styles.xml`. Our own implementation |
| `PulseOffice.Formats.OpenXml` | `.docx .xlsx .pptx` via Open XML SDK 3.5.1 |
| `PulseOffice.Formats.Interchange` | CSV, TSV, TXT, RTF, HTML, Markdown |
| `PulseOffice.Formats.Pdf` | PDF export (and hybrid PDF with the ODF embedded) |

### Interface

| Project | Responsibility |
|---|---|
| `PulseOffice.UI` | Design tokens, density, light/dark/contrast themes, per-module accents, converters |
| `PulseOffice.UI.Controls` | Custom titlebar, ribbon, menu bar, status bar, modal host with backdrop, compact controls |
| `PulseOffice.UI.Commands` | Command registry, context system, shortcuts, command palette |
| `PulseOffice.Shell` | Application shell: workspace, document tabs, settings view, module orchestration |
| `PulseOffice.Sheets.UI` · `.Documents.UI` · `.Presentations.UI` | Views and view models of each module |
| `PulseOffice.Platform` | Platform abstractions: native menu, window behaviour, file dialogs, recent documents |
| `PulseOffice.Platform.MacOS` · `.Windows` · `.Linux` | Implementations |
| `PulseOffice.Desktop` | Executable. The only project that knows all three platforms |

Tomorrow: `PulseOffice.Android`, `PulseOffice.iOS`, `PulseOffice.Browser` plug in here without
touching anything above, **provided rule 2 is never violated**. The rule is verified by an
architecture test, not by good will.

### Tests

`tests/` mirrors `src/`. Top coverage priority: `Sheets.Engine`, `Formats.Odf`,
`Text`, `Document.Commands`. These four are deterministic and UI-free: there is no excuse.

---

## Cross-app components

A chart created in Ledger must be able to live in Folio and in Deck. This is achieved by taking
ownership of the object away from the modules:

- `PulseOffice.Document.Embedding` — `EmbeddedObject`: charts, images, shapes, tables,
  sheet portions, text frames, formulas. A single model, owned by no module.
  Three modes: snapshot, linked, embedded.
- `PulseOffice.Exchange` — the only exchange channel between modules and with other apps. An
  `ExchangePackage` carries the neutral content (`ObjectsContent`: `EmbeddedObject` with size;
  `CellRangeContent`: cells with value, formula, displayed text and essential formats), the live data of the
  source module (`Native`, only within the same process) and the representations: native (polymorphic JSON
  from `JsonExchangeSerializer`, derived types are discovered from `IExchangeTypeCatalog`), ODF and OOXML (ODS/XLSX
  of the portion, ODT/DOCX of Folio text), HTML, RTF, text, PNG. `IObjectExchange` publishes and
  recognises packages (the same Id on the clipboard returns the live package) and delegates reception to the
  module's `IExchangeParticipant` (`FolioExchange`, `DeckExchange`, `LedgerExchange`), which declares the
  possible forms (`ExchangePasteMode`: native, objects, cells, table, sheet object, formatted text,
  plain text, image). A sheet portion becomes, in Folio, a table or an ODS `PackagedObject`, in
  Deck a `DataTableObject` or `PackagedObject`; a chart carries its data inline and stays a `ChartDefinition`.
  On the interface side: `SystemClipboard`/`ExchangeDataTransfers` (UI.Controls) translate the representations
  into the platform formats of `IRichClipboardFormats`, `ExchangePasteChooser` is the shared paste special,
  `ExchangeDragDrop` (Shell) drags out with ⌃⌥ and receives drops on the window.
  HTML tables (round 121): Folio reads pasted or opened HTML through `HtmlDocumentReader`, which keeps nested
  tables as tables inside the cell and inline formatting run by run; `HtmlTableReader` (Ledger's cell paste and
  Deck's `HtmlTableGrids`) gives one grid, so a nested table, which a sheet range cannot hold, becomes text of the
  outer cell (rows on lines, cells separated by tabs), and a cell is bold, italic, underlined or coloured only when
  that formatting covers all of its text (a sheet cell has no runs). `HtmlCellBorders` reads the `border`
  shorthand, the box longhands (`border-width`/`-style`/`-color`, one to four values) and the side shorthands and
  longhands, with style (solid, dotted, dashed, double); the Folio HTML writer and Ledger's HTML fragment write
  per-edge `border-<side>` declarations, so borders cross the clipboard both ways.
- `PulseOffice.Sheets.Pivot` — pivot tables, whose source can be a static range
  or a range bound to a query.
- `PulseOffice.Charts` — catalogue of types: classic, distribution (histogram, Pareto, box
  and whisker, violin, density, quantile-quantile), process control, multivariate, finance.
  Plus trend lines, error bars and statistics computed on the chart.

### Insertable graphics: stickers, icons, clipart

Stickers, icons and clipart are the same thing — vector graphics with a provenance — hence
a single type, `VectorAsset` in `PulseOffice.Drawing.Assets`, with four possible origins: embedded
SVG, reference to an installed collection, glyph of an icon font, resolved paths.

**Icon fonts are converted to paths on insertion.** A document that depends on
a font installed on the machine of whoever opens it is a broken document: the glyph's origin
(family, code point, variant) is kept separately, so weight or style can be changed without
starting from scratch.

**The licence is model data, not a footnote.** `AssetLicense` distinguishes what
we may distribute with the application from what the user may install:

| Collection | Licence | Distributable |
|---|---|---|
| Lucide | ISC | yes, no obligations |
| Tabler · Phosphor | MIT | yes, no obligations |
| Material Symbols | Apache 2.0 | yes, no obligations |
| Twemoji | CC BY 4.0 | yes, with credit in the about information |
| Noto Emoji | OFL 1.1 | yes, with credit |
| OpenMoji | CC BY-SA 4.0 | **no**: user-installable only |

OpenMoji is excluded from the preinstalled set not for quality — it is excellent — but because the
share-alike clause would fall on the documents produced with PulseOffice. That choice must remain the user's.

### Macros and scripting

All three modules are scriptable in **JavaScript** and **Python**, with an integrated IDE.

| Project | Responsibility |
|---|---|
| `PulseOffice.Scripting` | Abstractions: `IScriptEngine`, API catalogue, permissions, diagnostics |
| `PulseOffice.Scripting.JavaScript` | Jint 4.16 — fully managed interpreter, no native dependency |
| `PulseOffice.Scripting.Python` | IronPython 3.4 — fully managed, no CPython to install |
| `PulseOffice.Scripting.Editor` | AvaloniaEdit 12 + TextMate: highlighting, completion, inline errors |

| `PulseOffice.Scripting.CPython` | pythonnet 3.1 — real CPython, optional, for numpy/pandas |

**Two Python engines, IronPython by default.** IronPython is fully managed and works
everywhere without installing anything; CPython is switched on when the scientific libraries are needed.

**Why pythonnet and not CSnakes.** CSnakes has the best provisioning system
(it downloads standalone interpreters, manages venv, pip and uv) but **cannot execute arbitrary
source**: it works by importing typed `.py` modules generated at compile time. For macros,
which are code written by the user at runtime, `exec`/`eval` is needed, and pythonnet has it.
Verified by reflection on the assemblies of both, not from the documentation.
Consequence: we write the provisioning ourselves.

#### CPython provisioning

Four modes, chosen by the user in the settings:

| Mode | What it does | For whom |
|---|---|---|
| **Standalone** (recommended) | Downloads a relocatable interpreter (python-build-standalone) into the data folder | Those who just want it to work |
| System installation | Uses a CPython already present | Those who already have their environment |
| Explicit folder | Path given by hand | Special and corporate cases |
| Conda | Existing conda environment | Scientific settings |

Discovery queries the interpreter with `sysconfig` instead of inferring from paths: it is
the only reliable way with virtual environments, where the executable is a link to
an installation that lives elsewhere. Points where every system is different:

- **Windows** — official installer (per user and per machine), Microsoft Store alias;
  the DLL is `pythonXY.dll` in the root or in `DLLs`, and `sysconfig` does not expose `LIBDIR`.
- **macOS** — python.org framework, Homebrew (different paths on Apple Silicon and Intel),
  Xcode tools Python. `LDLIBRARY` is a **relative path** inside the framework,
  so only the file name must be taken.
- **Linux** — `/usr/bin`, `/usr/local/bin`, multiarch paths such as
  `/usr/lib/x86_64-linux-gnu`. Distributions move the shared library, and some
  ship only the static one, which is unusable.

**The interpreter's architecture must match the process's.** An x64 CPython cannot
be loaded into an arm64 process: it is the most frequent cause of failure on macOS, where the two
architectures coexist.

**Packages go into a dedicated virtual environment**, never into the system Python: a
user's `pip install` must not modify the machine's installation. `uv` is preferred
to `pip` when available.

**Downloading is always an explicit user action**: it involves network access and executing
binaries, and must never happen silently at startup.

**The API is metadata, not reflection.** `ScriptApiCatalog` explicitly describes every exposed
symbol: name, kind, signature, documentation. Completion and the distinct colouring
of language keywords and API symbols come from there, **without** the need for an external language
server. A language server (Pyright, TypeScript) can still be attached when installed,
but the editor must be valid without one.

**Macros are the first attack vector of office suites.** Default: no access to
files, network or processes; only the hosting document; time and memory caps. Every
widening is an explicit user choice on that document, and goes through trusted
locations or signed macros.

### Logging and diagnostics

`PulseOffice.Diagnostics` — Serilog to file, daily **and** size-based rotation (32 MB),
14 files kept for 30 days, asynchronous writing so as not to slow down the interface.
Paths follow each system's conventions: `%LOCALAPPDATA%` on Windows,
`~/Library/Logs` on macOS, `$XDG_STATE_HOME` on Linux. Never next to the executable, which on all
three systems may be read-only.

It is initialised **before anything else** in `Program.Main`: a startup that fails must
leave a trace. Unhandled exceptions also produce a crash report in
a separate file.

## Cross-platform — a non-negotiable constraint

Windows, Linux and macOS have equal standing: no feature may exist on one only.
Operational consequences already built into the structure:

- Everything system-specific sits behind an interface in `PulseOffice.Platform`
  (`IWindowChrome`, `IClipboardService`, `ICredentialStore`, file dialogs, recent documents).
- No `#if WINDOWS` scattered in the code: the implementation is chosen at composition time.
- Database drivers are all managed, with no native dependencies to install by hand.
- The build must stay clean on all three: no Windows-only packages.

## Critical subsystems

### 1. Commands and context sensitivity

The requirement "menus and ribbon must be contextual" is solved in a single place: a command
is declared once, with its validity contexts.

```
CommandDefinition
  Id                "sheets.format.conditional"
  Title / Tooltip / Icon / DefaultGesture
  Surface           Primary (ribbon) | Secondary (menu) | Both | Palette
  Module            Sheets | Documents | Presentations | Shell
  RequiredContext   SelectionKind.CellRange
  CanExecute(ctx) / Execute(ctx)
```

The **active context** is an observable object: current module, selection kind
(cell, range, shape, image, table, text, slide, chart…), modification state,
read-only, presence of revisions. Menu, ribbon, context menu, palette and status bar
are *projections* of this same registry filtered by the context.

The split you asked for — common commands on the ribbon, complex ones in the menus — is the `Surface` field.
Contextual tabs (e.g. "Chart", "Table", "Drawing") appear when the context activates
them and disappear on their own. No visibility code scattered in the views.

### 2. Custom titlebar and system menu

It is the point with the most OS-specific pitfalls, so it is isolated behind
`IWindowChrome` in `PulseOffice.Platform`.

| | macOS | Windows | Linux |
|---|---|---|---|
| Menu | System bar, via `NativeMenu` | Inside the custom titlebar | Inside the custom titlebar (+ global menu if the DE exposes it) |
| Window buttons | Native traffic lights, with computed *inset* | Drawn by us | Drawn by us, **order and side read from the DE settings** |
| Dragging | Explicit drag area | Same | Same |
| Pitfall | Traffic light position in full screen | *Snap Layouts*: hit-testing on the maximise button is needed, otherwise the feature disappears | Fractional scaling, Wayland vs X11 |

Everything in the titlebar (menu, title, search, buttons) has variable width:
with a narrow window the menu collapses before overlapping the buttons.

### 3. Integrated modals

No new window. `IDialogService` returns a `Task<TResult>` and shows the content
in a layer above the window with a backdrop. Non-negotiable requirements: focus trap,
`Esc` to cancel, focus restored to the originating element, content scrolling
when the window is short, accessibility announcement. Long dialogs (Format Cells,
Paragraph) are tabbed panels, not giant modals.

### 4. Status bar

Segment contract, each one registered by its owner:

- **Transient**: outcome of the last operation ("14 cells replaced", "Saved as ODS")
- **Contextual**: Sheets → sum/average/count of the selection · Text → words, page *n* of *m*,
  language · Presentations → slide *n* of *m*
- **Permanent**: document format and whether it is in compatibility mode, save state,
  selection mode, zoom
- **Ongoing activity**: progress for recalculation, import, PDF export — with cancellation

### 5. Settings

There will be a great many, so they are not hand-written in the UI. A **schema** is declared
(key, type, default value, scope, constraints, help text, search keywords) and
the view is generated from it. Scopes: factory default → company policy → user → document.
What follows for free: search among the settings, per-item reset, profile import/export,
diagnostics. It is the same approach other office suites use for their configuration registry, and it is
why they manage to handle thousands of settings.

### 6. Format strategy

ODF is the default; OpenXML is chosen per document or as a global preference. Two rules:

- **Preservation of the unknown.** Elements and attributes we cannot map onto our model
  are preserved and rewritten. It is the difference between a converter and an editor.
- **Fidelity report.** When saving to a format that cannot hold a feature used in the
  document, the status bar says so — without a blocking modal, with details on request.

### 7. Rendering

Avalonia 12 renders with Skia. Grid, page and canvas share a scene layer of our own
(`PulseOffice.Drawing` + `PulseOffice.Text`) so as not to rewrite text drawing three times.
We do not reference SkiaSharp directly: Avalonia's is used, to avoid version conflicts.

---

## Development order

The catalogue's reasoning is confirmed: we start from Sheets, Text comes last.

| # | Milestone |
|---|---|
| 0 | **Skeleton**: solution, shell, titlebar, empty ribbon+menu, status bar, themes, modals |
| 1 | Calculation engine: parser, dependency graph, recalculation, ~40 functions. Zero UI |
| 2 | Sheets grid: editing, selection, number format, undo/redo |
| 3 | ODS read/write, then XLSX |
| 4 | The 432 functions by frequency of use, sorting, filters, conditional formatting |
| 5 | Shape canvas + Presentations: slides, masters, transitions |
| 6 | Rich text layout — the wall |
| 7 | Text |

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Rich text layout | **Critical** | Tackle it at step 6, with the suite already standing. Separate prototype first |
| ODF written from scratch | High | Start from ODS, the simplest of the three. Round-trip tests on real files |
| Custom titlebar on 3 OSes | Medium | Isolated behind `IWindowChrome` from step 0 |
| Spreading over too many modules | High | One module at a time until shippable |

## Review 2026-10

Architecture review requested by the user after the twenty-sixth round, over all of `src/` (2174 C# files, about
295,000 lines), with a census script (types by lines, static classes) and a survey of duplicates,
patterns and boundaries.

### What was found
- **Types too large**: 36 types over 600 lines, 19 over 800. The worst: `SheetGridView` 2684,
  `DeckView` 2254, `FunctionNameVocabulary` 1843 (data), `LedgerView` 1610, `ServiceConfiguration` 1317, `LedgerCommands`
  1274, `Paginator` 1264, `SlideView` 1245, `FolioCanvas` 1104, `DeckActions` 1085, `CalculationEngine` 1078.
- **Static classes**: 57. Almost all allowed (pure computations in `Numerics`, constants, registration extensions); outside
  the rule only the composition root and the menu and ribbon declarations, which stay static as declared data.
- **Duplicates**: unit constants redeclared file by file (11 `EmuPerTwip`, 9 `EmuPerPoint`, 5 `TwipsPerCentimeter`
  of which three with the truncated value 566.9291) although `Foundation.Units.Twip`/`Emu` existed; the gesture machine
  on objects (move, resize, rotate, handles) rewritten in `SheetGridView`, `SlideView` and `FolioCanvas`
  (150–250 lines); `DeckObjectCodecs` parallel in ODF and OOXML; hexadecimal colours converted in 11 projects; spelling
  commands, rulers, `*Exchange`, `*DocumentService`, `*NavigatorSource`, `*ZoomCommands` similar in the three modules.
- **Patterns**: no `if is` chain longer than six links; the longest type switches are in the DOCX readers
  (`DocxInlineReader` 18 branches), where a Visitor over OpenXML elements is the candidate. 218 `new` of registered services
  outside the composition, almost all convenience constructors (`: this(new …)`, `?? new …`); the real cases were in the
  composition itself and in `SheetConnectors`.
- **Boundaries**: no module project references another; no format references the interface. Two
  couplings remain: `PulseOffice.Formats` (abstract) depends on the models of the three modules, and `Sheets.UI` uses the concrete
  `OdsReader` in a command.
- **Effect guards** (decision 19): `LedgerProtectionGuard` took snapshots with `ISheetSnapshots.Capture`, which copies
  the whole sheet **and the formulas of all the other sheets**: an O(workbook cells) cost for every command on a
  protected sheet, even for save and open. `CommandExecutor` did not close the checks when the command failed or was cancelled,
  leaving partial changes unverified. Restoring goes through the history (undo back to the starting point):
  this is intended, because it keeps a single return mechanism and also works for macros and the agent when they go through
  `CommandExecutor`; scripts launched directly remain covered by `IScriptProtection` and `EditVerdict`. Deck
  has no guards (it has no protection).
- **Folio outside the editor**: 186 command calls to services with `editor.Document, editor.History` instead of
  to the editor; they record in the history but skip normalisation and selection. Document protection and
  turning on revision tracking do not go through the history (as in the reference word processor and in Word, by choice).

### What was fixed
- `SheetGridView` 2684 → 462 lines: viewport, per-layer painters, pointer and keyboard input, gestures as **State**
  (`GridGesture` and one class per gesture, `GridGestureFactory`), declared factory `SheetGridPartsFactory`.
- `DeckView` 2254 → 577 and `DeckActions` 1085 → 270: facade with action areas (slides, elements, text, design,
  slide show) and view collaborators (thumbnails, inspector, modes, masters, text, keyboard, menus).
- `LedgerView` 1610 → 560 and `LedgerCommands` 1274 → 24: six `LedgerCommandSet` command sets and seven view
  collaborators.
- `ServiceConfiguration` 1317 → 102: eighteen compositions by area, registration order unchanged;
  `SheetConnectors`, `DocxComposer`, `TabularSources` receive the registered services instead of creating them, `IMeasureUnits`
  registered once instead of four times.
- Units of measure: all copies replaced by `Twip`/`Emu` from `Foundation.Units` (twenty files).
- `LedgerProtectionGuard` snapshots only what it compares (cells, objects, merges and breaks of the protected sheet),
  no longer the other sheets or the layout; `CommandExecutor` closes the checks even after cancellation or error.

### Automatic rules added (`DesignRulesTests`)
- No type over **800 lines** without a reason declared in the exceptions table; an exception that is no longer needed
  makes the test fail, so the table does not go stale.
- Static classes allowed only if made of constants, extensions, private helpers or pure computations, or if they are registrations
  (`*ServiceCollectionExtensions`, `*Composition`, `*Registration`) or computations in `Numerics`; the others are listed with
  the reason.
- No unit constant (`EmuPer…`, `TwipsPer…`) redeclared outside `Foundation`.

### What remains
- Types still over the cap, with a reason in the table: `Paginator`, `SlideView`, `FolioCanvas`, `CalculationEngine`,
  `RtfReadSession`, `FolioEditor` (plus two data tables). `OdtBodyReading`, `OdtBodyWriting` and `DocxDrawingCodec` exceed the cap
  as files but are already split into several types.
- A shared object gesture machine in `PulseOffice.UI.Controls` for Ledger, Deck and Folio (extracted in
  Ledger as State, to be generalised): it would remove `SlideView` and `FolioCanvas` from the table.
  Not yet done at the twenty-eighth round; `DocxInlineReader` has instead moved to per-element strategies.
- Parallel components of the three modules to unify: spelling, rulers, zoom, navigator and macro sources.
- Hexadecimal colour conversion and dash and border tables into a single catalogue shared by ODF and OOXML.
- Folio commands that call services with `editor.Document, editor.History`: move them to editor methods.
- Convenience constructors with `new` in the formats (`DocxWriter`, `OdtReader`/`OdtWriter`, `PdfExporter`) and in the views.
- Abstract `PulseOffice.Formats` depending on the module models; concrete `OdsReader` in `LedgerSheetExtraCommands`.
- The command sweep (`PulseOffice.Desktop.Sweep.Tests`) takes about half an hour: it should be split per module into parallel
  runs or made selectable, not shortened by removing contexts.

## Reuse across modules (2026-10)

Census requested by the user ("is there still code reusable by Folio, Ledger and Deck?"), over all of `src/`, with
tools (parallel `Folio*`/`Ledger*`/`Deck*` names, method signatures, hexadecimal colour conversions, gesture
machines) and reading. **129 type names** appear with the same suffix in at least two modules; most of them are
legitimate adapters (Strategy over the module's own model: `*FontCollector`, `*DocumentFactory`, `*CommandSet`,
`*View`), which already reuse a shared service. The entries below are those where the **logic** is repeated.

| Item | Files | Duplicated | Proposal | Priority | Status |
|---|---|---|---|---|---|
| Object gestures (move, resize, rotate, "has changed" threshold) | `ObjectDragGesture` (Ledger), `SlideView.TransformGesture` (Deck), `FolioCanvas.ObjectGesture` | preview state, rotation, comparison with the start, three times (~60 lines each) | `ObjectTransformSession` in the core (`PulseOffice.Drawing.Shapes`), created by `IObjectManipulator.Begin` | high | **done** |
| Hexadecimal colour conversion | 26 formatters and 10 readers in 30 files (ODF, OOXML, HTML, three modules, controls) | `"#" + (argb & 0xFFFFFF).ToString("X6")` and `uint.TryParse(…, HexNumber)` repeated | `ArgbColor` in `PulseOffice.Foundation.Colors` | high | **done** |
| Spell-check loop | `LedgerSpellingCommands`, `DeckSpellingCommands`, `FolioSpellingCommands` | ignored words, "replace all", "learn", translated from `SpellingDecision` three times | `SpellingRound` in `UI.Controls.Spelling` | high | **done** |
| Dictionary language choice | Ledger, Deck (Folio additionally has "no check") | available dictionaries, warning if missing, current index, prompt | `ISpellingLanguagePicker`, with one extra entry at the end | medium | **done** (Ledger, Deck, Folio) |
| Painting the selection frame (frame, handles, rotation handle, rotation around the centre) | `GridObjectPainter`, `SlideView.DrawFrame`, `FolioCanvas` | same geometry, different styles (see "Selection frame: the variants") | `IObjectFrameChrome` in `UI.Controls.Shapes` with the style as data | medium | **done** (user decision: Ledger solid, Deck and Folio dashed) |
| Snapping during the gesture | Deck (`Snapping`, lines from grid, guides, margins, objects), Folio (grid only), Ledger (none) | two different implementations | `IObjectSnapping` and `SnapLines` in the core | medium | **done** (Ledger: Alt snaps to cell borders, as in Excel) |
| Zoom | `PageZoom` (Folio, maximum 5) and `CanvasZoom` (Deck, maximum 4), `FolioZoomCommands` and `DeckZoomCommands` (Ledger presets only) | twin type and twin commands (in, out, fit, width, custom) | `ViewZoom`, `ZoomLimits`, `ZoomHistory` in `Foundation.Zoom`; `IZoomChoice` in `UI.Controls.Zoom` | medium | **done** (Folio, Deck); Ledger stays on its factor zoom |
| Arrow keys on objects | `DeckStageKeys` (Deck), `FolioView` (Folio, with Shift), none in Ledger | direction → stepped move, written twice with different rules | `IObjectNudgeKeys` in `UI.Controls.Shapes`, steps as per-module data | low | **done** (new in Ledger) |
| Navigator and macro sources | `*NavigatorSource` (355 lines in all), `*MacroEventsSource` (222) | parallel structure over different models | leave: they are adapters, the panel is already shared | low | not duplication |
| Rulers | `FolioRulers`, `DeckRulers` (343 lines) | indent and tab markers, units and tab operations computed twice | `IParagraphRuler` in `UI.Controls.Rulers` (ticks and strip were already shared) | low | **done** |
| ODF/OOXML dashes and borders | `Formats.Odf/ShapeStyles`, `Formats.OpenXml/ShapeStyles/Vocabulary`, `DocxBorders`, `OdtProperties` | the model is already one (`LineDash`, `BorderEdge`); each format has its own vocabulary | leave: it is each format's own translation | — | not duplication |
| Two `ILanguageChoices` | `UI.Controls.Text` (character languages) and `UI.Controls.Spelling` (dictionary names) | same name, different purposes | rename the spelling one `IDictionaryLanguageNames` | low | **done** (the record too: `DictionaryLanguage`) |

### What was unified
- `ObjectTransformSession` (core, without Avalonia): handle, start, preview, rotation, `Track`, `Settle` (for
  snapping), `Moved`/`Rotated` with the module's threshold. Ledger, Deck and Folio keep only what is their own (multiple
  selection and groups in Deck, target page in Folio, cell-anchored changes in Ledger).
- `ArgbColor` (`Foundation.Colors`): `FromRgbHex`, `FromHashedRgbHex` (ODF form), `ToHashedHex`, `ToRgbDigits`, lowercase
  forms for ODF. It replaces the copies in ODF, OOXML, HTML, Folio, Deck, Ledger and in the controls.
- `SpellingRound` and `SpellingLanguagePicker` (`UI.Controls.Spelling`), wired into the three modules.
- `ViewZoom`, `ZoomLimits` (`Slides` 10–400 %, `Pages` 10–500 %) and `ZoomHistory` in `Foundation.Zoom` in place of
  `CanvasZoom` and `PageZoom`; `IZoomChoice` in `UI.Controls.Zoom` (step forward and back on the scale, zoom dialog)
  in place of `FolioZoomChoice` and `DeckZoomChoice`. Each target exposes a `ZoomSubject` with its own limits. A non-finite
  factor becomes 100 % in Folio too (previously `Math.Clamp` let `NaN` through).
- `IParagraphRuler` in `UI.Controls.Rulers`: first-line, indent and tab markers, metric or imperial unit,
  move, add and remove tab (always sorted: previously Deck did not re-sort). `FolioRulers` and `DeckRulers` keep
  only page margins (Folio), text box inner margins (Deck) and application to their own model.
- `IObjectNudgeKeys` in `UI.Controls.Shapes`: arrow = small step, Shift+arrow = large step, the other modifiers
  do not move. Per-module steps: Deck 3/15 pt, Folio 1/10 pt, Ledger 1/10 px. Ledger had no arrow keys on objects:
  now it has them, also on several selected objects, each as a history step.
- `IObjectSnapping` in the core (`Drawing.Shapes`, formerly `Presentations.Snapping`): `Snap(session, lines, tolerance)` snaps
  the move (edge, centre or opposite side) or the dragged sides of a resize, never the rotation; `Lattice`
  builds the lines of a grid. Deck uses it with grid, guides and margins; Folio with the page grid (it now
  also snaps resizing, not just moving); Ledger with the borders of visible cells, holding Alt
  while dragging as in Excel.
- `IDictionaryLanguageNames` and `DictionaryLanguage` in place of the second `ILanguageChoices`/`LanguageChoice`; the language
  picker accepts one extra entry at the end (in Folio "no check") and Folio uses it.

### Selection frame: one component, two styles (user decision, 2026-10-29)
`IObjectFrameChrome` (`UI.Controls.Shapes`, `ObjectFrameChrome`) paints the frame, eight handles, rotation handle with
stem and secondary frames of a multiple selection, rotating around the centre, and answers hit tests (`HandleAt`); the
geometry and hit zones come from `IObjectManipulator`, which now accepts a unit (`unit`, model points per screen
point): Folio, which draws in page points, passes `1 / Scale`, so handles and stem have the same on-screen size
in the three modules and the hit area matches the drawing (previously in Folio the hit area scaled with the zoom and the drawing did not). The
style is data, `FrameStyle` (outline, handle fill, rotation marker, thicknesses, margin, radius), and
`IFrameStyles` offers two of them; each module picks its own with one line. The yellow adjustment handles remain
`IAdjustHandleChrome`, already shared.

| Module | Style |
|---|---|
| Ledger | **A, solid**: continuous frame in the module colour, filled square handles, filled square rotation handle with a stem |
| Deck | **B, dashed**: dashed frame in the accent colour, hollow square handles, hollow circle rotation handle with a stem |
| Folio | **B, dashed**, like Deck (before: thin continuous frame, filled dot without a stem) |

In both themes the frame keeps the same shapes; in dark the hollow handles are filled with the dark surface colour and
the outlines use the accent or module colour, so they stay legible on dark pages and cells.

### What remains
- Types still over the cap, with a reason in the table: `Paginator`, `SlideView`, `FolioCanvas`, `CalculationEngine`,
  `RtfReadSession`, `FolioEditor` (plus two data tables). `OdtBodyReading`, `OdtBodyWriting` and `DocxDrawingCodec` exceed the cap
  as files but are already split into several types.
- A shared object gesture machine in `PulseOffice.UI.Controls` for Ledger, Deck and Folio (extracted in
  Ledger as State, to be generalised): it would remove `SlideView` and `FolioCanvas` from the table.
  Not yet done at the twenty-eighth round; `DocxInlineReader` has instead moved to per-element strategies.
- Parallel components of the three modules to unify: spelling, rulers, zoom, navigator and macro sources.
- Hexadecimal colour conversion and dash and border tables into a single catalogue shared by ODF and OOXML.
- Folio commands that call services with `editor.Document, editor.History`: move them to editor methods.
- Convenience constructors with `new` in the formats (`DocxWriter`, `OdtReader`/`OdtWriter`, `PdfExporter`) and in the views.
- Abstract `PulseOffice.Formats` depending on the module models; concrete `OdsReader` in `LedgerSheetExtraCommands`.
- The command sweep (`PulseOffice.Desktop.Sweep.Tests`) takes about half an hour: it should be split per module into parallel
  runs or made selectable, not shortened by removing contexts.

## Reuse across modules (2026-10)

Census requested by the user ("is there still code reusable by Folio, Ledger and Deck?"), over all of `src/`, with
tools (parallel `Folio*`/`Ledger*`/`Deck*` names, method signatures, hexadecimal colour conversions, gesture
machines) and reading. **129 type names** appear with the same suffix in at least two modules; most of them are
legitimate adapters (Strategy over the module's own model: `*FontCollector`, `*DocumentFactory`, `*CommandSet`,
`*View`), which already reuse a shared service. The entries below are those where the **logic** is repeated.

| Item | Files | Duplicated | Proposal | Priority | Status |
|---|---|---|---|---|---|
| Object gestures (move, resize, rotate, "has changed" threshold) | `ObjectDragGesture` (Ledger), `SlideView.TransformGesture` (Deck), `FolioCanvas.ObjectGesture` | preview state, rotation, comparison with the start, three times (~60 lines each) | `ObjectTransformSession` in the core (`PulseOffice.Drawing.Shapes`), created by `IObjectManipulator.Begin` | high | **done** |
| Hexadecimal colour conversion | 26 formatters and 10 readers in 30 files (ODF, OOXML, HTML, three modules, controls) | `"#" + (argb & 0xFFFFFF).ToString("X6")` and `uint.TryParse(…, HexNumber)` repeated | `ArgbColor` in `PulseOffice.Foundation.Colors` | high | **done** |
| Spell-check loop | `LedgerSpellingCommands`, `DeckSpellingCommands`, `FolioSpellingCommands` | ignored words, "replace all", "learn", translated from `SpellingDecision` three times | `SpellingRound` in `UI.Controls.Spelling` | high | **done** |
| Dictionary language choice | Ledger, Deck (Folio additionally has "no check") | available dictionaries, warning if missing, current index, prompt | `ISpellingLanguagePicker`, with one extra entry at the end | medium | **done** (Ledger, Deck, Folio) |
| Painting the selection frame (frame, handles, rotation handle, rotation around the centre) | `GridObjectPainter`, `SlideView.DrawFrame`, `FolioCanvas` | same geometry, different styles (see "Selection frame: the variants") | `IObjectFrameChrome` in `UI.Controls.Shapes` with the style as data | medium | **done** (user decision: Ledger solid, Deck and Folio dashed) |
| Snapping during the gesture | Deck (`Snapping`, lines from grid, guides, margins, objects), Folio (grid only), Ledger (none) | two different implementations | `IObjectSnapping` and `SnapLines` in the core | medium | **done** (Ledger: Alt snaps to cell borders, as in Excel) |
| Zoom | `PageZoom` (Folio, maximum 5) and `CanvasZoom` (Deck, maximum 4), `FolioZoomCommands` and `DeckZoomCommands` (Ledger presets only) | twin type and twin commands (in, out, fit, width, custom) | `ViewZoom`, `ZoomLimits`, `ZoomHistory` in `Foundation.Zoom`; `IZoomChoice` in `UI.Controls.Zoom` | medium | **done** (Folio, Deck); Ledger stays on its factor zoom |
| Arrow keys on objects | `DeckStageKeys` (Deck), `FolioView` (Folio, with Shift), none in Ledger | direction → stepped move, written twice with different rules | `IObjectNudgeKeys` in `UI.Controls.Shapes`, steps as per-module data | low | **done** (new in Ledger) |
| Navigator and macro sources | `*NavigatorSource` (355 lines in all), `*MacroEventsSource` (222) | parallel structure over different models | leave: they are adapters, the panel is already shared | low | not duplication |
| Rulers | `FolioRulers`, `DeckRulers` (343 lines) | indent and tab markers, units and tab operations computed twice | `IParagraphRuler` in `UI.Controls.Rulers` (ticks and strip were already shared) | low | **done** |
| ODF/OOXML dashes and borders | `Formats.Odf/ShapeStyles`, `Formats.OpenXml/ShapeStyles/Vocabulary`, `DocxBorders`, `OdtProperties` | the model is already one (`LineDash`, `BorderEdge`); each format has its own vocabulary | leave: it is each format's own translation | — | not duplication |
| Two `ILanguageChoices` | `UI.Controls.Text` (character languages) and `UI.Controls.Spelling` (dictionary names) | same name, different purposes | rename the spelling one `IDictionaryLanguageNames` | low | **done** (the record too: `DictionaryLanguage`) |

### What was unified
- `ObjectTransformSession` (core, without Avalonia): handle, start, preview, rotation, `Track`, `Settle` (for
  snapping), `Moved`/`Rotated` with the module's threshold. Ledger, Deck and Folio keep only what is their own (multiple
  selection and groups in Deck, target page in Folio, cell-anchored changes in Ledger).
- `ArgbColor` (`Foundation.Colors`): `FromRgbHex`, `FromHashedRgbHex` (ODF form), `ToHashedHex`, `ToRgbDigits`, lowercase
  forms for ODF. It replaces the copies in ODF, OOXML, HTML, Folio, Deck, Ledger and in the controls.
- `SpellingRound` and `SpellingLanguagePicker` (`UI.Controls.Spelling`), wired into the three modules.
- `ViewZoom`, `ZoomLimits` (`Slides` 10–400 %, `Pages` 10–500 %) and `ZoomHistory` in `Foundation.Zoom` in place of
  `CanvasZoom` and `PageZoom`; `IZoomChoice` in `UI.Controls.Zoom` (step forward and back on the scale, zoom dialog)
  in place of `FolioZoomChoice` and `DeckZoomChoice`. Each target exposes a `ZoomSubject` with its own limits. A non-finite
  factor becomes 100 % in Folio too (previously `Math.Clamp` let `NaN` through).
- `IParagraphRuler` in `UI.Controls.Rulers`: first-line, indent and tab markers, metric or imperial unit,
  move, add and remove tab (always sorted: previously Deck did not re-sort). `FolioRulers` and `DeckRulers` keep
  only page margins (Folio), text box inner margins (Deck) and application to their own model.
- `IObjectNudgeKeys` in `UI.Controls.Shapes`: arrow = small step, Shift+arrow = large step, the other modifiers
  do not move. Per-module steps: Deck 3/15 pt, Folio 1/10 pt, Ledger 1/10 px. Ledger had no arrow keys on objects:
  now it has them, also on several selected objects, each as a history step.
- `IObjectSnapping` in the core (`Drawing.Shapes`, formerly `Presentations.Snapping`): `Snap(session, lines, tolerance)` snaps
  the move (edge, centre or opposite side) or the dragged sides of a resize, never the rotation; `Lattice`
  builds the lines of a grid. Deck uses it with grid, guides and margins; Folio with the page grid (it now
  also snaps resizing, not just moving); Ledger with the borders of visible cells, holding Alt
  while dragging as in Excel.
- `IDictionaryLanguageNames` and `DictionaryLanguage` in place of the second `ILanguageChoices`/`LanguageChoice`; the language
  picker accepts one extra entry at the end (in Folio "no check") and Folio uses it.

### Selection frame: the variants (user decision)
The geometry is the same in the three modules (eight handles, rotation handle above the top side, rotation around the
centre), the style is not. It was not unified because any choice changes the look of at least two modules.

| Module | Frame | Handles | Rotation |
|---|---|---|---|
| Ledger | continuous, module green | filled squares | filled square with stem |
| Deck | dashed, accent | hollow squares (white) | hollow circle with stem; yellow adjustment handles |
| Folio | thin continuous, accent | hollow squares (white) | filled dot without stem |

The two variants to choose from: **A, solid** (Ledger's: continuous frame, filled handles, more legible on the
grid sheet) and **B, dashed** (Deck's, the convention of presentation programs such as PowerPoint, hollow handles). Folio sits
in between and will follow the choice. With `IObjectFrameChrome` the style becomes data (`FrameStyle`), and the choice is one line per
module.

### What remains
- Ledger does not use `IZoomChoice`: its zoom is a grid factor with five presets and the shared zoom
  dialog; "fit to page" makes no sense on a sheet without pages.
- Navigator and macro sources and format vocabularies remain adapters by choice (see table).

### Ledger filter values (round 120)
- `IFilterColumnDates` (`PulseOffice.Sheets`) is the single place that decides which filter values of a column are
  dates (numeric cell with a date-time number format). The filter menu, the XLSX codec (`dateGroupItem`) and the ODS
  mapper (ISO 8601 `filter-set-item`, as the reference suite writes) all use it, so the in-memory allowed set stays
  canonical while each file carries dates the way other applications compare them.
- `IFilterRowKey` (`PulseOffice.Sheets.Engine`) builds the duplicate key for "unique rows only" in both the range
  filter and the advanced filter: kind-tagged, so number 1 and text "1" differ.
