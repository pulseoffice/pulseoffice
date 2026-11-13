# PulseOffice

Cross-platform office suite in .NET 10 + Avalonia. Three modules: word processing, spreadsheets,
presentations. The established office suites serve as the **functional specification** of what to build,
never as a code base.

> This file is the project's living record. It must be updated **on every substantial change**:
> new projects, architectural decisions, verified APIs, package versions, milestone status.
> If the code and this file diverge, the file is a bug.

## Documents

| File | Contents |
|---|---|
| [docs/worklog.md](docs/worklog.md) | **Work log: history of every block, decisions, defects, verifications** (new entries go here) |
| [docs/plan.md](docs/plan.md) | **Development plan: phases, order and completion criteria** |
| [docs/architecture.md](docs/architecture.md) | Solution structure, critical subsystems, risks |
| [docs/ledger-functions.md](docs/ledger-functions.md) | The 432 spreadsheet functions |
| [docs/completion-plan.md](docs/completion-plan.md) | **The plan to have them all: twenty-two blocks in order** |
| [docs/deck-plan.md](docs/deck-plan.md) | **Deck's plan towards parity: eight blocks in order** |
| [docs/audit-plan.md](docs/audit-plan.md) | **Audit cycles over the whole app: areas, method, status** |
| [docs/folio-plan.md](docs/folio-plan.md) | **Folio's plan towards parity with the reference word processors and Word: thirteen blocks, risk first** |
| [docs/ux-plan.md](docs/ux-plan.md) | **UX plan: the 298 surveyed surfaces, the shared components to do first and seven parallel blocks** |
| [docs/agent-plan.md](docs/agent-plan.md) | **PulseOffice controllable by an AI agent: MCP server done; how to connect Claude Code and Claude Desktop** |
| [docs/design-patterns.md](docs/design-patterns.md) | Design patterns used across the code base |
| [docs/refactoring-plan.md](docs/refactoring-plan.md) | Refactoring plan |
| [README.md](README.md) | **Public description of the app and of `pulse-cmd.py`: keep it current and short** |
| [pulseoffice.github.io](https://github.com/pulseoffice/pulseoffice.github.io) | **Website** (separate repository, published at https://pulseoffice.github.io/ by GitHub Actions); this repository is `pulseoffice/pulseoffice` since 2026-11-07 |
| [pulse-cmd.py](pulse-cmd.py) | Version and packages: notarized dmgs, Windows installer, Linux AppImage, update signing key, signed checksums, GitHub release |
| [tools/mcp-client](tools/mcp-client) | Test MCP client and stdio↔HTTP bridge for Claude Desktop (`pulse-mcp`) |
| [tools/corpus-audit](tools/corpus-audit) | Automatic ODF/OOXML round trip over the external test corpus: versioned list, results and summary |
| [tools/verify-signatures](tools/verify-signatures) | Headless verification, with the reference suite, of digital signatures in ODF and OOXML files |

Reference material (outside the repo, not versioned), used as a test corpus:
the external reference clone, a sparse clone of the reference suite kept next to the repository folder (77 MB).
Update: `git pull --depth 1`.

## Commands

```bash
dotnet build PulseOffice.slnx          # builds everything
dotnet test PulseOffice.slnx           # runs the tests
dotnet run --project src/PulseOffice.Desktop   # starts the app
```

## Verified environment

| | Version | Notes |
|---|---|---|
| .NET SDK | 10.0.400 | already installed |
| Avalonia | 12.1.2 | stable; 12 requires .NET 10 |
| DocumentFormat.OpenXml | 3.5.1 | for docx/xlsx/pptx |
| CommunityToolkit.Mvvm | 8.4.2 | |
| Solution format | `.slnx` | XML, natively supported by the SDK |
| SkiaSharp / Svg.Skia | 3.119.4 / 5.1.1 | only in `PulseOffice.Formats.Rasterizing`, same SkiaSharp as Avalonia |
| HarfBuzzSharp | 8.3.1.3 | only in `PulseOffice.Typography.Shaping` (complex-script shaping for screen and PDF), same version as Avalonia's |
| AvaloniaUI.DiagnosticsSupport | 2.2.3 | Debug builds only |
| pythonnet | 3.1.0 | tested with Python 3.14 (Homebrew) and standalone 3.13; starts only 3.10–3.14 |

Central package management in `Directory.Packages.props`: **versions are never written in the
.csproj files**. Common settings in `Directory.Build.props` (net10.0, nullable, warnings as errors).

## Decisions taken

0. **The suite is named PulseOffice** (since 2026-10-07; formerly QuadOffice). Renamed projects,
   folders, namespaces, solution (`PulseOffice.slnx`), the `Pulse` prefix of theme tokens and
   maps (`PulseAccent`, `PulseTheme`, `PulseRibbonLayout`…), the URNs of our elements in files
   (`urn:pulseoffice:…`, `pulseoffice.*` types) and the configuration and log folders
   (`PulseOffice`). There is no migration: files written with the old URNs lose our extensions,
   and preferences, recents and keychain entries start from scratch. The repository and its folder are
   `pulseoffice` too (since 2026-11-04; formerly `quadoffice`).

1. **.NET 10 + Avalonia**, after evaluating Flutter, Java/JavaFX and Kotlin/Compose.
   Decisive: Microsoft's official Open XML SDK, runtime performance, Avalonia covers
   desktop + mobile + browser.
2. **ODF is the default format**, OpenXML optional per document or by preference —
   but the two are kept **aligned step by step**: see rule 7 among those set by the user.
3. **We write the ODF ourselves.** No usable open .NET library exists: Spire, GemBox and
   Independentsoft are all commercial. `PulseOffice.Formats.Odf` is therefore a first-class
   subproject, not a wrapper.
4. **Chosen product names**: **Folio** (documents, blue `#2563EB`), **Ledger** (sheets, green
   `#16A34A`), **Deck** (presentations, red `#DC2626`). Code names stay functional
   (`Documents`, `Sheets`, `Presentations`); the brand lives only in `ModuleIdentity`.
5. **The core does not reference Avalonia.** Constraint verified by `PulseOffice.Architecture.Tests`:
   `CoreDependencyTests` on loaded assemblies, `ProjectGraphTests` on the .csproj graph (all
   Foundation, Document*, Drawing*, Text*, Charts, Sheets*, Documents*, Presentations*,
   Formats* projects except the .UI ones, transitive closure: no Avalonia package, no UI or Shell project).
6. **Inverted development order**: sheets → presentations → documents. The word processor depends on
   rich text layout, the riskiest subsystem: it must be tackled with the suite already standing.
7. **Integrated modals with backdrop**, never new windows.
8. **Mixed interface**: common commands on the ribbon, complex commands in menus. Both are
   contextual projections of a single command registry.
9. **External data**: generic ODBC plus four native drivers (SQL Server, PostgreSQL, MySQL, SQLite), one per
   project; Oracle only via ODBC, because the Oracle driver has a GPL-incompatible license (2026-11-02). Credentials
   never enter the document: they live in the operating system's store.
10. **Cross-app objects**: charts, images, shapes, tables and sheet ranges are
    `EmbeddedObject`, belong to no module, and are exchanged **only** via `PulseOffice.Exchange`
    (since 2026-10-16): a neutral `ExchangePackage` (objects or sheet range + native JSON, ODF, OOXML,
    HTML, RTF, text, PNG representations) goes through `IObjectExchange`; each module is an
    `IExchangeParticipant` (Strategy) that captures and receives according to `ExchangePasteMode`. Copy, cut,
    paste, paste special, macros and drag and drop (⌃⌥ + drag, drop on the window) use this
    channel; platform format names come from `IRichClipboardFormats`.
11. **Unified vector graphics**: stickers, icons and clipart are a single type (`VectorAsset`);
    icon fonts are converted to paths on insertion, so the document does not
    depend on installed fonts. Each collection's license is model data:
    we preinstall only non-share-alike licenses (Lucide ISC, Tabler/Phosphor MIT, Material
    Symbols Apache 2.0, Twemoji CC BY 4.0 with credit). OpenMoji is CC BY-SA: installable
    by the user, never preinstalled.
12. **Scripting in JavaScript and Python** with an integrated IDE. Jint (JS) and IronPython (Python)
    as defaults, fully managed, zero native binaries. **CPython via pythonnet** as an
    optional second Python engine, for numpy and pandas. CSnakes rejected despite its
    excellent provisioning: it cannot run arbitrary source, it is meant for importing typed
    modules generated at build time. Verified by reflection on the assemblies.
    The scriptable API is **explicit metadata** (`ScriptApiCatalog`), not reflection: completion
    and symbol colouring come from it without external language servers.
13. **Macros sandboxed by default**: no files, network or processes; only the
    host document; time and memory caps. Extensions only by explicit user choice.
    **File writing for a macro** (2026-11-03): granted only from the macro editor, after a modal with backdrop, to the
    single macro identified by the SHA-256 fingerprint of language + code (if the code changes, it lapses); the grant lives
    in local settings (`macro-file-permissions.json`), never in the document, and the MCP agent cannot grant it. It gives
    only `MacroFolderWrite`: text files in `Documents/PulseOffice/Macros` through the `files` root, no paths outside, links
    or executable extensions.
14. **Rotating file logging** active from the start, initialized before everything else
    in `Program.Main`.
15. **Cross-platform is a constraint, not a goal**: no feature may exist on a
    single system; no `#if`, everything behind interfaces in `PulseOffice.Platform`. Since round 86 (2026-11-06) the
    only `OperatingSystem.Is*`, `RuntimeInformation.IsOSPlatform` and `Environment.OSVersion` calls live in the
    `PulseOffice.Platform*` projects: everything else asks `IHostSystem` (kind, path case rules, executable names,
    version; `FixedHostSystem` for tests) and writes private files through `IPrivateFiles`. Guarded by
    `SourceRulesTests.Operating_system_checks_live_only_in_the_platform_projects`.

17. **Formula separators by language** (user decision, 2026-10-17): with the interface in
    English (or in a language without its own vocabulary) formulas follow English Excel — arguments separated by
    commas, array constants `{1,2;3,4}` (comma between columns, semicolon between rows), decimal point. Italian,
    German, Spanish and French keep the semicolon, `|` between array rows and the decimal comma. It applies to
    entry, cell, formula bar, function wizard and macros (`FormulaLocal`,
    `SetFormulaLocal`); the canonical form in files and in the model (`;`, `|`, point) does not change. The table is
    `FormulaPunctuationConvention`. Since round 53 (2026-11-06) the spreadsheet locale (`ISpreadsheetLocale`: cell input,
    cell display, VALUE/NUMBERVALUE/DOLLAR/TEXT, conditional-format operands, pasted text) follows the interface
    language too (`InterfaceSpreadsheetLocale`; the system region is kept when its language matches).

18. **AI agent via MCP** (2026-10-24): MCP server built into `PulseOffice.Agent` (our own protocol, no SDK;
    local HTTP on 127.0.0.1 with an access code in a 0600 file, never in the keychain) and `PulseOffice.Agent.UI`
    (adapters). Content tools are **generated** from `ScriptApiCatalog` and run the same API as macros
    in the same sandbox; every change is an «Agente: …» step of the history; off by default, levels
    read / edit / files, modal confirmation for what ⌘Z cannot undo. Details in
    [docs/agent-plan.md](docs/agent-plan.md).

19. **Command effect guards** (2026-10-25): `CommandExecutor` opens an `ICommandEffectCheck` for each
    registered `ICommandEffectGuard` before running a command and closes it afterwards; a guard that sees a forbidden effect
    (locked cells, objects, merges of a protected sheet: `LedgerProtectionGuard`; paragraphs of a protected section:
    `FolioProtectedContentGuard`) undoes back to the history's starting point and says so in the status bar. It is the net
    that also holds commands written without checking protection; checks at the source remain where needed
    (permissions to insert rows and columns, filter). The sweep of all commands in several contexts is in
    `tests/PulseOffice.Desktop.Sweep.Tests` (50-minute watchdog with `TestProcessLifetimeMinutes`).

20. **GPL-3.0-only license and packages** (2026-11-02): text in `LICENSE`, `PackageLicenseExpression` in
    `Directory.Build.props`, which also holds `<Version>`. `pulse-cmd.py` (all in English) bumps the version and produces
    signed and notarized dmgs (arm64 and x64; certificate chosen from the keychain, notarytool credentials only in the
    keychain), NSIS installers in the five languages with a license page, Linux AppImages (x86_64 and aarch64, built in
    Docker with `appimagetool`; replaced the Flatpak bundle on 2026-11-08 because its sandbox hides `lp`, `ffmpeg` and
    `secret-tool` and cannot update itself), and publishes a GitHub release with the packages (`release`, via `gh`,
    confirmation before publishing). Icon in `packaging/` (`icon.svg` source, `icon.png`, `icon.ico`). The same packages are built by GitHub Actions
    (`.github/workflows/packages.yml`, 2026-11-13): a `v*` tag yields a **draft** release; credentials come from the
    `release` environment's secrets, and the dmg is laid out by `dmgbuild` rather than by scripting Finder. `bump <part> --push`
    commits every pending change with the new version, tags it and pushes both, which starts that workflow.

21. **Protection leaves the undo history** (user decision, 2026-11-02; done 2026-11-03): protection of sheet, structure,
    changes and document, protection of fields and bookmarks, and change recording are not ⌘Z steps, and no undo or redo
    of other steps touches them (snapshots do not contain them). They go through `UndoStack.ApplyOutsideHistory`, which
    marks the document modified (`OpenDocument` listens to `ChangedOutsideHistory`); the command declares itself with
    `CommandDefinition.OutsideHistory`, and the agent treats it as irreversible (confirmation). Protection of Folio table
    cells and sections remains undoable content, as in other word processors.

22. **Signatures without a trusted timestamp are judged at today's date** (2026-11-03): the date declared in the signature
    does not count; with a verified RFC 3161 timestamp the certificate is judged at the timestamp's date; without one, at
    today's date, and a certificate expired today gives the distinct status "valid but not verifiable in time"
    (`SignatureStatus.UnverifiableInTime`, a warning, not an error).

23. **Folio "forms only" protection and wider auto-redaction** (user decisions, 2026-11-04; done 2026-11-05): under
    `DocumentRestriction.Forms` only the content of content controls that are not content-locked can be filled
    (`IFormFilling`), never the control itself nor the text outside it; `FolioProtectedContentGuard` nets commands with a
    `FormOutline`. Auto-redaction also clears document properties, renames visible bookmarks (references follow) and
    neutralises comment and change authors, in the same undo step (`IMetadataRedaction`).

24. **Deleting another reviewer's insertion is nested, as in Word** (user decision, 2026-11-05): with tracking on,
    deleting text inside one's own tracked insertion removes it for good; inside another author's insertion the text
    becomes a `Deletion` whose `WithinInsertion` names that insertion (kept alive, even when emptied, while referenced).
    Accepting the deletion drops the text, rejecting it gives the text back to the insertion's author, rejecting the
    insertion removes both, accepting the insertion leaves a plain pending deletion. DOCX writes and reads
    `<w:ins><w:del><w:delText/></w:del></w:ins>`; ODT, which has no nested regions, writes a normal deletion region with
    our `pt:within-insertion` attribute on `text:deletion` (other readers see a plain deletion). Drawn struck and
    underlined in the deleter's colour; the review panel says whose insertion it lies in. Log:
    [docs/deep-audit/folio-nested-revisions.md](docs/deep-audit/folio-nested-revisions.md).

25. **Per-series chart sources** (user decision, option A, 2026-11-05): a Ledger chart keeps the compact form (one
    `SourceSheet` + `SourceRange`) when one range describes it; otherwise `ChartDefinition.SeriesSources` gives each model
    series its own sheet-qualified name, values and categories references (bubble X/Y/size and scatter X are model series,
    as before), on any sheet. Readers settle to the compact form only when it reproduces the read references exactly
    (`IChartSourceLayout`); XLSX writes them as `c:ser` strRef/numRef with caches, ODS as per-series
    `chart:values-cell-range-address`/`chart:label-cell-address` with the list in `table:cell-range-address`. References
    follow row/column insertion and deletion, sheet renaming, and become `#REF!` when their rows or sheet disappear; each
    change is one undo step. The shared chart editor edits each series' range. Log: docs/deep-audit/chart-series-sources.md.

26. **Nothing changes under an open modal** (2026-11-06): the MCP agent's mutating calls are refused while a modal is
    open (`IAgentOperationGate.EditAsync`), and `DialogHost` admits a dialog's answer only if the state seen at opening
    still holds (`IDialogResultAdmission` → `WorkspaceDialogAdmission`: document still open, not newly read-only, every
    module `IDocumentStateWitness` unchanged); otherwise the answer becomes a cancel and the status bar says why. New
    module state that commands rely on after a dialog goes into that module's witness, not into per-command rechecks.
    Log: docs/deep-audit/round-57-after-dialog.md.

27. **Bidirectional text** (round 94, 2026-11-07): paragraphs carry a base direction (`TextParagraph.Direction`,
    `ParagraphProperties.Direction`; ODF `style:writing-mode` rl-tb/lr-tb, DOCX `w:bidi`, PPTX `a:pPr/@rtl`) and runs an
    optional right-to-left embedding (`TextRun.RightToLeft`; DOCX `w:rtl`, ODF our `pt:right-to-left`). Alignment and
    paragraph margins in the model are **logical** (Left = start, MarginLeft = start indent); the ODF and DrawingML
    codecs mirror them because those formats store margins physically. The Unicode Bidirectional Algorithm is our own
    injectable `IBidiAlgorithm` (UAX #9: explicit embeddings/overrides/isolates, weak and neutral rules, paired brackets,
    L1/L2) in `PulseOffice.Text.Rich.Bidi`; Folio's `LineComposer` splits runs at level changes and RTL atoms, places
    them visually per tab segment and mirrors RTL paragraphs (`ILineBidi`); glyph runs carry their `Level`, hit-testing
    and caret Left/Right are visual, selection stays logical. Deck and Ledger shape text use Avalonia's own bidi through
    `FlowDirection`. Log: docs/deep-audit/round-94-bidi.md.

28. **Ledger object anchoring** (2026-11-07): every placed object has `ObjectAnchoring` (MoveAndSize, Move, Fixed;
    Move is the default for objects created in the app). The model keeps one geometry (anchor cell, offset, size) and the
    mode decides how it follows the grid: `SheetLayout` raises `GeometryChanging/GeometryChanged` around every size or
    visibility change and `SheetObjectFollower` (owned by `Worksheet`) settles the objects through `ISheetObjectAnchoring`;
    structure edits suspend those notifications and `ObjectsPart` settles objects from markers captured before the edit.
    XLSX ↔ `editAs`, ODS ↔ `table:end-cell-address` (move and size) and `table:shapes` (fixed), as the reference suite does.

29. **Automatic updates** (user decisions, 2026-11-08): `PulseOffice.Updates` (no Avalonia) asks the GitHub API for the
    latest **stable** release (drafts and pre-releases ignored) at startup, at most once a day (`updates.automatic`, on by
    default; last check and skipped version in `update-state.json`), and on demand (`shell.help.checkUpdates`, Help menu and
    the macOS application menu). A newer version is offered in a notification and a modal (install and restart / later /
    skip this version). The package for the running system and architecture is downloaded only from GitHub hosts, with
    size caps, and installed only if `SHA256SUMS` is signed (ECDSA P-256, `SHA256SUMS.sig`) by the key whose public half is
    `src/PulseOffice.Updates/Trust/update-key.pem` and the package matches its SHA-256; without that file the app only
    points to the download page. `IUpdateInstaller` (Platform): macOS mounts the dmg, requires the same Developer ID team,
    stages the bundle and swaps it after exit, then reopens; Windows runs the NSIS installer with `/S` after exit (UAC
    prompt) and reopens the app unelevated; Linux replaces the running AppImage (`APPIMAGE`) after exit and reopens it;
    runs outside an installed app only point to the download page. `pulse-cmd.py update-key` creates the signing key (private key outside the repository, passphrase asked
    by openssl); `release` writes and signs `SHA256SUMS` and uploads both files.

30. **Linux desktop integration for the AppImage** (user decisions, 2026-11-10): `IDesktopIntegration` (Platform;
    `AppImageDesktopIntegration` in Platform.Linux, `IAppImageLocation` tracks the running image and is shared with the
    updater). On the first start from an AppImage, `DesktopIntegrationCoordinator` (Shell) asks, with the shared
    `ChoicePromptView`, whether to add PulseOffice to the applications menu, by default moving it to `~/Applications`;
    "don't ask again" is remembered in `desktop-integration.json`. Integration writes, for the current user only, a
    launcher script (`~/.local/share/PulseOffice/launch`), the path it starts (`appimage-path`), the `.desktop` entry
    pointing to the launcher, the icon and the file types (`update-desktop-database`, `xdg-mime`, best effort). The
    launcher starts the registered image, otherwise the newest `PulseOffice*.AppImage` in the usual folders, otherwise
    asks with `zenity` or `kdialog` (texts in the five languages from `LANG`) and lets the user pick the file, otherwise
    shows a `notify-send` notice, otherwise does nothing. When the app starts from another place than the registered one
    it offers to move itself to `~/Applications` (or leaves it, updating the menu; "don't ask again" makes it silent).
    Help › Show in Applications Menu adds or removes the integration; the command exists only when running from an
    AppImage.

16. **Folio content controls**: a pair of inline markers with the properties on the start marker,
    not anchored objects. In DOCX form controls are distinguished by the `pulseoffice.form:` prefix in `w:tag`.

## Verified platform APIs

Avalonia 12 changed the window decoration API compared with 11. Verified by
reflection on the assembly, not from the documentation:

| What | Status in 12.1.2 |
|---|---|
| `SystemDecorations` | **renamed** `WindowDecorations` — values `None`, `BorderOnly`, `Full` |
| `ExtendClientAreaChromeHints` | **removed** |
| `HasTitleBar` | **does not exist** on `Window` |
| `ExtendClientAreaToDecorationsHint` | exists |
| `ExtendClientAreaTitleBarHeightHint` | exists |
| `WindowDecorationsTheme` | **new**, it is a `ControlTheme`: decorations are themeable |
| `WindowDecorationMargin` | **new** |
| `Avalonia.Controls.Chrome.WindowDrawnDecorations` | **new**, client-drawn decorations |

Current configuration, on all three systems: `ExtendClientAreaToDecorationsHint=True` + `WindowDecorations=Full`
(2026-11-02). On macOS the native traffic lights remain. On Windows and Linux (`IWindowChrome.DrawsCaptionButtons`,
2026-11-04) the window keeps Avalonia's drawn border, shadow and resize handles, but `ShellWindow` replaces the drawn
decorations theme with `BorderOnlyDecorationsTemplate` (a Decorator over the standard template that keeps only the
underlay): Avalonia's title text, its full-screen button and its caption buttons are gone, and our `TitleBar` draws
three Windows 11 style `CaptionButtons` (46 px, close in red on hover, restore glyph when maximized) carrying the
`MinimizeButton`/`MaximizeButton`/`CloseButton` roles (snap layouts) and running the actions on click themselves.
`TitleBar` starts a window drag only outside buttons, menus and role-carrying elements. Our
`TitleBar` carries `WindowDecorationsElementRole.TitleBar`; menus and search `User`, so they stay clickable.
`IWindowChrome` says where the menus are, the insets and whether we draw the caption buttons.

## Status

The history of the work done — section by section, with decisions, defects found and verifications — is in
[docs/worklog.md](docs/worklog.md). **New log entries are appended there**; here
remains only what applies to every session: decisions, rules, open points and queued work.

### Queued (requested, not yet started)
This entry lists **only what has not been done yet**. The already closed items that used to
appear here — ODF signing, light/dark/system themes, multilingual formulas,
formats and interface — are in the «Status» section, where the log describes them in full;
keeping them here as «queued» too was the file lying about itself.

No module is queued: **Ledger, Deck and Folio are complete** with respect to the command surveys of the
reference spreadsheet, presentation program (367 of 367) and word processor (471 of 471), and
the final cross-cutting audit is closed (log, «Audit finale: chiusura», 2026-10-13). What remains are the defects and
limits declared in «Open points», above all the verifications on real Windows and Linux and in Word, Excel and
PowerPoint, which cannot be done on this machine (a Mac).

The two residual technical points — `CPythonScriptEngine` and DevTools in
Avalonia 12 — stay in the **«Open points»** section below, so as not to list them twice.

### Note on the ODF milestone
The ODF layer (`PulseOffice.Formats.Odf`) is written from the OASIS ODF 1.3 specification: the
spreadsheet, text and presentation content models, the common serialization layer (styles, automatic
styles, master pages) and the ZIP package with its manifest. Files produced by other office suites are
the reference for the *how* in practice, never code to copy. Then the shell: `IWindowChrome` for the three systems,
native macOS menu, modal service, settings schema.

### Open points
- **Agent on Windows and Linux**: server and client tested only on macOS; the owner-only ACL of `WindowsPrivateFiles`
  (`PulseOffice.Platform`, access code and operation log) has not run on a real Windows.
- **Layouts in PPTX exported by other office suites**: some suites write only a few OOXML layout types (`tx`, `obj`, `objAndTx`, `txAndTwoObj`, `blank`), so from such a PPTX some layouts come back approximated. It is their exporter; in ODP the round trip is exact.
- **Printing on Windows**: `WindowsPrintService` chooses the printer (list from `Win32_Printer`, verb `printto`)
  and declares capabilities (no duplex or native copies: copies and collation are obtained by repeating the
  pages in the PDF). Written but **not tested** on a real Windows.
- **mp4 video export on Windows (Media Foundation via Vortice)**: written and wired, but **not
  verified** on a real Windows — this machine is a Mac. macOS (VideoToolbox) and Linux (ffmpeg)
  are verified. To be tested as soon as a Windows is available.
- **OOXML signature in Word**: verified by our tests, by `SignedXml` and by a headless
  check of the reference suite with `tools/verify-signatures` (ODT, ODS, DOCX, XLSX, PPTX, two signatures, broken signature: see log); not yet seen in Word.
- **Signature line in Word and Excel**: the VML now shows a PNG (rasterized by `SkiaVectorRasterizer`),
  but it has not yet been seen open in real Word and Excel; other office suites show it.
- **CPython** (`CPythonScriptEngine`, pythonnet): done, with the declared limits — it does not run in the sandbox (like
  IronPython), the memory cap does not apply, the time cap does not interrupt code stuck in a
  native call, the runtime starts once per process (changing it requires a restart). Virtual environments and
  package installation (pip/uv) are not there yet.
- **Avalonia 12 developer tools**: `AvaloniaUI.DiagnosticsSupport` hooked into Debug builds (F12);
  the tools window is an external program (`avdt`, AvaloniaUI Developer Tools) that requires an Avalonia
  account: not installed here, so F12 has not been tried live.

## Working rules set by the user

1. **No gratuitous static methods.** Composition through interfaces, dependency injection,
   design patterns where needed. `static` only for truly pure, stateless functions
   (mathematical computation), stating why in the documentation.
   **Prefer well-known, appropriate design patterns** (Factory, Strategy, Prototype, Command,
   …) to a class of static methods: a class that builds, clones or transforms must be an
   injectable service behind its interface, not a container of `static`. The only allowed
   `static`s remain pure computations and private helpers of a service.
2. **Never commit or push unless the user asks** (2026-11-04): the user says when and how; commits may carry the
   `Co-Authored-By` line (allowed since 2026-11-04).
2-bis. **Restructure instead of patching**: if a change requires redoing the
   structure, it is redone. A sequence of minimal patches on a structure that no longer holds
   produces unmaintainable code precisely while the project grows.
3. **Cross-platform is a constraint**, not a goal: Windows, Linux and macOS on equal footing.
4. **Complete coverage of Ledger's functions**: all of them, not a subset.
5. **No comments in the code.** Neither line comments nor documentation comments: the
   code explains itself through names and structure. The rule also applies to XAML. The
   *why* of a choice goes in this file or in [docs/plan.md](docs/plan.md), where
   it stays readable without opening the source.
6. **Modals always have a veil behind them.** A window that appears without dimming
   what lies underneath does not read as modal: it is not clear that the rest is suspended, nor
   where it ends. The veil is also what intercepts clicks aimed at what lies
   behind.
7. **ODF remains the default format, but OOXML is always kept aligned.** No
   new feature is finished until it crosses **both** formats: either it is written and
   read back in XLSX as in ODS, or the loss is declared in `CheckFidelity` with its own
   code. A format that loses data silently is the worst defect this project can
   have, and silence is exactly what happens when something is added to one format only.
   Guarded by `XlsxOdsParityTests`. **Declaring is the last resort** (user, 2026-11-10): before declaring a loss, study
   both specifications (ODF 1.3, ECMA-376 with its extension lists and newer namespaces), how the reference suite and
   Microsoft Office store the same thing, and the web; prefer a native mapping, then a standard extension other
   applications read, then our own `urn:pulseoffice:` extension that round-trips and degrades gracefully. The round log
   records what was researched when a loss is still declared.
8. **Log messages are in English.** The log is read by developers and ends up
   in bug reports: Italian would make it unusable outside here. **Interface**
   texts stay translated into the five languages — they are two different audiences.
9. **Deck covers all the layouts and all the animations of the other office suites and Microsoft Office.**
   As rule 4 applies to Ledger, this one applies to Deck: the goal is parity, not a
   subset. In practice: all standard PowerPoint layouts exist, and
   animations (entrance, emphasis, exit, motion paths) cross the two formats **without being lost**.
   The way not to lose them without enumerating hundreds of presets is to **carry the native
   identifier** of the preset in the model, so even a preset the editor does not know survives the
   round trip; the table of known presets is **shared data** between the two mappers, and extending it means
   adding rows, not touching the logic.
10. **Components shared among the three modules.** While developing Folio, Deck or Ledger, if a component
    can be reused it goes into a shared project (e.g. `PulseOffice.UI.Controls`) and is **wired
    into all three modules**, not only the one being worked on. Maximize reusable code
    and user-friendliness; duplicating a component across modules is a defect. Example:
    `SymbolPickerView` (grid symbol picker) in `PulseOffice.UI.Controls`.
11. **Git in English** (2026-11-03): branch names and commit messages are in English; the author is
    `balthier82` with the GitHub noreply email (already in the repository's git configuration); the
    `Co-Authored-By` line is allowed (since 2026-11-04).
12. **Documentation and memories in English** (2026-11-03): every `.md` file in the repository and every
    agent memory is written in English. Interface texts stay in the five languages.
    Restated 2026-11-07: `.md` files and commit messages are **always** in English, and `.md` files never carry
    images as references (no screenshots or image links): what an image would show is described in words. The
    README's app icon is the only image.
13. **The reference suite is never named nor identifiable** (2026-11-03; tightened 2026-11-08): no `.md` file, commit or
    memory names the open-source office suite used as functional reference or points to it — no product or module
    names, source paths or file names, binaries, APIs, macro languages, lineage or legacy brand names. It is "the
    reference suite", and the most generic wording is used. Only code may carry what formats or test tools need
    (vendor-prefixed function names, media types, the test tools that drive it, the MPL 2.0 notice of the bundled
    autocorrect lists in their `NOTICE` file).
14. **Periodic branch cleanup** (2026-11-03): branches and worktrees no longer in use are removed
    periodically, once merged.

## Conventions

- **No comments in the code** (rule 5). Documentation lives in this file and in the
  documents under `docs/`; the names of variables, methods and classes must suffice to read the
  code. Public identifiers in English, because they mirror format concepts
  (ODF/OOXML) that are in English.
- **Log messages in English**, interface texts translated into the five languages.
- One project = one responsibility. If a project starts doing two things, it is split.
- No Avalonia dependencies under `src/PulseOffice.{Foundation,Document*,Drawing,Text,Charts,
  Sheets*,Documents,Presentations,Formats*}`.
- Tests prioritized on what is deterministic and UI-free: calculation engine, ODF, text layout,
  command stack.
