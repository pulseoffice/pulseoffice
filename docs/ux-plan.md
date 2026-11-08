# UX plan: the surfaces left behind

Survey of 2026-10-22, requested by the user: "a sweeping check of all 3 apps" to find the modals and features that are
"too bare or old-fashioned" and bring them up to the level of tables, charts, Fontwork, the Deck theme, the macro IDE
and the styles panel. This document is **only the plan**: no code has been touched.

## Method

- **Code.** A script extracted every `ShowAsync`/`AskAsync` call of `IDialogService` and of the prompt services
  (`forms`, `tabbed`, `prompt`, `gallery`, …) from all of `src/`: **331 opening points, 298 distinct surfaces** (file ×
  title), of which **211 built with `FormPromptView`/`TabbedForms`**, that is columns of generic fields
  (`TextField`, `NumberField`, `ChoiceField`, `CheckField`). On top of these come **31 dedicated views** (shape
  gallery, Fontwork, table, theme, chart, symbols, fonts, IDE, print, settings…) and **about 25 non-modal surfaces**:
  side panel (styles, navigator, review, Deck transitions and animations), ribbon popups (colours, borders, table grid,
  band galleries), command palette, filter menu, status bar, notifications.
- **Live**, by sampling: own instance with a throwaway `PULSEOFFICE_DATA_HOME`, foreground verified by pid before every
  key, dialogs opened from the palette (⇧⌘P), captures with `windowshot --pid` in
  `/private/tmp/claude-501/-Volumes-Dati-develop-quadoffice/bddfccb4-bf7c-470e-b056-751ec31a2231/scratchpad/ux-inventario/`
  (30 captures: 11 Ledger, 11 Folio, 8 Deck, palette and startup). Light theme; the dark theme must be reviewed block
  by block.

## The reference level

The surfaces already in good shape, from which the language is taken: `FontworkGalleryView` (gallery with thumbnails
drawing the real data, large preview, clickable swatches, full keyboard support), `TableInsertView` (direct grid +
synchronized fields, gallery, preview), Deck's `ThemeDialogView` (gallery on the left, preview and options on the
right), `ChartEditor`/`ChartPickerView`, `MacroEditorView`, `ShapeStyleView` (real tabs + preview pane),
`StylesPanel` (icon bar, dropdown with icon, search, list with preview), `PageTabStrip`, `ColorFieldControl`,
`SymbolPickerView`, `FontBrowserView`, `PrintPromptView` (page preview).

Criteria: the control follows the data (measurement → field with unit and dragging; visual choice → gallery or
swatches; alignment → icon group; border → clickable diagram); live preview where the effect cannot be guessed from the
name; real tabs only beyond about ten fields; help text of one line at most, the rest in a tooltip; two columns when
there are many fields (settings on the left, preview on the right); keyboard (Enter, Esc, arrows in galleries, double
click confirms); theme tokens in both themes; **one component, three modules**.

## Cross-cutting defects found

1. **The "flat form" is the rule.** 211 surfaces out of 298 are a column of full-width labels and fields (capture
   `L-pagina.png`: Ledger Page Setup, 16 fields in a row with scrolling, no preview).
2. **Bare numeric fields.** `NumberField` has `Unit`, but 9 dialogs out of 10 in Ledger and Deck do not use it
   (`Durata (s)`, `Margine sinistro (mm)` with the unit in the label); no field has dragging, a slider or presets; the
   box is narrow (120 px) with tiny arrows and the value right-aligned far from the label.
3. **Dropdowns where galleries are needed**: Deck transitions and effects, list type and level, Folio list style
   ("Numbering 123" in a dropdown), sparkline chart type, number format, cell style, Ledger theme, fields, index type,
   Ledger borders (seven "Left/Top/…" dropdowns with style names).
4. **No preview** in Character, Paragraph, Page (Folio), Format Cells (Ledger), Conditional Formatting, Transition,
   Header and Footer, Ledger Page Setup, Lists.
5. **Verbose help texts** at the top of 140 dialogs ("In headers: &P page, &N pages, &D date…", "Structure protection
   prevents adding…"): often two lines that push the content down.
6. **Focused dropdown filled with the accent** (`L-convalida.png`, `F-elenchi.png`, `D-tema.png`): the solid green,
   blue or red background looks like an error or selection state, not focus. A `PulseTheme` defect, one for all.
7. **Single-dropdown dialogs** (More Fields, Index or Table of Contents, Paste Special, Apply Master, Which style?): a
   whole modal for a choice that in Word/Excel is a gallery or an icon menu (`F-campi.png`, `F-indice.png`).
8. **Dialogs with one empty tab**: Folio's Character takes 620 × 640 for six fields, half of the pane is white
   (`F-carattere.png`); Validation with "Any value" is an empty pane (`L-convalida.png`).
9. **Inconsistencies between modules**: Character and Paragraph exist three times (Folio with tabs, Deck flat in
   `DeckTextDialogCommands`, Ledger inside Format Cells) with different fields and order; Page Setup twice (Folio with
   tabs in cm, Ledger flat in mm); Lists twice (Folio and Deck); Thesaurus, Language, Hyphenation, Spelling repeated per
   module with different forms.
10. **Bare command palette** (`x-palette.png`): no icons, no shortcuts, no module, solid grey selection, no "recent"
    entry; and the search does not find synonyms ("Misura della diapositiva" → no results, `D-misura.png`).

## Shared components to build first

In order of payoff: each one unlocks dozens of surfaces. They go in `PulseOffice.UI.Controls` (folders indicated), with
the Avalonia-free model alongside when there is state, and wired into the three modules (rule 10).

| # | Component | Where | Used for | Notes |
|---|---|---|---|---|
| C1 | **`MeasureField`**: field with a visible and convertible unit (cm, mm, pt, in, %, s, °), horizontal dragging on the label, mouse wheel, ⇧ for ×10 step, optional presets in a menu (0, 0.5, 1 cm…), optional slider (`ShowSlider`) | `Fields/` | ~70 dialogs: margins, indents, spacing, durations, rotations, sizes | `NumberField` uses it by itself when it has `Unit`: existing dialogs improve without touching them. Unit of measure from the preferences. |
| C2 | **`ThumbnailGallery<T>`**: generic tiled gallery with a thumbnail drawn by an `IThumbnailPainter<T>`, caption, groups with headers, optional search, arrows/Home/End, Enter, double click | `Galleries/` | transitions, animations, lists, number formats, cell styles, sparklines, field and index types, templates, layouts, masters | Extracted from the three existing galleries (Fontwork, table styles, themes), which then use it. |
| C3 | **`LivePreviewPane`**: preview pane with a frame, checkerboard or page background, fit zoom, editable sample text, update on every change with a 60 ms debounce | `Preview/` | character, paragraph, page, cell format, conditional, transition, header and footer | `PagePreview` variant (page with margins, headers, columns, orientation) shared by Folio, Ledger and Deck. |
| C4 | **`TwoPaneDialogLayout`** + **`DialogSection`**: settings on the left in sections with a small uppercase title (like the Deck theme), preview on the right; two-column "label · control" rows instead of label above field | `Dialogs/` | all dialogs with more than six fields | `FormPromptView` and `TabbedForms` receive `Layout = TwoColumn` and `Preview = …`, so migration is incremental. |
| C5 | **`HelpTip`**: one-line subtitle + "?" icon with the long text in the tooltip | `Dialogs/` | 140 dialogs with verbose explanations | The `*.explanation` keys split into `*.summary` (one line) and `*.help`. |
| C6 | **`BorderEditor`**: clickable diagram of the cell/paragraph/page (sides, inner, diagonals), icon presets (none, outline, all, thick outside…), drawn line samples, colour with `ColorFieldControl`, width with `MeasureField` | `Borders/` | Format Cells borders, Folio paragraph/page/table borders and background, Deck table properties | The ribbon's `BorderGallerySlot` popup shares the icons. |
| C7 | **`FillEditor`**: none / solid / gradient (with stop editor) / pattern / image, with preview | `Fills/` | Format Cells background, paragraph and page background, slide background, shape fill | Extracted from the Fill tab of `ShapeStyleView`, which already does it but is not reusable. |
| C8 | **`IconChoiceGroup`**: exclusive icon choice (segments) with tooltip and accessible name | `Choices/` | alignments, orientation, series direction, transition direction, anchoring, line spacing | Replaces many 2–5 item dropdowns. |
| C9 | **`SwatchStrip`**: row of swatches (predefined styles, colour schemes, "Normal / Narrow / Wide" margin presets as in Word) | `Choices/` | margins, cell styles in Conditional Formatting, colour schemes, zoom presets | |
| C10 | **Focused dropdown style** in `PulseTheme.axaml`: accent border, neutral background | theme | all dropdowns | An hour of work, effect across the whole app: to be done immediately. |

## Using the components

**Block A closed on 2026-10-22.** The usage notes, with the APIs, sit next to this plan:

| Components | Notes |
|---|---|
| C1 `MeasureField`, C5 `HelpTip`, C8 `IconChoiceGroup`, C9 `SwatchStrip` | [ux/components-A1.md](ux/components-A1.md) |
| C2 `ThumbnailGallery<T>`, C3 `LivePreviewPane` and `PagePreview` | [ux/components-A2.md](ux/components-A2.md) |
| C4 `TwoPaneDialogLayout`, `DialogSection`, `DialogRow`; `FormPromptView`, `TabbedForms`, `ChoicePromptView` | [ux/components-A3.md](ux/components-A3.md) |
| C6 `BorderEditor`, C7 `FillEditor` | [ux/components-A4.md](ux/components-A4.md) |

Rules that emerged from the merge and the live test, valid for blocks B–G:

- The explanation of a `FormPromptView` goes through `HelpTip` (first sentence visible, the rest behind the "?"), placed
  in the `Header` slot of `TwoPaneDialogLayout`; `Explanation` remains as plain text for those using the layout alone.
- Numeric fields with a unit (`MeasureField` and the old `NumberBox`) show focus on the **whole frame** (class
  `measureFrame`, `:focus-within`), never on an inner ring around the number alone.
- Diagrams representing the document (the border diagram) are drawn on **white paper** even in the dark theme: on a
  dark background a black border disappears.
- `BorderEditor` sides are `ToggleButton.borderSide` toggles: surface with a border, on = soft accent with an accent
  border, like the ribbon tiles.
- The minimum dialog width is 400: at 360 the content was below the stacking threshold (340) and every small form ended
  up with labels above the fields. The `DialogRow` label is left-aligned (with `MaxWidth` and stretch alignment, when
  stacked it ended up centred).
- `TabbedForms` never opens a dialog narrower than its own row of tabs (Folio's Paragraph was cutting off "Drop Caps").
- `Tables/TableStyleGalleryView` (Table Styles of Ledger, Deck and Folio) is also on `ThumbnailGallery`, with the
  `TableLookPainter` shared with Insert Table; initial focus goes to the chosen tile.

## Work blocks

Seven blocks. **A** creates the components and must be done first (at least C1, C2, C3, C4, C10, with the API fixed);
the other six touch disjoint files and can run in parallel in different worktrees. Where a block needs a component of A
not yet ready, it uses it through its interface and does not duplicate it. Every block closes with headless tests, a
live test in light and dark theme in the modules involved, texts in the five languages, an entry in the log.

### Block A — Shared components and theme (prerequisite)

Files: `src/PulseOffice.UI.Controls/{Fields,Galleries,Preview,Dialogs,Borders,Fills,Choices}/` (new),
`FormPromptView.cs`, `TabbedForms.cs`, `TabbedDialogView.cs`, `FormFieldsPanel.cs`, `NumberBox.cs`, `NumberEntry.cs`,
`Theming/PulseTheme.axaml`, `Fontwork/FontworkGalleryView.cs`, `Tables/TableStyleGalleryView.cs` (only to move to C2).

| Item | Current state | What to do | Priority |
|---|---|---|---|
| C10 | **done** (2026-10-22): with focus or the dropdown open the field stays on the `PulseSurface` background, 1.5 accent border and accent arrow; no accent fill | — | — |
| C1–C9 | **done** (2026-10-22): see "Using the components" below | — | — |
| `FormPromptView` / `TabbedForms` | **done** (2026-10-22): two columns with `DialogRow`, `HelpTip` in the layout header, automatic `MeasureField`, `Preview`, sections, width according to content and never narrower than the row of tabs | — | — |
| `ChoicePromptView` | **done** (2026-10-22): option cards with description, Cancel in the footer, Enter on the default | — | — |
| `TextPromptView` | **done** (block G): `ITextInputRule` rules (required, name already used) with an inline message below the field | — | — |

### Block B — Folio: text formatting, shared with Deck

Files: `Documents.UI/Commands/{FolioCharacterDialog,FolioParagraphDialog,FolioPageDialog,FolioListCommands,FolioSectionCommands,FolioToolCommands}.cs`,
`Documents.UI/StyleCatalog/FolioStyleDialogs.cs`, `Documents.UI/Commands/FolioDialogCommands.cs`,
`Presentations.UI/DeckTextDialogCommands.cs`, new `UI.Controls/TextFormat/` (shared Character, Paragraph, Lists
dialogs).

| Item | Module | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|---|
| Character… | Folio, Deck | `FolioCharacterDialog.cs`, `DeckTextDialogCommands.cs` | Folio: three tabs with six fields per tab and half the pane empty; Deck: a different flat form | **A single shared `CharacterFormatView`**: on the left font with `FontPicker`, style (Regular/Bold/Italic as a list), size with presets; on the right effects (underline with line samples, strikethrough, small caps, superscript/subscript, colour and highlighter with `ColorFieldControl`); spacing and position in a second tab; **preview of the selected text** at the bottom, as in Word | C1, C3, C4, C8 | high |
| Paragraph… | Folio, Deck | `FolioParagraphDialog.cs`, `DeckTextDialogCommands.cs` | six tabs of stacked fields, 14 `NumberField` one below the other, no preview | Shared `ParagraphFormatView`: indents and spacing in two columns with `MeasureField` and line spacing presets (1 · 1.15 · 1.5 · 2) as swatches; alignment with `IconChoiceGroup`; **preview of paragraphs as grey lines** (Word's one); tabs as an editable list with a ruler; borders and background with `BorderEditor`/`FillEditor` | C1, C3, C4, C6, C7, C8 | high |
| Page Setup… | Folio | `FolioPageDialog.cs` | four tabs, 14 measurements in a column, no preview | `PageSetupView` with `PagePreview` on the right showing size, orientation, margins, header, columns and watermark; margin presets as swatches (Normal, Narrow, Moderate, Wide, Mirrored); orientation with two icons; columns as a gallery (1, 2, 3, left, right) | C1, C3, C4, C9, C6, C7 | high |
| Bullets and Numbering… | Folio, Deck | `FolioListCommands.cs`, `FolioStyleDialogs.cs`, `DeckTextDialogCommands.cs` | two dropdowns ("Numbering 123", "Level 1") and a checkbox | Gallery of drawn bullets and numberings (C2), level as a 1–9 list on the left with a multi-level preview on the right, customization (symbol with `SymbolPickerView`, image, format, indent with `MeasureField`) | C1, C2, C3 | high |
| Chapter Numbering… | Folio | `FolioListCommands.cs` | tabs of fields | Same view as lists in "heading levels" mode, preview with "1.1 Heading" | C2, C3 | medium |
| Style (modify / new) | Folio | `FolioStyleDialogs.cs` | tabs of fields; `New style` a single box | Reuses the shared Character/Paragraph as tabs; at the top name, based on, next style with an icon dropdown; preview | B itself | medium |
| Section…, Sections… | Folio | `FolioSectionCommands.cs` | three `forms` in sequence | One dialog with the list of sections on the left and properties (columns in a gallery, protection, hide) on the right | C2, C4 | medium |
| Line Numbering…, Page Numbers…, Comment Format…, Sort…, Phonetic Guide, Theme… | Folio | `FolioToolCommands.cs` | a flat form each | Page numbers as a gallery of positions (top/bottom × left/centre/right, as in Word); line numbering with a margin preview; the rest in `TwoPaneDialogLayout` | C1, C2, C4 | low |

### Block C — Folio: insertion, references, tables and objects

Files: `Documents.UI/Commands/{FolioFieldCommands,FolioIndexCommands,FolioInsertCommands,FolioBreakCommands,FolioTitlePageCommands,FolioTableDialogs,FolioObjectDialogs,FolioMailMergeCommands,FolioReviewCommands,FolioContentControls,FolioAutoTextCommands,FolioMasterCommands,FolioNavigationCommands,FolioExportCommands,FolioSendCommands}.cs`.

| Item | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|
| More Fields… (12 openings) | `FolioFieldCommands.cs` | chain of single-dropdown dialogs: type, then format, then value | **One three-column dialog** like the "Fields" dialog of the reference word processor and Word: categories (Document, References, Functions, Info, Variables, Database), field list, format; preview of the computed value; Enter inserts without closing | C4, C3 | high |
| Cross-reference… | `FolioFieldCommands.cs` | three dialogs in sequence | Same view as fields, "Cross-references" tab: targets (headings, bookmarks, captions) with search, reference format as a list with an example | C4 | high |
| Index or Table of Contents…, Index Entry…, Bibliography Entry/Database… | `FolioIndexCommands.cs` | a "Type" dropdown then tabs of fields | Gallery of drawn tables of contents (classic, modern, dotted, without numbers) as in Word, then tabs with preview; index entry as a non-modal panel that stays open while scrolling through the text (as the reference word processor does) | C2, C3 | high |
| Hyperlink…, Bookmark…, Comment… | `FolioInsertCommands.cs` | flat form | Hyperlink like Word's: on the left the type as icons (Web page, Place in document, File, Email), on the right the related fields; place in document as a tree of headings and bookmarks. **Shared with Deck and Ledger** (`LedgerInsertCommands`, `DeckInsertCommands`) in `UI.Controls/Links/` | C4, C8 | high |
| Table Properties…, Split Cells, Split Table, Row Height, Column Width, Sort, Formula, Convert text↔table | `FolioTableDialogs.cs` | 9 flat `forms` plus one tabbed | Properties: tabs with `BorderEditor`, `FillEditor`, table alignment as icons, widths in `MeasureField`; split cells with a preview grid; convert with a preview of the first split rows | C1, C6, C7, C8 | medium |
| Object Properties…, Name, Caption, Link Frames | `FolioObjectDialogs.cs` | tabbed + forms, 9 measurements | Position and size with `MeasureField` and aspect ratio lock; anchoring and text wrap as an **icon gallery** (In line, Square, Tight, Behind, In front, as in Word); caption with preview "Figure 1: …" | C1, C2, C8 | medium |
| Manual Break…, Envelope…, Title Page… | `FolioBreakCommands.cs`, `FolioTitlePageCommands.cs` | flat form | Break: icon choices (page, column, line, section); envelope with an envelope preview and sizes as swatches | C3, C8 | low |
| Mail Merge…, Insert Merge Field…, Links to External Files | `FolioMailMergeCommands.cs`, `FolioMasterCommands.cs` | two dropdowns and numbers | Step-by-step wizard with a preview of the merged document and navigation between records | C3 | medium |
| Comment/Protect/Reply/Edit Comment, Delete Author's Comments | `FolioReviewCommands.cs` | form with a single box | Replies and edits inline in the Review panel, not in a modal; protection with `HelpTip` | — | medium |
| Content Controls, AutoText, Go to Page, Export EPUB, Send | `FolioContentControls.cs`, `FolioAutoTextCommands.cs`, `FolioNavigationCommands.cs`, `FolioExportCommands.cs`, `FolioSendCommands.cs` | flat forms | AutoText as a list with a preview of the entry; Go to Page as a field in the status bar (non-modal); the rest with `TwoPaneDialogLayout` | C3, C4 | low |

### Block D — Ledger: cell format and styles, printing

Files: `Sheets.UI/{FormatCellsView,CellFormatDialog,LedgerFormatDialogCommands,LedgerConditionCommands,LedgerStyleCommands,LedgerFinalCommands,LedgerSparklineCommands,LedgerPrintCommands,LedgerRibbonCommands}.cs`, `UI.Controls/Rows/ConditionRowsEditor.cs`.

| Item | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|
| Format Cells… | `FormatCellsView.cs` | six tabs; Numbers is a list of tall rows with no example; Borders are seven dropdowns per side; Background a single colour | **Numbers** as in Excel: categories on the left, options (decimals with `MeasureField`, separator, negatives as red/parenthesis swatches, currency symbol) and **example of the active cell's value**; free code below. **Alignment**: horizontal/vertical `IconChoiceGroup`, orientation with a draggable dial (°). **Font** = shared view from block B. **Borders**: `BorderEditor`. **Background**: `FillEditor` with patterns. Cell preview always visible on the right | C1, C3, C6, C7, C8, B | high |
| Conditional Formatting | `LedgerConditionCommands.cs`, `Rows/ConditionRowsEditor.cs` | flat form: dropdowns, value, two colours in hexadecimal, a checkbox | **Rules manager** as in Excel: list of the range's rules with a preview of each, draggable order, New/Edit/Delete in `PanelActionBar`; rule editor with the type in a gallery (Value, Top/Bottom, Above average, Duplicates, Text, Date, **Data bars, Colour scales, Icon sets** — all with drawn thumbnails) and **style as swatches** ("Light red fill with dark red text"…) plus "Custom…" | C2, C3, C9, `PanelActionBar` | high |
| Page Setup | `LedgerPrintCommands.cs` | 16 fields in a column with scrolling, margins in mm in the label, &P &N codes to remember | Same `PageSetupView` as block B (page, margins, header and footer) with its own Sheet tab (print area with `RangeField`, titles, order, fit to N pages); **three-pane header editor with insert buttons** (Page, Pages, Date, File, Sheet) instead of codes; `PagePreview` | B, C1, C3, C9 | high |
| Cell Styles, New Style, Apply Style | `LedgerStyleCommands.cs` | dropdown / `TextPromptView` | Gallery of drawn cell styles (C2), like Excel's "Cell Styles" gallery; new style with preview | C2 | medium |
| Sparkline… | `LedgerSparklineCommands.cs` | two ranges + "Line" dropdown | Type as three thumbnails (line, columns, win/loss) drawn on the real data, colours and markers in a second section, preview | C2, C3 | medium |
| Document Theme, Quick Alignment/Indent, Fit Width, Object Anchoring, Pivot Calculated Field | `LedgerFinalCommands.cs` | flat forms | Theme like Deck's theme gallery (component to extract from `ThemeDialogView` to `UI.Controls/Themes/`); alignment with `IconChoiceGroup`; the rest with `MeasureField` and `HelpTip` | C1, C2, C8 | medium |

### Block E — Ledger: data, sheets, analysis and tools

Files: `Sheets.UI/{LedgerDataDialogCommands,LedgerToolCommands,LedgerPartialCommands,LedgerPasteCommands,LedgerNameCommands,LedgerSheetCommands,LedgerSheetExtraCommands,LedgerSheetToolsCommands,LedgerSelectionExtraCommands,LedgerTailCommands,LedgerAnalysisCommands,LedgerDatabaseRangeCommands,LedgerFillCommands,LedgerCellShiftCommands,LedgerCommentCommands,LedgerSpellingCommands,LedgerGalleryCommands,LedgerObjectCommands,LedgerFilterCommands,FilterMenuView,PivotPromptView,FunctionWizardView}.cs`, `Data.UI/*`.

| Item | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|
| Paste Special | `LedgerPasteCommands.cs` | form with dropdowns | As in Excel: option groups as icons (All, Formulas, Values, Formats, Comments, Validation), operation (None, +, −, ×, ÷), Skip blanks, Transpose; **plus the row of quick icons** from the ribbon paste menu | C8, C4 | high |
| Sort… | `LedgerToolCommands.cs` | flat form with ranges | **List of sort levels** (column, sort by value/colour, order with icon), Add/Copy/Delete level, "My data has headers"; column names from the headers | `PanelActionBar` | high |
| Data Validation | `LedgerToolCommands.cs` | three tabs, the first empty with "Any value" | Types as icon choices (Whole number, Decimal, List, Date, Time, Length, Custom); for List an item editor (`ListEditorControl`) with a preview of the dropdown; messages with a preview of the balloon | C8, C3 | high |
| Series… | `LedgerPartialCommands.cs` | four dropdowns and two fields | Direction and type with `IconChoiceGroup`, date unit visible only for "Date", **preview of the first generated values** | C1, C3, C8 | medium |
| Advanced Filter…, filter menu | `LedgerDataDialogCommands.cs`, `FilterMenuView.cs` | form; list-style filter menu | Advanced filter with conditions in rows (`ConditionRowsEditor`) and a count of passing rows; filter menu as in Excel: sort at the top, search, "Number/Text/Colour filters" in a submenu, values with counts | C3 | medium |
| Consolidate…, Multiple Operations…, Fill Sheets…, Define Labels… | `LedgerDataDialogCommands.cs`, `LedgerSheetExtraCommands.cs` | forms with ranges | List of source ranges with Add/Delete, function in a dropdown with icon, preview of the result | `PanelActionBar`, C3 | medium |
| Define a Name, Manage Names | `LedgerNameCommands.cs` | form; plain list | Manager as in Excel: Name · Value · Reference · Scope table, filter, inline editing, `RangeField` | `PanelListFrame`, `PanelSearchBox` | medium |
| Move/Copy Sheet, Show Sheets, Tab Colour, Protect Sheet/Structure, Select Sheets, Go to Sheet, Rename | `LedgerPartialCommands.cs`, `LedgerSheetCommands.cs`, `LedgerSheetToolsCommands.cs`, `LedgerSelectionExtraCommands.cs` | flat forms and `TextPromptView` | Move/copy with a list of sheets and a position indicator; tab colour as a swatch popup (non-modal) on the tab; protection with a checkbox list of permissions and `HelpTip`; inline rename on the tab | C9 | medium |
| Random Numbers, Shuffle, Data Form, Scenarios, Goal Seek/Solver, Statistics | `LedgerSheetExtraCommands.cs`, `LedgerAnalysisCommands.cs` | flat forms with 6–7 numbers | Distribution with a thumbnail of the curve, parameters with `MeasureField`; data form as a record card with navigation; scenarios in a list with comparison | C1, C2, C3 | low |
| External data, streams, XML source, providers | `LedgerTailCommands.cs`, `Data.UI/*` | flat forms, one with a tabular preview | Step-by-step wizard (source → preview → destination) with the existing `TabularPreviewView` in every step | C3 | low |
| Insert/Delete Cells, Row Height, Column Width | `LedgerCellShiftCommands.cs`, `LedgerFillCommands.cs` | dropdown choices, bare number | Shift with four icons (as in Excel); measurements with `MeasureField` in the preferred unit | C1, C8 | low |
| Insert Function, Pivot | `FunctionWizardView.cs`, `PivotPromptView.cs` | dedicated views, already decent | Touch-ups: categories with icons, live example of the result; pivot with draggable zones (Rows, Columns, Values, Filters) as in Excel | C3 | medium |

### Block F — Deck: transitions, animations, slide show and slides

Files: `Presentations.UI/{DeckMotionCommands,DeckView,DeckPanelCommands,DeckCommands,DeckSlideSetupCommands,DeckEditCommands,DeckSlideCommands,DeckMasterCommands,DeckExtraInsertCommands,DeckExportCommands,DeckFieldCommands,DeckNavigationCommands,DeckObjectCommands,DeckStyleCommands,DeckAnimatedImageCommands,DeckFormFieldCommands,DeckTextFrameCommands,CustomShowsView}.cs`, `Presentations.UI/ObjectDialogs/*`.

| Item | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|
| Transition (dialog and panel) | `DeckMotionCommands.cs`, `DeckView.cs` | dialog: Effect and Direction dropdowns, bare duration in seconds, sound as a file path; panel: two dropdowns | **Transition gallery** with thumbnails (Subtle, Exciting, Dynamic groups as in PowerPoint) that **animate on hover** between the previous and the current slide; direction with an arrow `IconChoiceGroup`; duration with `MeasureField` (s) and slider; sound with a list of embedded sounds and "Browse…"; "Apply to all" as a button, not a checkbox. The same gallery in the side panel and in a "Transitions" ribbon popup | C1, C2, C3, C8 | high |
| Animations ("Effect" dialog and panel) | `DeckMotionCommands.cs`, `DeckView.cs` | panel with "Add"; dialog with three dropdowns and three numbers | **Animation pane** as in PowerPoint: ordered list of effects with an icon per category (entrance green, emphasis yellow, exit red, motion path), draggable, with a timeline; adding from an **animated thumbnail gallery** by category; options (start, duration, delay, direction) in `MeasureField`/`IconChoiceGroup`; Play button | C1, C2, C3, C8, `PanelActionBar` | high |
| Slide Show Settings | `DeckCommands.cs` | flat form: range, from/to, advance, two checkboxes, pen colour in a dropdown, monitor "0 = automatic" | "Slides", "Advance", "Screens" sections in two columns; pen colour as swatches; monitor as a list of screens with name and resolution; from/to with thumbnails | C4, C9 | medium |
| Slide Size | `DeckSlideSetupCommands.cs` | form with numbers and dropdowns; not found by the palette with "Misura della diapositiva" | Gallery of sizes (16:9, 4:3, A4, Letter, Banner…) with drawn proportions; custom with `MeasureField`; "Fit / Maximize" as two illustrated option cards (PowerPoint); synonyms in the palette | C1, C2 | medium |
| Header and Footer | `DeckEditCommands.cs` | six checkboxes and two fields | Slides / Notes and Handouts tabs, **slide preview with highlighted placeholders** (as in PowerPoint), date with live formats in a dropdown | C3, C4 | medium |
| Slide Background | `DeckSlideCommands.cs` | reuses `ShapeStyleView` (Fill, Outline, Effects, **Text** tabs that a background does not need) | `FillEditor` alone with a preview of the whole slide, "Apply to all", "Hide background graphics" | C7, C3 | medium |
| Master: new, delete, apply, elements | `DeckMasterCommands.cs` | four dropdown forms | Master gallery with thumbnails (like the theme), elements as checkboxes on the master preview | C2, C3 | medium |
| Photo Album, Guide/Snap Point, Export Images, Animated Image | `DeckExtraInsertCommands.cs`, `DeckExportCommands.cs`, `DeckAnimatedImageCommands.cs` | flat forms | Album: list of photos with thumbnails, layout as a gallery; export with format, size in px with aspect ratio lock and preview | C1, C2, C3 | low |
| Fields (date, number…), Go to Slide, Name, Distribute, Interaction, Object Styles, Text in Box, Position and Size | `DeckFieldCommands.cs`, `DeckNavigationCommands.cs`, `DeckObjectCommands.cs`, `DeckStyleCommands.cs`, `DeckTextFrameCommands.cs`, `ObjectDialogs/PositionSizeDialog.cs` | `ChoicePromptView` and flat forms; Position and Size with 5 numbers in cm in the label | Fields as an icon menu from the ribbon; Go to Slide with thumbnails; Position and Size with `MeasureField`, lock, clickable 9-position base point, rotation with a dial (shared with Folio and Ledger: `UI.Controls/Objects/PositionSizeView`) | C1, C2, C8 | medium |
| Custom Slide Shows | `CustomShowsView.cs` | dedicated view with two lists | Slide thumbnails, dragging between the two lists | C2 | low |

### Block G — Shell and common surfaces

Files: `Shell/*`, `Desktop/Composition/Shell*Commands.cs`, `UI.Controls/{CommandPaletteView,ChoicePromptView,TextPromptView,ColorPromptView,PrintPromptView,RecoveryPromptView,StatusBarView}.cs`,
`UI.Controls/{Prompts,Objects,Printing,Settings,Spelling,Exchange,Search,Notifications,Shortcuts,Dictionaries,AutoCorrection}/*`.

| Item | File | Current state | What to do | Components | Priority |
|---|---|---|---|---|---|
| Command palette | `CommandPaletteView.cs` | text list, solid grey selection, no icons or shortcuts; no synonyms | Rows with the command icon, path, **shortcut on the right as drawn keys**, selection in the soft accent; "Recent" section when the palette is empty; search by synonyms and keywords (data from the command registry) | `PanelListFrame` | high |
| Zoom | `Prompts/ZoomPrompt.cs` | a number | Presets (50, 75, 100, 150, 200, Fit page, Fit width) as swatches + slider; better as a popup from the status bar | C1, C9 | high |
| Templates (New from Template, Save as Template) | `ShellTemplateCommands.cs` | dropdown of names | **Template gallery with a thumbnail of the first page**, filter by module, large preview, like Word's "New" page; also integrated into the start page | C2, C3 | high |
| Export as PDF, Print, Printer Settings | `Printing/PdfExportPrompt.cs`, `PrintPromptView.cs`, `ShellPrintCommands.cs` | PDF is a long `FormPromptView`; Print has the preview | PDF with the same structure as Print: preview on the left, options in sections (General, Images, Security, Links) | C3, C4 | medium |
| Colour Correction, Replace Colours, Compress, Crop, Alt Text, Image Map, Contour | `Objects/RasterRecolor.cs`, `RasterImageFiles.cs`, `RasterImageOperations.cs`, `AltTextPrompt.cs` | Colour: six bare numbers; Compress and Crop forms | Colour with **sliders and before/after preview** and presets as thumbnails (PowerPoint "Corrections"); compression with a size estimate; alt text with a counter and hint | C1, C2, C3 | medium |
| Spelling, Thesaurus, Language, Hyphenation, Text Conversion (Hangul/Hanja, Chinese) | `Spelling/SpellingPrompt.cs`, `Prompts/TextToolPrompts.cs`, `LedgerSpellingCommands.cs`, `DeckSpellingCommands.cs`, `FolioSpellingCommands.cs` | different forms per module | **A single "Text review" side panel** (as in Word), shared: suggestions, ignore/add, synonyms with meanings; language as a list with search and flag/code | `PanelListFrame`, `PanelSearchBox` | medium |
| Digital Signatures, Certificate, Macro Signing, Signature Line | `ShellSignatureCommands.cs`, `Forms/SignatureLinePrompt.cs` | flat forms | List of signatures with status icon (valid, broken, untrusted), certificate details in sections; signature line with preview | C3, C4 | low |
| Versions, Document Properties, Scanner, Record Macro, Password | `ShellDocumentCommands.cs`, `ShellScannerCommands.cs`, `ShellMacroRecorderCommands.cs`, `Prompts/DocumentPasswordPrompt.cs` | flat forms | Versions as a list with date and author and comparison; properties in tabs (General, Description, Custom, Statistics) | C4 | low |
| Paste Special (Exchange), generic choices | `Exchange/ExchangePasteChooser.cs`, `ChoicePromptView.cs` | a dropdown / text and buttons | Formats as an icon list with descriptions; choices as option cards (block A) | C8 | medium |
| Interface options (bars, command layout) | `ShellInterfaceCommands.cs` | checkboxes and a dropdown | Ribbon layout as three thumbnails (Full, Compact, Tabs only) | C2 | low |
| Settings, Shortcuts, Dictionaries, AutoCorrect, Fonts, About, Recovery, Unsaved Changes | `Settings/SettingsView.cs` and related | dedicated views, already decent | Consistency touch-ups: search in settings, `MeasureField`, `HelpTip`; no rewrite | C1, C5 | low |

## Block status (2026-10-22)

All seven blocks are **closed and merged into `main`**, including the four final branches (Folio, Ledger, Deck,
shell); the detailed notes are in `docs/ux/block-B..G.md`. Final live test in light and dark theme: log, "UX, chiusura
dei blocchi".

| Block | Status | What remains |
|---|---|---|
| A | closed | — |
| B — Folio formatting | closed | — |
| C — Folio insertion | closed: also table position on the page (ODT/DOCX round trip); comment editing from the panel in the three modules | — |
| D — Ledger format and printing | closed | Shared Paragraph not used in Ledger, by choice (cells have no indents or line spacing) |
| E — Ledger data | closed: analysis dialogs with preview, permission-based protection, tabs, step-by-step flows, Insert Function, date filter | — |
| F — Deck | closed: also date formats in header and footer (PPTX `datetime1..13`, ODP date style, `ODP215`), Ctrl+G | dragging in the animation pane not tested live |
| G — Shell | closed: PDF images and security (AES-256, `PDF014`, `PDF020`), version comparison, Text panel even without dictionaries | signing with a real certificate and F7 with an installed dictionary: not testable on this machine |

## Recommended order

1. **A** (components C1–C5, C8, C10; C6, C7 right after), one agent.
2. In parallel, in separate worktrees: **B**, **D**, **F**, **G** (the most used surfaces), then **C** and **E**.
   B provides the shared Character and Paragraph that D uses in the Font tab of Format Cells and in the Ledger page
   setup: D consumes them through the interface and, if B is not yet merged, leaves that tab as it is until the merge.
3. Every block updates `docs/worklog.md` and, at the end, this table (status column to be added).

## Reference captures

In the scratchpad folder indicated above: `L-pagina.png` (worst flat form), `L-cond-pal.png` (conditional
formatting), `L-formatocelle.png` (number formats without an example), `L-convalida.png` (empty tab, dropdown filled
with the accent), `F-carattere.png` (half-empty pane), `F-paragrafo.png` (bare numbers in a column), `F-elenchi.png`
(dropdowns instead of the gallery), `F-campi.png` and `F-indice.png` (modal for a single dropdown),
`D-transizione.png` (transition without gallery or preview), `D-imposta.png`, `D-intestazione.png`, `D-sfondo.png`
(tabs that do not belong), `x-palette.png` (bare palette); by contrast `D-tema.png` (reference level).
