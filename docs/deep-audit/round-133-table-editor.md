# Round 133: the table editor

Scope: the four table editor leftovers of round 128 (docs/deep-audit/round-128-deck-tables-2.md, "Remaining limits"):
no virtualisation, single-line cells, no header selection, banding that ignored the table's setting. The editor
(`PulseOffice.Data.UI`) is the shared component that edits data tables; today Deck opens it from its table commands.

## Structure

The old `TableEditorView` built one bordered `TextBox` per cell inside a `Grid` in a `ScrollViewer`, and kept its state
in the view. It is now split by responsibility:

| Type | File | Role |
|---|---|---|
| `TableEditorSheet` | src/PulseOffice.Data.UI/TableEditorSheet.cs | Avalonia-free state: headers, rows, merges, formats, selection (header row included as row -1), navigation over merges, merge/split, rows and columns, vertical alignment, undo/redo, `Build` |
| `TableEditHistory`, `CellTextStep`, `StructureStep` | src/PulseOffice.Data.UI/TableEditHistory.cs | Command pattern history (64 steps): text edits store only the changed cells, structural edits a memento |
| `TableMergeIndex` | src/PulseOffice.Data.UI/TableMergeIndex.cs | Origins, owners and covered cells of the merges (header merges kept to one row), vertical alignment lookup |
| `ICellLineEstimator` / `CellLineEstimator` | src/PulseOffice.Data.UI/TableEditorLayout.cs | Line count of a cell: paragraphs plus estimated wrapping, so 100,000 cells are sized without text measurement |
| `TableEditorLayout` | src/PulseOffice.Data.UI/TableEditorLayout.cs | Row heights following content (capped at 12 lines), prefix sums of row tops, binary-search hit testing, merged cell boxes; a text edit recomputes only its row |
| `ITableEditorPaletteSource` / `TableEditorPalette` | src/PulseOffice.Data.UI/TableEditorPalette.cs | Theme brushes resolved from the host; header, selection and chrome fills are opaque blends so frozen areas hide what scrolls under them |
| `TableCellPresenter` | src/PulseOffice.Data.UI/TableCellPresenter.cs | Recyclable cell: a border with a wrapping text block |
| `TableGridPanel` | src/PulseOffice.Data.UI/TableGridPanel.cs | The virtualised grid: realises only the visible cells from a pool, recycled on scroll; frozen column letters, header row and row numbers; one editing `TextBox` over the active cell; mouse, wheel and keyboard |
| `TableEditorView` | src/PulseOffice.Data.UI/TableEditorView.cs | Dialog shell: the panel with its own scroll bars, the toolbar (rows, columns, cells, alignment, undo/redo), footer |

## Behaviour

- Navigation mode: arrows (Shift extends), Page Up/Down, Tab/Shift+Tab, Ctrl/Cmd+A, Ctrl/Cmd+Z, Ctrl/Cmd+Y or
  Ctrl/Cmd+Shift+Z, Delete/Backspace clears the selection, Enter or F2 or a double-click edits, typing starts editing
  with the typed text. Arrows step over merged cells and land on their origin.
- Edit mode: Enter inserts a line break, Ctrl+Enter (Cmd+Enter on macOS) confirms and moves down, Tab confirms and
  moves on, Escape cancels, clicking elsewhere confirms. Line breaks are stored as `\n` (Windows line ends are
  normalised).
- Header cells are selectable (click, Shift-click, drag, the row-number gutter of the header selects the whole header);
  header cells merge into one-row header merges and split again. A selection that mixes header and body cannot be
  merged.
- Banded rows follow `DataTableStyle.BandedRows`; the header row is emphasised (accent tint, semibold, accent rule)
  only when `DataTableStyle.HeaderRow` is on, otherwise it shows as plain muted column names. The model has no
  first-column or last-row emphasis, so there is nothing more to follow.
- Theme changes repaint all realised cells.

## Defects

| Severity | Where (before) | Defect | Test |
|---|---|---|---|
| Serious | src/PulseOffice.Data.UI/TableEditorView.cs:163-207 | One text box per cell, no virtualisation: 100,000 cells took 34.6 s to open headless | Round133TableEditorTimingTests, Round133TableEditorTests.Only_the_visible_cells_of_a_large_table_are_realised |
| Medium | src/PulseOffice.Data.UI/TableEditorView.cs:322-333 | Single-line cells: several paragraphs on one line, row height fixed | Round133TableEditorTests.A_cell_with_two_paragraphs_shows_them_on_two_lines_and_its_row_grows |
| Medium | src/PulseOffice.Data.UI/TableEditorView.cs:190-197, 338 | Header not selectable: a header merge read from a file could not be split | Round133TableEditorTests.A_header_merge_read_from_a_file_can_be_selected_and_split |
| Minor | src/PulseOffice.Data.UI/TableEditorView.cs:341 | Banding ignored `BandedRows` | Round133TableEditorTests.Rows_are_not_banded_when_the_table_turns_banding_off |

Each test was run against the old editor first and failed (6,028 realised controls for 2,000 rows; 23 px vs 23 px rows;
split disabled; banded background with banding off; 34,574 ms to open).

## Timing

Opening a 10,000 x 10 table (100,000 cells) in a headless window, constructor to first layout, no delays:
34,574 ms before, 51 ms after.

## Tests

- tests/PulseOffice.Presentations.UI.Tests/Round133TableEditorTests.cs (behaviour, five tests)
- tests/PulseOffice.Presentations.UI.Tests/Round133TableEditorSheetTests.cs (sheet, history, estimator, editing, recycling)
- tests/PulseOffice.Presentations.UI.Tests/Round133TableEditorTimingTests.cs (the measurement, bound at 3 s)
- tests/PulseOffice.Presentations.UI.Tests/Round128TableEditorTests.cs adapted to the presenters.

## Remaining limits

- Row heights come from an estimate of wrapping (average glyph width), not from measured text: a cell full of wide
  glyphs may wrap onto more lines than its row shows; explicit line breaks are always counted. Rows are capped at 12
  lines; the editing box grows to its full text.
- Rows and columns are still added and removed only at the end, as before.

## To try live

- Open the table editor on a Deck table imported with many rows: it opens at once; scroll with wheel, Shift+wheel and
  the scroll bars; column letters, header row and row numbers stay in place.
- Type into a cell, press Enter twice, confirm with Ctrl+Enter / Cmd+Enter: the row grows and the slide shows the lines.
- Open a PPTX with a merged header: click the header, Split; select two header cells, Merge.
- Turn banded rows off on a table, open the editor: no bands. Turn the header row off: the header shows plain.
- Switch theme while the editor is open; undo and redo from the toolbar and with the keyboard.
