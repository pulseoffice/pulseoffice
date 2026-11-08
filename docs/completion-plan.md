# Completion plan: every feature

Requirement set by the user: **all of them**, not a subset. This document says how to
get there without losing anything along the way and without doing the same things twice.

## The yardstick, before the plan

A promise of completeness that cannot be measured is worth nothing. The yardstick is the same one
already used for the sheet functions — a report that regenerates itself — applied to commands:

- a command map with **426 rows**, one per menu command of the reference spreadsheet, with its
  status (`fatto` done, `parziale` partial, `manca` missing, `escluso` excluded) and our corresponding command;
- a census script that regenerates the menu tree from the reference clone, appends at the end the new
  commands that might appear in a later version, and **verifies that the commands of ours cited in
  the map really exist** — if a command is renamed, the map stops being a lie because the census fails.

Today:

```
fatto 233 · parziale 20 · manca 135 · escluso 53
copertura sui comandi voluti: 233/373 (62,5%)
```

Done: **B1-B17, B19, B20** (the parts that were commands), plus the zooms, the view toggles
and the file commands. Three subsystems and a queue of minor items remain — see the end.

The census counts **menus**, and this makes it wrong at one precise point: CSV in the reference
spreadsheet is not a menu command but a filter of the save dialog, so in the map it is worth
a single row while in the work it is worth a whole block. Where the number and reality diverge,
the block rules.

## How a block is closed

A block is not finished when it compiles. It is finished when:

1. the map rows it claims to close move to `fatto`, and the census confirms it;
2. **the round trip with the real reference suite** has been done in both directions, in ODS and in XLSX, when the
   block touches the format — that is how every fidelity defect so far has been found;
3. **the application has been driven live** with `tools/uiprobe` along the block's main path
   — that is how the inert title bar, the invisible modals, the arbitrary menu order
   and the undo that did not redraw were found;
4. the deterministic tests cover the computation, not the appearance;
5. CLAUDE.md is updated, including the traps found: they are worth more than the code.

## Order

Not by number of commands, but by **damage avoided**, then by **real use**, then by what
unlocks the rest. Blocks 1-3 come before anything else because today we silently lose
other people's data.

### B1 — Merged cells (3 commands, but worth it on its own)
**Content**: merge, merge and centre, unmerge.
**Structure**: it is the most invasive change of the plan and must be done first, while the code
affected is still small. It touches the model (`Worksheet` with the list of merged regions and the
anchor cell), drawing (a region is drawn as a single cell, and the text is the
anchor's), hit testing and selection (clicking a covered cell selects the
region), arrow navigation, fill, sorting, insertion and
deletion of rows and columns, printing, ODS (`table:number-columns-spanned` and
`covered-table-cell`) and OOXML (`mergeCells`).
**The decision**: covered cells **stay in the model** with their content. Whoever unmerges
must get back what was there, and the formats write them anyway.
**Finished when**: a sheet from the reference suite with merged titles opens, is edited, is saved and
goes back to the reference suite identical; and the same in XLSX.

### B2 — Interchange formats: CSV, text, HTML
**Content**: CSV reading and writing with the options dialog (separator, quotes,
encoding, number language, type per column, preview), fixed-width text, HTML
reading.
**Structure**: fills `PulseOffice.Formats.Interchange`, which today contains a placeholder. The
CSV reader **is already half written**: `TextToColumns` does exactly this work on
quotes, and must be extracted into a shared parser instead of being rewritten.
**The decision**: numbers and dates are interpreted according to the language **chosen in the dialog**,
not that of the system. An American CSV opened in Italy with decimal comma is the
classic way to ruin a thousand rows without noticing.
**Finished when**: a CSV written by us opens in the reference spreadsheet and in Excel with the right columns, and a
CSV with quotes, line breaks inside fields and BOM is reread without loss.

### B3 — Raster images (8 commands)
**Content**: insertion of PNG, JPEG, GIF, BMP, TIFF; crop, original size, fit
to cell, replace, compress, save, alternative text.
**Structure**: `RasterImageObject` as an `EmbeddedObject` next to the vector one, the original
bytes in the document, `Pictures/` in ODF and `media/` in OOXML with the relationships. **It unlocks
SVG in XLSX**: OOXML requires a raster thumbnail next to the vector, and until we
can produce one SVG degrades to geometry.
**The decision**: the original file is never re-encoded on save. Recompressing a JPEG
at every round trip wears it out.
**Finished when**: someone else's sheet with photos opens, is moved, is saved in both formats and the
images remain the same.

### B4 — Document properties (8 commands)
**Content**: title, author, subject, keywords, comments, dates, statistics; reload,
save a copy, save all, templates, printer settings.
**Structure**: a metadata layer in `PulseOffice.Document` that ODF (`meta.xml`) and OOXML
(`docProps/core.xml` and `app.xml`) map. Today **we discard them**: opening and resaving
someone else's document erases its author.
**Finished when**: open, save, and the original metadata are still there.

### B5 — Everyday gestures (35 commands)
**Content**: directional fills (⌘D and its three siblings), fill series and random
numbers, paste special with transpose and operations, clone formatting, clear formatting,
repeat last operation, exact and optimal height and width, hide and show rows and
columns, F4 for reference type, and smart selections (row, column, data area,
visible only, unprotected only, array, all sheets).
**Structure**: almost everything rests on pieces already written — `SheetFill`, `FormulaTranslator`,
`SheetSnapshot`, the command registry. It is the block with the best effort-to-result
ratio, and it is what makes Ledger look like a real spreadsheet instead of a
demo.
**The known trap**: *repeat* is not "re-run the command": it is re-run **on the current
selection**. It must be modelled as a repeatable operation that the command declares, otherwise every
command will have to remember to support it and half will not.
**Finished when**: you can work an hour in Ledger without looking for a gesture and not finding it.

### B6 — Filters, data ranges, and analyses without a dialog (12 commands)
**Content**: autofilter with the drop-down button in headers, standard filter,
advanced filter, hide and reset the autofilter; database ranges (define,
select, refresh); data form; shuffle; **and the missing dialogs for consolidation and
multiple operations**, whose computation is already written and tested.
**Why together**: they are all cases of the same gap — the engine exists, the road to
reach it does not. Closing them together also closes the habit of stopping at the engine.
**Finished when**: a table is filtered without writing code, and the thirteen statistical
procedures, consolidation and multiple operations are all reachable from the menus.

### B7 — The two big dialogs (23 commands)
**Content**: **Format Cells** (number, font, effects, alignment with vertical and
justified, borders, background, protection) and **Insert Function** (list by category of the
534 functions, named arguments, description, result preview), plus the function list
and the navigator.
**Why it matters**: half of the work already done **is not visible**. The 534 functions exist and there is
no place to discover them; the complete number formats exist and are reachable only with
quick commands.
**Structure**: a more serious tabbed-dialog framework than `FormPromptView` is needed, which was
born for five fields. It must be done here, because from here on every block asks for one.
**Finished when**: from *Format Cells* every property the model can carry is reachable, and
from *Insert Function* one writes a `VLOOKUP` without knowing the argument order.

### B8 — Defined names (6 commands)
**Content**: define, manage, insert into formula, create from titles, labels.
**Structure**: the model already has them; missing are the dialogs, use in the parser as references and
saving in ODF (`table:named-expressions` — in XLSX they are already there).
**Finished when**: a name defined in the reference suite is read, used in a formula and goes
back.

### B9 — View and printing (27 commands)
**Content**: show formulas, value highlighting, row and column highlighting, grid and
headers, hidden-rows indicator, **page break mode** with
manual breaks, **print preview**, full screen, formula and status bars,
zoom at fixed percentages and fit to page.
**Structure**: print preview is not a new view: `IPageCanvas` already exists and was born
for this — the same pagination that produces the PDF, painted on screen.
**Finished when**: you see where pages will fall before printing, and you move them.

### B10 — Cells, sheets and links (16 commands)
**Content**: insert and delete cells **with shifting** (right, down, entire row,
entire column), move or copy a sheet **to another document**, sheet from file,
links to external sheets, go to sheet, auto outline, remove outline,
show and hide individual comments, delete all comments, sheet events,
right-to-left sheet.
**The trap**: shifting cells is a formula rewrite like inserting
rows, but **partial**: `StructureRewriter` must be extended, not duplicated.
**Finished when**: inserting cells shifting down leaves the formulas right, and ⌘Z restores
everything.

### B11 — Calculation and formula auditing (10 commands)
**Content**: recalculate, hard recalculate, toggleable autocalculate, convert formula
to value, detective fill mode, refresh and selectively remove arrows,
autorefresh, mark invalid data.
**Structure**: the engine already does everything; it only needs exposing. Manual calculation requires a
switch in the engine and the "sheet needs recalculation" mark in the status bar.
**Finished when**: with autocalculate off a huge sheet stays usable, and F9
recalculates.

### B12 — Assisted writing (7 commands)
**Content**: autocompletion while typing, autocorrect,
spelling with dictionaries, thesaurus, hyphenation, text language.
**Structure**: it is a subsystem, not a command. `PulseOffice.Text.Linguistics` with
`ISpellChecker` and Hunspell dictionaries; the library must be **fully managed** like
everything else (no native binaries), and the chosen one must be verified by reflection
on the assembly before adopting it, as was done for the scripting engines.
**The internal priority**: **AutoInput** comes before spelling and costs a penny: it is
what makes typing a column of repeating texts bearable.
**Finished when**: typing "Mi" in a column that contains "Milano" completes the rest, and
misspelled words are underlined in the document language.

### B13 — Styles and themes (7 commands)
**Content**: Accent 1/2/3 styles, update style from example, table styles
(AutoFormat), sheet themes.
**Structure**: here the debt already declared is also closed — the **name→cell link**, which in
ODF is lost and must be preserved in OOXML (`cellStyleXfs`).
**Finished when**: changing a named style changes all the cells that carry it, and the name
survives saving to XLSX.

### B14 — Sparklines (1 command, one family)
**Content**: in-cell charts, in groups, with their axes and colours.
**Structure**: they are not anchored objects, they are **cell properties**, and must be drawn
by the grid. Persistence: `calcext:sparklines` in ODF, `x14` extension in OOXML.
**Finished when**: a reference-spreadsheet file with sparklines opens showing them, and ours go back to
the reference spreadsheet.

### B15 — Tables and complete pivot (3 commands)
**Content**: named ranges with headers and style (Excel "tables"), calculated
field of the pivot, delete pivot.
**Structure**: in OOXML they are `tableParts` with their part; in ODF they are `table:database-range`.
Structured references (`Table1[Column]`) are a new syntax in the parser: whether to do it must be decided
now, because it touches the lexer.
**Finished when**: an Excel table opens as a table, not as formatted cells.

### B16 — Text, fields and links (18 commands)
**Content**: hyperlink, text box, symbols, current date and time, fields
(sheet name, document title, date), formatting marks, fontwork, QR code.
**Finished when**: a link in someone else's sheet opens on click and survives the round trip.

### B17 — Advanced drawing (20 commands)
**Content**: Bézier curves, polygons, freehand lines, callout shapes, symbol and
flowchart shapes, flip, enter and exit group, point editing, object
name, anchoring (to cell, to page), bring to front and send to back relative to the grid.
**Structure**: `ShapeOutline` already supports everything; missing are the interactive drawing tools and
the point editor, which is the real piece.
**Finished when**: you draw a curve, edit it point by point and save it in both
formats.

### B18 — Advanced external data (4 commands)
**Content**: XML source, data streams, data provider and its refresh.
**Structure**: it builds on what was just done — connections, keychain,
`QueryBinding`, `DataTableObject`. **Here `WEBSERVICE` and `FILTERXML` are unlocked**, which
wait for network access with its permissions.
**Finished when**: an XML mapped onto a range refreshes like a query.

### B19 — Track changes and protection (7 commands)
**Content**: record changes, show, manage, comment, protect recording;
document structure protection; read-only mode.
**Structure**: tracked changes are a model of their own (who, when, value before and after) that ODF
saves in `table:tracked-changes`. It is the most "serious application" block and the least urgent.

### B20 — Macros, forms and controls (36 commands)
**Content**: the script engines already decided (Jint, IronPython, CPython) with sandbox and IDE, the
macro recorder, macro signing; and the twenty form controls with their
properties.
**Honesty note**: it is the biggest block of the census and the least "spreadsheet". Form
controls belong more to a database than to a sheet, and stay at the end: they will
be done, but after everything else works.

### B21 — Digital signature (3 commands)
Already queued in the general plan: ODF signing and verification (OASIS XML-DSig) and OOXML, certificate
chain, timestamp, status-bar indicator, link with signed macros.

### B22 — Data sources as a panel
The data-sources window found in other office suites, which with our connection model is by now
a panel over things that already exist.

## How long it takes, and what can be done together

Blocks 1-4 are independent of each other and go first. 5 and 6 can proceed in
parallel because they touch different parts. 7 goes before 8, 13 and 15, because they all need
dialogs. 12 and 20 are subsystems and must not be mixed with anything else. 14, 15, 17 and 19
add new families and go after the rest is solid — otherwise features are added
to a framework that then changes.

## What stays out, and why it is a decision and not an oversight

53 commands are marked `escluso` in the map, each with its written reason: scanner
acquisition, sending by mail and Bluetooth, redaction, Asian conversions, extensions and
toolbar customisation of the reference suite, remote archives and document management, OLE
objects, help and donations. They are in the map, not hidden: changing one's mind on one of them is a
row, not archaeology.

## The other two modules

The same census, done on the other two menu bars, gives the measure of the work to come:

| Module | Menu commands |
|---|---|
| Spreadsheet (reference) | 485 |
| Word processor (reference) | 554 |
| Presentations (reference) | 434 |

When it is Deck's and Folio's turn, the first thing to do is their map, with the same
tool and the same rule: **measure first, then promise**. It is worth noting how much
of this plan already concerns them: document properties, raster images, spelling, text and
fields, links, advanced drawing, track changes, signing and macros are common to the three modules. Done
here, there they will be interface and not engine.

## Status at 23 September 2026

Closed: B1 merged cells, B2 CSV, B3 raster images, B4 properties, B5 everyday gestures,
B6 filters and analyses without a dialog, B7 the two big dialogs, B8 defined names, B9 view and
print preview, B10 cells and sheets, B11 calculation, B12 AutoInput, B13 styles, B14 sparklines,
B15 data areas, B16 fields and links, B17 (the non-interactive part), B19 track changes,
B20 macros with two engines.

What remains are **subsystems, not commands**:

1. **Forms and controls** (33 items) — twenty controls with properties, tab order,
   design mode and data binding. It is more a small form generator than a
   spreadsheet, and that is why it is at the end.
2. **Interactive drawing** (21 items) — Bézier curves, polygons and freehand lines have the
   model (`ShapeOutline` already supports everything) but not the **tools**: drawing mode,
   the point editor and the control handles are needed. It is interaction work, not
   geometry.
3. **Spelling and language** (8 items) — Hunspell dictionaries, per-cell language, autocorrect,
   thesaurus, hyphenation. The library must be fully managed, like everything
   else, and must be verified by reflection before adopting it.
4. **Digital signature** (4 items) — block B21 remains as described above: XML-DSig for ODF,
   OOXML signatures, certificate chain and timestamp. It is not hard piece by piece, it is long, and
   getting it wrong means saying "signed" about something that is not.

Plus a queue of minor items: symbols, fontwork, QR, gallery, navigator, page break
mode, selecting several sheets together (which requires a non-rectangular selection, i.e. a
change to the selection model), links to external files.
