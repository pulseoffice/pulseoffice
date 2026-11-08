# Audit cycle plan

Goal: find and fix defects across the whole app, in cycles, until a complete round finds no more substantial
defects. Each cycle has an area, a method and an outcome written at the end of `docs/worklog.md`.

## Rules common to every cycle
- Defects are sought **from behaviour**, not from reading the code alone: live tests, comparison with the reference
  office suite (headless, own profile; with the UI only with a throwaway profile), format round trips, tests that fail
  before the fix.
- Every defect: cause, root-cause fix, a test guarding it, its own commit.
- All project rules apply (CLAUDE.md): ODF/OOXML aligned, five languages, shared components, no comments, DI, modals
  with a backdrop.
- Cycle outcome: list of defects by severity (blocker, serious, medium, minor), fixed and open.

## First round
| Cycle | Area | Main method |
|---|---|---|
| 1 | **Ledger** | calculation engine and functions compared with the reference spreadsheet on test sheets; ODS/XLSX formats; editor, selection, fill, sort, filters, pivot tables, charts |
| 2 | **Deck** | layouts, animations, transitions, slide show, masters; ODP/PPTX formats compared with the reference presentation program; exports |
| 3 | **Folio** | pagination compared with the reference word processor (same fonts, written tolerance); editor and caret; ODT/DOCX formats on real documents from Word, other office suites and Google Docs |
| 4 | **Shell and cross-cutting** | startup, tabs, windows, modals, settings, recent files, recovery, keyboard and accessibility, themes, five languages, native menu, common printing and PDF export |
| 5 | **Robustness and security** | malformed and truncated files (reader fuzzing), huge files, memory, macro sandbox, signatures, external data and credentials |
| 6 | **Code and architecture** | project rules, duplication, dependencies, unregistered services, slow or flaky tests, performance |

## Subsequent rounds
After the first round it repeats, giving priority to the areas that produced the most serious defects.
It closes when a whole round finds only minor defects.

## Automatic corpus round (`tools/corpus-audit`)
Versioned C# tool (DI, commands as strategies) that rebuilds the round of the eighth and ninth rounds, lost with the
scratchpad. It reads every file in the list `tools/corpus-audit/corpus.txt` (5016 ODS, XLSX, ODT, DOCX, ODP, PPTX from
three corpus files of the external test corpus), rewrites it in the same format and in the other one of
the family, rereads the copies, collects the declared losses (reading the original, `CheckFidelity`, writing,
rereading the copy) and counts the features in the package XML before and after: filters, validations, conditional
formats, pivots, charts, formulas, merges, names, revisions, fields, indexes, frames, shapes, groups, links, images,
lists, notes, comments, tables, sections, animations, transitions, media. A feature whose count drops is **DECL** if the
round declared something, **LOSS** if it vanished silently. Each file runs in a child process with a time limit
(120 s), eight at a time; a whole round takes about eight minutes.

```bash
cd tools/corpus-audit && dotnet build -c Release
B=bin/Release/net10.0/corpus-audit; R=<reference clone>
$B list $R corpus.txt                                 # regenerates the list (only if the clone changes)
$B run $R corpus.txt results.tsv summary.md 8 120    # whole round: DECL/LOSS rows and summary
CORPUS_AUDIT_KEEP=/tmp/copie $B one $R <corpus file>   # one file, keeping the written copies
```

`results.tsv` (versioned) has one row per drop and per copy with its status; `summary.md` (versioned) summarizes by
format pair and feature. Two rounds are compared with `diff` on the two `results.tsv` files.

**Counting artefacts already excluded** (from the entries of the eighth and ninth rounds and from this one): the
content of `mc:Fallback` (VML duplicating the DrawingML); in embedded ODF objects (`Object N/`) only charts are counted;
replacement images next to `draw:object`/`draw:plugin`; `_xlnm.*` names (print areas, filters) and names of external
workbooks; DOCX fields that are something else in ODF (`HYPERLINK`, `TOC`, `INDEX`, `PAGEREF`, `XE`, `FORM*`…);
`w:framePr` with only `wrap`; invisible and empty PPTX shapes (`noFill` and line `noFill`, no text), which Deck skips on
purpose; shapes with text in ODT and DOCX count as frames, VML text boxes always as frames; ODS form controls
(`draw:control`) count as shapes. Validations, conditional formats, revisions, fields, indexes, frames, shapes, links,
lists and transitions are compared by **presence** (the granularity of the two formats does not match: one rule per
range versus one per condition, one `w:ins` per run versus a region), the others by number. A feature is DECL only if
the message of a diagnostic of the round names its subject (`DeclarationMatcher`); the codes column carries the
relevant codes. Also excluded: empty Word text boxes counted as frames, `w:framePr w:dropCap` drop caps,
`word/settings.xml` and unreferenced DOCX headers.

## Status
- First round: cycle 1 (Ledger) closed on 2026-10-13; Ledger editor verified headless in cycle 2.
- Cycle 2 (Deck) closed on 2026-10-13 for formats, animations, transitions and performance.
- Cycle 2 bis (Deck) closed on 2026-10-13: per-preset behaviours in ODP and PPTX, transitions in the slide show, direction of cover/push, editor, slide show, exports and rendering comparison with the reference presentation program; Ledger leftovers (filters, find and replace, dark theme) verified. Open: raster Deck PDF, bitmap fills, custom dashes, HTML paste in Ledger (see the log).
- Cycle 3 (Folio) closed on 2026-10-13: the test corpus in ODT and DOCX with no silent losses (sentinel
  `FolioCorpusParityTests`), pagination compared with the reference word processor with written tolerance (identical
  pages up to 10, ±5% beyond), grapheme-based editor, PDF, EPUB and HTML, performance on 505 pages. Two blockers, nine
  serious, three medium fixed. Open: RTF/HTML clipboard with other apps, Word floating tables, tables of contents with
  the document's TOC styles (see the log).
- Cycle 4 (shell and common parts) closed on 2026-10-16: rich clipboard with other apps, opening from Finder and the
  command line, vector Deck PDF, focus and keyboard, button accessibility, macOS application menu in the five
  languages, WCAG AA contrasts guarded on the tokens, live language switch guarded on all composed views, hang of
  `PulseOffice.Desktop.Tests` in the full suite fixed (with a hang timeout). No blockers; serious, medium and minor all
  fixed; multiple windows not planned by choice (see the log).
- Cycle 5 (robustness and security) closed on 2026-10-16: deterministic fuzzing of the readers of all formats
  (`tests/PulseOffice.Robustness.Tests`, long run with `PULSEOFFICE_FUZZ_ROUNDS` and `PULSEOFFICE_FUZZ_SEED`), input
  inspection of files (nesting, ZIP and entity bombs, XXE), sanitized SVG, linked images and executable objects not
  opened without consent, consent to document macros according to the security level, document SQL read-only, forged
  signatures guarded, atomic save also for Save As, recovery 700/600, 1 GB limit. Two blockers and nine serious fixed;
  open: Jint memory peak on a single allocation and non-atomic exports (see the log).
- Cycle 6 (code and architecture) closed on 2026-10-16: atomic exports, Jint cap on single allocations, project rules
  guarded by `SourceRulesTests`, opening and saving of the three modules unified, Folio hyphenation wired, dead code
  removed, Argon2id without per-block allocations. **First round closed.**
- Second round, second part (2026-10-16): Exchange robustness (hostile packages), HTML/RTF/ODT clipboard, raster headers
  (BMP/TIFF/WebP) and decoding under a single cap; duplications unified (format registries, open as new, tab strips,
  dialog parts, HTML/RTF adapters); number formats across folders. Four serious and four medium fixed; open: live test
  of Exchange (screen locked), clipboard in the other office suite's format, object PNGs and ODP/PPTX of the Deck
  selection (see the log).
- Second round, third part (2026-10-16): Ledger aligned with the reference spreadsheet on the items left open by cycle 1
  (3D references, `~`, CURRENT, FORECAST.ETS, inverse and Bessel functions, dynamic cycles, GETPIVOTDATA, memory from
  364 to 260 MB); Exchange (object PNGs, ODP/PPTX of the Deck selection); Deck (bitmap fills, image backgrounds of the
  master and the slide, exact dashes, pen, laser and presenter view). Five serious, eleven medium and five minor fixed.
  **The second round did not find only minor defects**: a third round is needed, focused on (1) rendering compatibility
  of Deck with files produced by other office suites (images in master pages, presentation styles of placeholders,
  natural size of tiles, line widths), extending the rendering comparison to all ODP and PPTX of the presentation corpus; (2) live test
  of Exchange and clipboard in the other office suite's format, as soon as the screen is unlocked; (3) the debt of
  services built with `new`; (4) the Ledger functions still differing in the test corpus (error
  codes, FORMULA text) and GETPIVOTDATA with hidden subtotals.

- Third round, first part (2026-10-16): presentation styles of placeholders, master page images, raster fallback of
  SVG images; automatic rendering measurement on all ODP and PPTX of the presentation corpus (`tools/deck-render-compare`, threshold:
  every slide ≥ 0.80, mean ≥ 0.92). ODP 0.945 with 13 below threshold, PPTX 0.908 with 59. **Closing criterion not
  met**: serious items remain (background gradients, SVG, SmartArt, PPTX tables) and items 2–5 of the round were not
  addressed; no live tests (screen always locked). See the log.

- Third round, second part (2026-10-16): gradient/hatch/image backgrounds, PowerPoint table styles, SmartArt from the
  fallback drawing, arcs and sectors, text box frame, tiled images; debt of services built with `new` cleared; Ledger
  from 2250 to 1526 differences in the corpus (automatic labels, LOOKUP, CONVERT, LAMBDA). Measurements: ODP mean 0.967
  with 5 slides below 0.80, PPTX 0.954 with 25. **Closing criterion not met**: the mean is above threshold in both
  formats, the per-slide threshold is not (SmartArt without drawing, duotone, tiles). Only partial live tests (Ledger
  chart on the clipboard; ODT crash found and fixed).

### Summary of the first round for the second
| Area | Blockers | Serious | Notes |
|---|---|---|---|
| Folio (cycle 3) | 2 | 9 | pagination and formats; open floating tables, TOCs with TOC styles |
| Robustness and security (cycle 5) | 2 | 9 | readers, consent to external content, SQL, saving |
| Code and architecture (cycle 6) | 0 | 4 | functional defects uncovered by clones and dead code (hyphenation, Ledger recent files) |
| Deck (cycles 2 and 2 bis) | — | various | open bitmap fills, custom dashes |
| Ledger (cycle 1) | — | various | open HTML paste (closed in cycle 4) |
| Fifth round (2026-10-18) | 0 | 4 | series colours, Folio→Deck table, direct DOCX borders, a corpus file in ODT; four medium and three minor, all fixed |
| Ledger and Deck live (fourth round, 2026-10-18) | 0 | 6 | **Ledger status: closed live**; pivot chart series names and duplicate "Export PDF" closed, the band in the slide show came from the capture (see log) |
| Shell (cycle 4) | 0 | few | all fixed |

Proposed priority for the second round: **Folio** (most serious, riskiest subsystem), then **robustness** (new readers
and paths added after cycle 5), then **code** on the remaining clones (format registries, data views,
`PulseOffice.Exchange`), which have already shown they hide functional divergences; Deck, Ledger and shell afterwards.
- Second round, first part (startup, test stability, Exchange and Folio) closed on 2026-10-16: the app that "closed at
  startup" crashed with the screen off (Avalonia.Native without a display link) and now waits for an active screen; the
  test processes hung for hours were xunit v3 processes surviving their launcher and are now terminated by a common
  watchdog; Folio: Word floating tables, the document's TOC styles, continuous endnotes, hidden revisions written by
  other office suites, tabs beyond the margin, grapheme-based clicks, table at the end of the document; the test corpus
  at 936 of 1053 within tolerance (with `tools/folio-pages`). Live test of Exchange not done (screen locked).

- Third round, third part (2026-10-17): formula separators according to the language (user decision), SmartArt layout
  engine, duotone, merges in table headers, deterministic Ledger corpus comparison; live tests of Exchange, clipboard
  with the other office suite, menus and live language switch. Measurements: PPTX 0.961 with 17 below 0.80, ODP 0.960
  with 13. **Closing criterion not met**: SmartArt with drawing, large tables, master decorations and tiles below
  threshold remain, as does the real Ledger residue (see the log).


- Fourth round, first part — real use of the app (2026-10-17): live tests with windowshot on cross-cutting scenarios
  (budget in Ledger, sheet portion in Deck as an object, Deck text box in Folio, command search, settings, dark theme).
  Closed the two serious items from the previous round (sheet object shown as an icon; Deck text box turning into
  text), fixed two medium and two minor. **The full requested scenarios (five-page report in Folio, budget with pivot
  and chart, eight-slide presentation shown in full, PDF) were not all walked through live**: they remain for the
  second part of the fourth round (see the log).
- Fourth round, second part — Folio live (2026-10-17): five-page report with all the required elements, ODT and DOCX
  saved and reopened (ODT byte-for-byte identical), comparison of pages and positions with the reference word
  processor, keyboard only, undo/redo of more than 50 steps, dark theme, language switch, two DOCX from the test corpus
  corpus edited and reopened. Ten serious fixed (focus after the ribbon, Tab in lists, per-character undo, footnotes,
  repeated page break, header without caret, autocompletion accepted silently, DOCX header margins, empty paragraph
  mark, break before a control), seven medium and four minor, including the vertical ruler. One medium left open
  (a corpus case in ODT: 4 pages in the reference word processor) and some minor ones (see the log).
- Fifth round, first part (2026-10-18): closed the two open Folio items — a corpus file in ODT now 3 pages in the
  reference word processor (even pages without header as in Word, generic font family, Wingdings symbols), style
  preview readable in the dark theme. One serious and one minor, fixed. **Round not complete**: Ledger, Deck, Exchange,
  printing/PDF and languages live remain (resume point in the log); it cannot yet be said that the round found only
  minor defects.
- Fifth round, second part (2026-10-18): Ledger (`many_charts.ods`) and Deck (a corpus file) live with the reference
  suite for comparison, Exchange among the three modules (chart, table, sheet portion, paste special), PDF from the
  three modules, UI in English, German and French, dark theme. Three serious (chart series colours lost in ODS and XLSX,
  Folio table pasted into Deck as text, direct borders of DOCX tables), four medium and three minor: all fixed.
  **Status: round closed, but it did not find only minor defects** — four serious in total (with a corpus file from the
  first part), no blockers. The "only minor" criterion remains to be reached in a sixth round.
- Sixth round (2026-10-18): automatic ODF/OOXML round on new corpus files with feature counting and comparison against
  the reference suite's PDF on 22 ODP, plus live tests in Deck and Ledger. Two serious (colour filters read as text
  filters; repeated cells with the default style of the first column), eight medium and three declared losses, all
  fixed; three minor open. **Status: the sixth round did NOT find only minor defects** (two serious, both in Ledger on
  files produced by the reference suite). A seventh round is needed, with the same automatic method extended to the
  whole spreadsheet and text corpora (feature counts before and after) and the three open minor items (see the log).
- Seventh round (2026-10-18): the spreadsheet and text corpora with feature counts before and after, Deck on the three minor
  items of the sixth round, live tests of the shell (atomic writing, macro sandbox, focus in modals, Office templates,
  accessibility). One blocker (XLSX shared formulas), serious items in Ledger, Folio and shell, all fixed or declared
  (`XLSX080`–`082`, `ODF080`–`081`, `DOCX121`–`126`, `PPTX037`). **Status: the seventh round did NOT find only minor
  defects.** Open for the eighth round: rich text in shapes (Folio), ODT object index, ODT sender fields, x14 data bar,
  new conditional operators in the Ledger dialog, ODP default language lost, CJK text without an Asian font in ODP,
  x-symbol charset of bullets (see the log).
- Eighth round (2026-10-18): four branches (Deck, Ledger, Folio, corpus) on the open items of the seventh round and on
  the corpus, plus live tests of Ledger conditional formatting and of rich text in Folio shapes. Serious fixed:
  ODP/PPTX default language, CJK text, x14 data bar, rich text in shapes, discarded PPTX `custGeom`, links in Deck,
  legacy form fields, OOXML scatter/radar/3D charts, VML images, ODP animations; plus medium and minor. Corpus residue
  from 604 to 372 declared losses. Live: status bar summary not translated, pending format in Folio, ODT shapes read as
  text boxes and shape fields discarded by the reference word processor, all fixed. New codes: `PPTX038`,
  `PPTX042`–`044`, `ODF082`, `ODF086`–`087`, `ODF090`, `ODF095`, `DOCX127`–`128`, `DOCX135`–`136`, `XLSX087`–`088`.
  **Status: the eighth round did NOT find only minor defects.** Open for the ninth round: Deck groups flattened on read,
  Deck media (repeat, volume, linked), SVG in OOXML with PNG fallback, rich text lost in Folio groups, colour and
  centring of the shape with the style, on-screen drawing of `AsianFont` and of data bar options,
  `dml-textshape.docx` with two missing text boxes, and the minor items listed in the log.
- Ninth round (2026-10-18): four branches (Ledger and shell, Folio, DOCX corpus, Deck) on the open items of the eighth,
  plus live tests of data bars, dates, the macro IDE in German, Folio shapes and groups, Deck groups and SVG, CJK.
  Serious fixed: on-screen CJK without an Asian font, shapes with style without fill, shape text as a character, rich
  text in groups, VML of `dml-textshape.docx`, moved paragraph marks, linked media lost, Deck SVG lost, Deck groups
  flattened. Live, fixed: flipped shapes drawn out of place and arrowheads never drawn (all three modules), app crash
  when grouping with the caret in a shape's text, SVG inserted in Deck saved as a fake PNG, new objects below read
  groups, shape text not centred. Corpus residue from 343 to 328 declared losses. Codes removed: `PPTX044`, `ODF095`,
  `PPTX009`, `ODP014`; new: `DOCX140`–`141`, `DOCX150`–`152`. **Status: the ninth round did NOT find only minor
  defects** (serious both in the branches and live). Open for the tenth round: rich text drawn inside groups, single
  VML, editor group not reselectable as a unit after reopening (and PPTX group members selected alone), linked media not
  played, XLSX vectors as `svgBlip`, corpus docx/odt with frames, links, images and lists, XLSX dynamic arrays, `IF`
  with nested fields, PPTX animations two corpus files, `custGeom` with text and colour from `fontRef` in PPTX,
  and the minor items listed in the log.
- Tenth round (2026-10-19), first part: four branches (Deck, Folio, Ledger, DOCX/ODT corpus) on the open items of the
  ninth, merged into main, suite green. Serious fixed: Deck groups and DOCX text box groups not selectable as a unit,
  rich text of groups not drawn, animations on OLE on the wrong target, XLSX/ODS dynamic arrays not aligned; plus
  medium and minor. New codes: `DOCX160`–`161`, `DOCX170`–`173`, `ODF170`, `ODF175`–`178`. Automatic corpus round and
  live tests **not yet done** (corpus tools lost with the scratchpad: they must be rebuilt in `tools/`).
  **Status: the tenth round did NOT find only minor defects**; it stays open until the corpus round and the live tests.
- Tenth round (2026-10-19), second part: corpus tool rebuilt in `tools/corpus-audit` (versioned list and results),
  round on 5016 files: **new baseline 186 LOSS**, 850 DECL. Serious fixed: free PPTX text boxes written and read as
  placeholders; DOCX shapes without fill, border and text colour from the theme (`wps:style`). Medium fixed: text of
  Folio group members laid out without paragraphs (now with the Folio engine). Seen on screen: groups of `group.pptx`
  (selection as a unit, moving, rich text), rotation of `group-rot.pptx` identical to the reference presentation
  program, Folio DOCX group after the fixes. Not tested live (app could not be brought to the foreground): linked media,
  date in Ledger, macro group in Tools, reopened editor group. **Status: the tenth round did NOT find only minor
  defects.** Open for the eleventh round: those four tests, the 186 LOSS, the minor items listed in the log.
- Eleventh round, first part (2026-10-19): corpus DECL tied to the feature through the diagnostic's message
  (`DeclarationMatcher`), counting artefacts removed (empty text boxes, drop caps, `text:h`, `settings.xml`, orphan
  headers), DOCX171 extended to fields and tables: LOSS 368, DECL 575, not yet analysed one by one. Fixed: DOCX lines
  without an empty text box or fill, bullet on empty paragraphs, Deck and Ledger macro group identical to Folio (medium,
  seen live). Seen on screen: date in Ledger, Folio and Deck macro group. **Status: only minor and one medium, no
  serious.** Open: the 368 LOSS, linked media and editor group live, group frame in Deck.
- Eleventh round, second part (2026-10-19): partial analysis of the LOSS, **368 → 303** (DECL 528). Tool artefacts
  fixed: `w:framePr` in styles (latent styles such as "envelope address"), empty fields without an instruction, bullet
  images (`w:numPicBullet`), headers and footers not referenced by any relationship, Deck links written as
  `presentation:event-listener`, lists inside ODT comments. Real loss: text of freeform DOCX shapes (`custGeom` with
  `wps:txbx`) lost in Folio without warning, now declared `DOCX174` (**serious**, the `FreeformObject` model has no
  text). Single frame of the selected group in Deck (minor, fixed). Seen on screen: linked media with a relative path
  (`../media/ping.m4a`) opened from the slide show in the system player. Not tested live: editor group saved and
  reopened (app could not be brought to the foreground). **Status: the eleventh round did NOT find only minor** (one
  serious declared, one medium in the first part). Open: the remaining 303 LOSS, text in Folio freeform shapes (to be
  brought into the model), image inside a tracked deletion in a note (a corpus file, minor), live test of the editor
  group and of the group frame.

**Twelfth round, first part (2026-10-19): only one serious found and closed** (text of Folio freeform shapes, now
carried in DOCX and ODT), ⌘Q without documents verified live. Resume point: rerun `tools/corpus-audit` (the test corpus
rows change), then the LOSS from `.odt shapes`, `.docx frames`, `.xlsx shapes` (form controls; ActiveX declared),
a corpus file; live tests of groups in Deck and Folio and of the single frame.

**Twelfth round, second part (2026-10-19)**: freeform shape text confirmed in the reference word processor; fixed links
on Folio objects (serious: shapes in `draw:a` silently lost) and Deck automatic advance without effect (medium);
declared `XLSX089`, `DOCX175`, `DOCX176`, `ODT009`; LOSS 303 → 183. Groups created in the editor seen on screen in Deck
and Folio, saved in native and OOXML and reopened as a unit. **Status: the round did NOT find only minor.** Open:
reopened group in Deck without resize and rotation handles (medium, two models to unify), XLSX form controls in our own
form (medium, `XLSX020`), first click after a modal in Deck (minor), the 183 LOSS.

**Thirteenth round (2026-10-19)**: three branches merged into main. Deck: the two group models (reopened `SlideGroup`
and `ShapeGroupObject` created in the editor) unified into `SlideGroup` behind `ISlideGroupEditing`, with handles,
rotation and copy (medium, closed); first click on the canvas after a popup solved with `IFlyoutDismissal`, a shared
component wired into the three modules (minor, closed); seen on screen. Ledger: XLSX form controls written in the
standard form (`ctrlProp` and VML), verified in the reference spreadsheet, `XLSX020` narrowed to what Excel cannot keep
(medium, closed). Corpus: LOSS 183 → 5 before the merge, DECL 606 → 490; after the merge **LOSS 5, DECL 488**. New
codes `DOCX180`–`183`, `ODT180`–`181`, `PPTX180`–`184`, `ODP180`–`183`, `XLSX180`–`183`, `ODF190`–`194`, verified
unique. **Status: the thirteenth round did NOT find only minor** (serious fixed and serious only declared, see log).
Open for the fourteenth: interpreter of OOXML preset geometries (`presetShapeDefinitions`) and of ODF formula-based
geometries; hidden shapes and links in shape text in Deck; PPTX sounds; links on objects in Ledger; linked cell of
controls losing the sheet name; the 5 residual LOSS (a corpus file groups, a corpus file images in ODP, a corpus file DOCX
frames twice, `sectionprot2` revisions in DOCX) and the minor items.

**Fourteenth round (2026-10-24)**: three branches merged into main (computed geometry engine; hidden shapes, links in
shape text and sounds in Deck, links on objects and linked cell with sheet in Ledger; residual losses, DOCX195, ODT list
indentation). Corpus: **LOSS 3, DECL 481** on 9948 copies (previously 5 and 488). Verified with the reference suite and
live in light and dark; three new defects found and fixed (Deck shape text out of scale in the slide show and
thumbnails, unnumbered item of an inherited list lost in ODT, PPTX with two SmartArt in the same group unreadable, a
merge regression). **Status: the fourteenth round did NOT find only minor**: one serious closed (preset and
formula-based geometries) and silent defects fixed; only minor items remain open (see log, "Audit, quattordicesimo
giro"). The next round can start from the open minor items and the three remaining LOSS. Closed after the round:
revisions in ODT headers (`SdtContent.docx` from LOSS to OK) and default tab distance in Folio (log, "Quattordicesimo
giro, minori di Folio").
"Audit, quattordicesimo giro"). The next round can start from the open minor items and the three remaining LOSS.

**Fifteenth round, Ledger and Deck (2026-10-24)**: closed the open minor items of the fourteenth for Ledger and Deck
(localized formula bar, cell linked to another sheet, click sound with the `dom` namespace declared, appearance of links
in shape text, text frames in `table:shapes`) and a medium found live (click sound hidden and deleted by the choice of
action). Corpus **LOSS 1, DECL 481**. See log.

**Fifteenth round (2026-10-24)**: three branches merged; corpus **LOSS 0, DECL 478**. **Status: the fifteenth round did
NOT find only minor**: no blockers or serious, but six medium (fixed, see log). The closing criterion is getting closer:
the next round starts from the open minor items and must test live the adjustment handles in the three editors.

**Sixteenth round (2026-10-24)**: three branches merged; corpus **LOSS 0, DECL 478**. **Status: the sixteenth round did
NOT find only minor**: one serious fixed (adjusted Folio shapes in ODT) and **one serious open** (text not writable in
Deck and Ledger shapes), two medium fixed. The seventeenth round starts from the shared inline text editor for shapes,
then from the points not tested live (linked cell, mail merge, animations and notes) and the open minor items (see log).

**Seventeenth round (2026-10-24)**: shared inline text editor for shapes (`ShapeTextEditor`) wired into Deck and Ledger,
formatted runs of shapes in ODF and OOXML, minor items of the sixteenth closed; corpus **LOSS 0, DECL 478**. **Status:
the seventeenth round did NOT find only minor** (one serious and two medium, all fixed); only minor items open. The
eighteenth starts from: ribbon state on Deck shape text, automatic ink in Folio shape text, live tests of the linked
cell and mail merge, OOXML saving from the app (see log).

**Eighteenth round (2026-10-24)**, goal "only minor": closed the minor items of the seventeenth (Deck ribbon on shape
text, automatic ink of Folio shapes, cell commands with a shape in Ledger, first character of `uiprobe`); new command
"Save in Microsoft Office format…" tested live in the three modules; tested live the linked cell with inserted rows and
mail merge (preview, single document, one DOCX per record); explored keyboard, screen readers, live language switch
and large documents. Corpus **LOSS 0, DECL 478**. **Status: the eighteenth round did NOT find only minor**: no blockers
or serious, but four medium (main button invisible in the data dialogs, Deck slide panel not usable from the keyboard,
Ledger grid silent for screen readers, slow and blocking opening of the 100,000-row sheet), all fixed. The nineteenth
starts from the open minor items (default labels of form controls, F6 cycle among areas, saving large files on the UI
thread) and from a second keyboard and screen reader round on Folio (see log, "Audit, diciottesimo giro").

**Nineteenth round (2026-10-24)**, goal "only minor": closed the minor items of the eighteenth (default names of form
controls in the UI language, F6/Shift+F6 cycle among ribbon, document areas, side panel and status bar with visible
focus, saving off the UI thread with progress, pending or on-word format in shape text); tested live mail merge from the
previewed record, Deck slide panel from the keyboard and Ledger's new keys. Explored paste from other apps, print
preview, recovery after a forced close, corrupted files and several documents at once. Corpus **LOSS 0, DECL 478**.
**Status: the nineteenth round found ONLY minor defects** (borders of HTML tables in Folio, technical messages for broken
files with a shared code, provider name, "Preview" button, focus ring of thumbnails), all fixed. Open, minor: borders of
HTML tables pasted into Ledger; Deck ribbon "B" off for pending bold before typing. Plan criterion met; a further round,
if desired, on keyboard and screen readers in Folio (see log, "Audit, diciannovesimo giro").
- **Twentieth and twenty-first rounds** (2026-10-25): from the resume point of the twentieth — CSV opened by the agent
  without a window (detected options or `csv_*`), native bubble and stock charts in DrawingML, external data ranges in
  XLSX as query tables (XLSX010 now informational), signing from a PKCS#12 file from the dialog (seen on screen), minor
  items of macros, icon, focus, `AppendParagraph` and `save_document_as`; found and fixed a medium (`Read` of the agent
  and of macros did not show the text as it appears). Exploration: undo/redo of 45 mixed steps in Ledger, closing with
  changes (Cancel), Ledger→Folio paste. Corpus **LOSS 0, DECL 478**. **Status: the twenty-first round did NOT find only
  minor** (one medium, fixed). Resume point for the twenty-second in the log ("Audit, ventunesimo giro").
- **Twenty-second round** (2026-10-25): Ledger→Folio paste with bold, italic and alignment of cells (medium, fixed and
  seen on screen); undo of one hundred agent steps in Folio and Deck, theme and language with unsaved documents, ⌘Q with
  four unsaved documents (the modal now lists the titles: minor, fixed); shapes with formatted text and groups across
  modules guarded by tests. Corpus **LOSS 0, DECL 478**. Signature verification in the reference suite still not
  concluded. **Status: the twenty-second round did NOT find only minor** (one medium, fixed). Resume point for the
  twenty-third in the log ("Audit, ventiduesimo giro").
- Twenty-third round (2026-10-25): signature verified by the reference suite run headless (`tools/verify-signatures`,
  `ZipFormat` storage): ODT, ODS, ODP, DOCX, XLSX, PPTX valid, altered copies invalid, calibrated on
  `good.odt`/`bad.odt`/`multi.docx`/`bad.docx`; signing of ODT with Basic macros (medium, fixed). HTML from web pages,
  Google Docs, Word, Excel and other office suites: transparent rendered black (medium), white site backgrounds, system
  fonts, absurd measurements, Ledger colours (minor), all fixed and the web page seen on screen. Folio and Deck driven
  only by the agent: silent invalid references and wrong slide in `render_page` after a hidden one (medium), plus eight
  minor, fixed; reads, screen, ⌘Z, permissions and confirmations seen on screen. Corpus **LOSS 0, DECL 478**.
  **Status: the twenty-third round did NOT find only minor** (four medium, fixed). Resume point for the twenty-fourth in
  the log ("Audit, ventitreesimo giro").
- Twenty-fourth round (2026-10-25): ODT with macros signed by the reference suite opened live (signature shown, now
  "certificate not verified"); Basic macros of ODT and ODP silently lost (serious, now `ODF041` and counted by the
  corpus round); web tables pasted into Ledger with merges, caption and alignments, and merges preserved by copy and
  paste (medium); long agent run on Ledger: sheet and structure protection ignored by macros and agent (serious),
  `run_command` reporting success when the app refused (medium), invalid formulas written as `#NAME?` (medium);
  "one case per catalogue member" contract in the three modules (`LedgerFacadeContractTests`,
  `FolioFacadeContractTests`, `DeckFacadeContractTests`) with some fifteen silent "does nothing" (medium) and two
  serious in Folio on paragraph protection; two flaky suite tests fixed. All fixed. Corpus **LOSS 0, DECL 529**. No
  clicks: the app would not come to the foreground, the "live" part went through the MCP agent with window captures.
  **Status: the twenty-fourth round did NOT find only minor** (five serious, fixed). Resume point for the twenty-fifth in
  the log ("Audit, ventiquattresimo giro").
- Twenty-fifth round (2026-10-25): the app came to the foreground and tests with clicks and keys were done (sheet
  protection from the UI, Paste Special of HTML, signatures window also from the status bar, tab colour palette). Run of
  **all enabled commands** of the three modules in a typical context (`CommandSweepTests`: no exceptions, no infinite
  waits, no "does nothing" without a reason) and contract of the hand-written agent tools (`AgentToolContractTests`).
  Three medium — command refusals not observable as a result (now `CommandOutcome`, reported by `run_command`),
  "Select row/column" limited to the used area, Deck images and media outside the shared components with unreadable
  images ignored — and some fifteen minor, all fixed. Corpus **LOSS 0, DECL 529**. **Status: the twenty-fifth round did
  NOT find only minor** (three medium, no serious). Resume point for the twenty-sixth in the log ("Audit,
  venticinquesimo giro").
- Twenty-sixth round (2026-10-25): run of the commands in seven new contexts (object selected, text in a shape, cell
  being edited, read-only, protected sheet, protected section, slide show running), in its own project
  (`PulseOffice.Desktop.Sweep.Tests`). Found two serious — sheet protection bypassed by insertions, deletions, objects,
  merges and writes to locked cells; a protected Folio section editable through lists and styles — closed with a command
  effect guard (`ICommandEffectGuard`, one strategy per module, which undoes and says why), two medium (silent refusals
  in protected sections; Deck text dialogs blind to shape text) and a dozen minor, including context menus that stopped
  the arrow keys on group headers and the orange accent of Deck thumbnails. Icon stroke in the dark theme compared live
  before and after: no defects. Corpus **LOSS 0, DECL 529**. **Status: the twenty-sixth round did NOT find only minor**
  (two serious, two medium, all fixed). Resume point for the twenty-seventh in the log ("Audit, ventiseiesimo giro").
- Twenty-seventh round (2026-10-26): live test, in light and dark, of the classes restructured by the review (Ledger
  grid and commands, Deck view and actions, composition): no regressions. Seven minor fixed — row header too narrow for
  seven digits, text of rotated shapes not rotated, Deck inspector without transitions on opening, placeholder hints in
  the slide show, comment marker under the filter button, tabs with the same name, undeclared columns of an ODS from
  another office suite rendered narrow — plus a defect of the `uiprobe/keys` tool (modifiers left pressed). Corpus
  **LOSS 0, DECL 529**. **Status: the twenty-seventh round found only minor defects.** Still optional: shared gestures in
  `UI.Controls` and the Visitor of `DocxInlineReader`.
- Twenty-eighth round (2026-10-27), **partial**: `DocxInlineReader` with per-element strategies instead of the switch
  (unchanged behaviour, corpus **LOSS 0, DECL 529**); inline editor of the rotated shape in Ledger seen on screen already
  rotated with the shape. No defects found, but **it cannot be said that the round found only minor**: the shared
  gestures among the three modules and the broad live Folio round are missing, and move to the twenty-ninth.
- Deep audit, first round (2026-11-02): full reading of all the code by area, branches merged into `audit-profondo-1`;
  after the merge two performance regressions and two medium fixed (see log). Corpus **LOSS 0, DECL 529**.
  **Status: the first round did NOT find only minor** (serious in almost every area); the second round follows.
- Deep audit, second round (2026-11-03): six areas, branches merged into `deep-audit-round-2`; one severe regression of
  the first round (volatile SUBTOTAL) and several quadratic paths and silent caps fixed (see work log). Corpus unchanged.
  **Status: the second round did NOT find only minor defects**; a third round follows.
- Deep audit, round 119 (2026-11-08): Folio HTML/RTF readers left by round 115 (tail of the RTF and HTML readers,
  `HtmlTableReader`, `HtmlCellBorders`, `RtfPicture`). Two medium (row spans lost or overrunning the table on paste,
  list headers numbered in HTML) and minor defects fixed; log in docs/deep-audit/round-119-folio-readers.md.
- Deep audit, round 121 (2026-11-08): HTML tables and the HTML clipboard path of the three modules. The three limits
  left by round 119 fixed at the root (nested tables, mixed inline formatting, border longhands), borders now
  written by the Folio HTML writer and Ledger's fragment; medium and minor defects fixed; log in
  docs/deep-audit/round-121-html-tables.md.
- Deep audit, round 118 (2026-11-08): Deck graphic-style relinking through groups, masters and layouts; ODS cell-style
  inheritance from other suites and explicit resets under named styles; `Default` parent kept; PPTX shape-style fidelity
  rules extended to every drawn object. Log: [deep-audit/round-118-deck-styles.md](deep-audit/round-118-deck-styles.md).
  **Status: grave defects found and fixed**, minor residuals listed in the log.
- Deep audit round 120 (2026-11-08): Ledger filter leftovers of round 117 and the sort code; two medium (dates in
  saved value filters now written as XLSX `dateGroupItem` and ODS ISO text; sorting refuses rows holding part of an
  array formula) and six minor fixed; sort settings persistence noted as open. Log:
  [deep-audit/round-120-ledger-filter-ui.md](deep-audit/round-120-ledger-filter-ui.md).
