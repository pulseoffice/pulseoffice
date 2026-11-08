# Block D — Ledger: format, styles, printing (status at the suspension point)

## Done
- **Shared theme**: `ListBoxItem` with selection in `PulseAccentSubtle` (no longer a full accent tint), compact items (padding 8,3, MinHeight 0), consistent hover and focus.
- **Format cells → Numbers** (`Sheets.UI/CellFormat/NumberFormatPage.cs`): categories on the left, live sample with the active cell's value (or a typical value), decimals with `MeasureField`, thousands separator, currency symbol, negatives as swatches (red/parentheses), compact list of formats with sample and code, free code below with a warning if invalid. Pure logic in `Sheets/Formatting/NumberFormatDesign.cs` (`INumberFormatDesigner`: Compose/Recognize).
- **Format cells → Alignment** (`CellAlignmentPage.cs`): horizontal/vertical `IconChoiceGroup`, indent with `MeasureField`, orientation with the new **`AngleDial`/`AngleField`** (UI.Controls/Fields, shared: `FormPromptView` adds it automatically to every field in "°", so it applies in Folio, Deck and Ledger), cell preview.
- **Page setup** (`Sheets.UI/PageLayout/SheetPageSetupView.cs`): four tabs (Page, Margins, Header and footer, Sheet), each with `PagePreview`; scale % or fit to N×M pages; margins and bands in mm; centring; **three-area editor with insert buttons** (`UI.Controls/Printing/HeaderFooterZonesEditor` + `HeaderFooterTokens`: the codes &P &N &D &T &F &A appear as [Page]…) and presets; print area with `RangeField`, rows/columns to repeat, page order as icons. All fields already existed in the model and cross ODS and XLSX: no new loss code.
- **Conditional formatting** (`Sheets.UI/Conditional/*`): rule manager (`ledger.conditional.manage`, new command, ribbon home/styles) with a drawn preview per rule, selection/sheet scope, New/Edit/Duplicate/Delete/Up/Down (`PanelActionBar`, Alt+↑↓, Enter, Del); editor with a drawn gallery of types (value, top/bottom, average, duplicates, text, date, blanks, formula, bars, scales, icons), styles as swatches + custom, bar colours, gallery of scales and icon sets. No new type in the model.
- **Cell styles**: drawn gallery (`GalleryPickerView<T>`, new shared component in UI.Controls/Galleries, + `CellStylePainter`), new style with preview (`NewCellStyleView`).
- **Sparklines**: dialog with types as thumbnails drawn on the real data, colours, markers, preview. The drawing is extracted into `ISparklinePainter` (also used by the grid, which now also draws the high/low/first/last markers, already saved in ODS/XLSX).
- **Document theme**: `UI.Controls/Themes/ThemePaletteGallery` wired into Ledger and Folio (Deck already has its own theme gallery).
- New texts in the five languages; new icons (`ledger.align.fill`, print order, `ledger.conditional.manage`, `ui.duplicate`).

## Completion (second session)
- **Tests**: `Sheets.Tests/NumberFormatDesignerTests`, `UI.Controls.Tests/BlockDComponentsTests` (header placeholder round trip, `&&`, half-circle and full dial), `Sheets.UI.Tests/ConditionalRulesTests` (completion, type recognition, swatches, descriptions in the five languages), `Desktop.Tests/LedgerFormatSurfacesTests` (headless views: numbers, alignment, Page setup, manager, editor, sparklines).
- `IConditionRules`/`ConditionDraft` removed: the logic is now `IConditionalRuleCompletion` (`Conditional/ConditionalRuleCompletion.cs`), used by the commands and tested.
- Rule manager: dragging a row over another (`MoveVisible`), in addition to Alt+↑↓.
- Format cells: cell preview (background, font, borders, formatted value) on the right also in Font, Borders, Background and Protection.
- Quick alignment (`ledger.format.paragraph`): now it is the shared Alignment page in a dialog of its own.
- Optimal width: margin in pt with `MeasureField` and presets. Pivot calculated field: formula with fields as insert buttons. Anchoring: unchanged (a single `RangeField` with help).
- Folio: the `FolioScene` test registers a silent `IThemePaletteGallery`.

## Still to try live
All dialogs in light and dark, the nested manager → editor dialog and dragging rules.

## Closure (third session)
Ledger's remaining surfaces (data, sheets, analysis) were completed in block E: see `block-E.md`, "Completion
(third session)". Sheet protection with check-box permissions and the tab colour as a popup also close the sheet-format
part. The shared Paragraph remains unused in Ledger by choice: cells have no indents or line spacing.
