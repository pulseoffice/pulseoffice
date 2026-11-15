# Round 143: the areas left by round 141, live on Linux

Third live round on Linux (Fedora, Xvfb display `:143`, private probe state and data folders). It covers what rounds
141 and 142 left open: Ledger conditional formatting, pivot table, filters and sort; dark theme and interface
language; crash recovery; recent files; ODT and ODP save and reopen; PDF export from each module.

## Method

- Debug build of `src/PulseOffice.Desktop` driven with `tools/uiprobe-linux`; every step captured and read back; the
  files written were unpacked (`content.xml`) and the PDFs read with `pdfinfo`, `pdffonts`, `pdftotext` and
  `pdftoppm`.
- The code behind each area read for the cause of what was seen (command enablement, quick sort planner, pivot
  creation, cell text composer, autosave and recovery, recent documents, PDF font set).
- Each defect: a regression test seen red, the fix, the test and its project green (`tools/run-tests.sh` for the
  Desktop, Sheets.UI, Formats.Pdf and Sheets.Printing projects), one commit.
- Probe changes: the app now runs with `XDG_CURRENT_DESKTOP` set to a non-GNOME value, so the GTK file chooser
  appears on the virtual display (round 141's note), and `UIPROBE_DATA` reuses a data folder, which a restart after a
  simulated crash needs.

## Coverage

| Area | Outcome |
|---|---|
| Quick sort from the ribbon on the cell cursor | defect 1 |
| Autofilter: toggle, value list, filter, sort inside the filtered range | clean |
| Clear filters with the cursor in the table | defect 2 |
| Pivot table dialog (fields, preview, create) | defect 3 (result sheet not shown) |
| Conditional formatting: value rule with validation, data bars, rule follows edits | clean |
| Dark theme from the settings, live | clean |
| Interface language switch to English, live; English formula separators | clean |
| Cell text size on screen against the PDF and the cell editor | defect 4 |
| Autosave snapshot after one minute, SIGKILL, restart, recovery prompt, restore | clean |
| Recent files on the start screen and their reopening | clean |
| ODS, ODT and ODP save through the file chooser and reopen from the recent list | clean |
| PDF export: Ledger (two sheets, conditional formats), Folio, Deck | defect 5 in Deck |

## Defects

| # | Severity | Where | Defect | Fix |
|---|---|---|---|---|
| 1 | medium | `LedgerEditDataCommands.Data` | Sort ascending/descending required a range selection, so with only the cursor in a data region the ribbon buttons and Ctrl+Shift+A were disabled, although `QuickSortPlanner` expands the cursor to the current region (and detects the header). | The commands accept a single cell. Test `Ordinare_dal_solo_cursore_ordina_la_regione_corrente`, red before. |
| 2 | medium | `LedgerFilterCommands` | Clear filters ignores the selection but required a range, so it stayed disabled with the cursor in the filtered table. | Accepts a single cell. Test `Rimuovere_i_filtri_dal_solo_cursore`, red before. |
| 3 | minor | `LedgerPivotCommands.CreateAsync` | The pivot table went to a new sheet while the source sheet stayed active; only the notice said where. | The destination sheet is selected after a successful creation. Test `The_new_pivot_sheet_becomes_the_active_sheet`, red before. |
| 4 | medium | `CellTextComposer` | Font sizes in points were passed to Avalonia as device-independent pixels, so cell text was drawn at three quarters of its size while columns and rows use 96 pixels per inch: the screen did not match the PDF (a pivot "Totale generale" label that fitted on screen was cut with an ellipsis on paper), the in-cell editor showed the text larger than the cell, and automatic row heights and column widths were measured too small. | Points are converted with `CellBoxMetrics.PixelsPerPoint` (96/72) in the one composer every painter and measure uses. Test `Il_testo_delle_celle_misura_i_punti_come_la_carta` (a 72 pt line measured 87.9 px), red before. |
| 5 | medium | `EmbeddedFontResource`, `PdfFontSet` | An embedded font resource reported the weight and slant the text asked for, not those of the face it embeds, so synthetic bold and slant never applied. Fedora installs Noto Sans as a variable font whose default instance is regular: Deck's bold title (Inter, not installed, falling back to Noto Sans) was exported in regular weight. | The resource takes bold and italic from its face. Test `BoldTextWithOnlyARegularFaceIsEmboldened`, red before. |

## Open items (minor, left)

- Searching the palette for "tema scuro" opens the settings on General, not on Appearance.
- After a language switch the ribbon returns to its first tab.
- "Email as PDF" and "Sign Existing PDF" have no icon in the command palette (the icon test covers ribbon commands
  only).
- Outline grouping commands sit under Data › Pivot table in the menus; the specifications keep them in their own
  group and outline submenu.
- The pivot dialog preview names the value column after the field ("Vendite") while the result says "Somma di".
- A data bar for the lowest value has zero length; the specifications draw a short minimum bar.
- An export saved under a name with another format's extension keeps that name (an EPUB written as `.pdf`).
- The start screen recentres its content as the recent list grows, so a click aimed at the first entry can land on a
  different one after a save; not a defect, but the probe must take a fresh capture before clicking.

## Not verified here

- Sorting a filtered range moves the hidden rows too; the two specifications differ on this, so it was left.
- Popups opened by click on the virtual display lose clicks without a window manager; choices were made with the
  keyboard. The white corners of a dark-theme drop-down are the same missing compositor.
- Heading shortcuts with Ctrl+Alt typed a digit through `xdotool` (round 141 reached them); not investigated further.
- Rows still use the default 15 pt height after defect 4; text of 10 and 11 pt fits, larger default fonts were not
  tried live.
