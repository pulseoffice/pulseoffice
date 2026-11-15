# Round 141 (part A): Ledger and the shell on Linux, live

Second audit round on Linux (Fedora, Xvfb display), the first with live clicks and keys: the Linux counterpart of
`uiprobe` drives the app through `xdotool` on a private virtual display with a throwaway data folder.

## Method

- App built from this branch and started with the Linux probe; every scenario followed by screenshots read back and
  by the app log.
- Scenario: a household budget in a new Ledger sheet (text, decimal input with the Italian comma, `=B2+C2` and
  `=SOMMA(...)` formulas), bold header, currency format, a column chart from the palette, undo/redo chain, Save As ODS
  and XLSX through the system file dialog, the XLSX reopened and compared with the original, the command palette,
  closing a modified tab (Esc, Tab, buttons of the unsaved-changes modal).
- The saved XLSX unpacked and its parts read (`sheet1.xml`, `styles.xml`, chart and drawing parts).

## Probe notes (not app defects)

- The private D-Bus session of the probe starts the GNOME portal, whose file chooser never appears without
  GNOME Shell: Save As and Open then wait forever. Setting `XDG_CURRENT_DESKTOP` to a non-GNOME value for the app makes
  the portal use its GTK back end, which shows the dialog on the virtual display. The probe should do this itself.
- With no window manager the GTK dialog does not get keyboard focus; `xdotool windowfocus` on the dialog is needed,
  otherwise keys reach the main window (they did once, typing a path into three cells, which then showed in the saved
  file).
- Two probe sessions sharing one state folder overwrite each other's pid files: each session needs its own
  `UIPROBE_STATE`.

## Coverage

| Area | Outcome |
|---|---|
| Start screen, new Ledger document | clean |
| Cell entry, Tab/Enter navigation, formulas, decimal comma | clean |
| Bold, currency format, column chart from the palette | clean |
| Undo/redo chain with status bar message | clean |
| Save As ODS and XLSX, reopen XLSX (values, formats, chart) | XLSX default format defect, fixed |
| Command palette search | ranking defect and missing keywords, fixed |
| Unsaved-changes modal: Esc, Tab focus ring, buttons | clean |
| App log at startup | warning about an icon-less ribbon command, fixed |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `XlsxStyleMapper`, `XlsxWriter` | The first `cellXfs` entry was the style of whichever cell was written first. A budget starting with a bold header made xf 0 bold, and xf 0 is what every cell without a style attribute uses, including cells typed later in another spreadsheet application. | The writer declares the default style as the first cell format (`DeclareDefaultFormat`, which refuses to run after any other format). Test: `XlsxDefaultCellFormatTests`, red before. |
| 2 | medium | `CommandScorer` (palette search) | Matches were plain substrings, so "grafico" ranked spell check first (its keyword "ortografico" contains the letters) above every chart command whose menu trail holds the word. | Every field scores first at word starts; substring matches inside words score below any word-start match. Test in `PaletteSearchTests`, red before. |
| 3 | minor | `DeckIcons` | `deck.layouts.gallery`, a ribbon command reachable from the palette since round 140, had no icon; the app logged a warning at each start. | Icon added; the composition test now fails for any ribbon command without an icon (red before). |
| 4 | minor | settings command keywords | Searching the palette for theme, dark theme or language found nothing, although they are chosen in the settings. | Keywords added in the five languages. |

## Open items

- The XLSX writer still builds its style mapper and helpers with `new` inside `Write` (rule 1); left for a
  refactoring round, together with the other OpenXml writers.
- Not exercised live this round for lack of time: conditional formatting dialog, pivot table, filters and sort,
  dark theme and language switch in the settings, recovery after a crash, recent files list after reopening.
- The Linux probe changes in the notes above live outside this branch.
