# Block B — Folio: text formatting, shared with Deck

Status: **closed** (resumed after the interruption; clean `--no-incremental` build and full suite).

## Done

Infrastructure (`UI.Controls`):
- `FormPromptView.ValuesChanged`; `FormFieldEditor.Observe` (custom fields report changes: colour, swatches,
  icon choices, borders, tabs); `FormTab.LivePreview(IFormValuesFeed)` with `FormValuesFeed` reading the values of
  all tabs; `TabbedDialogView` hides the tab strip when there is only one tab.
- `TextFormat/`: `ITextFormatPreviews` (LivePreviewPane fed by the form); `ICharacterFormatTabs`/`ICharacterFormatDialog`
  (neutral model `CharacterFormatState`, `CharacterFeatures` says what the surface can write, preview
  `CharacterSampleView` of the selected text); `IParagraphFormatTabs`/`IParagraphFormatDialog` (alignment as icons,
  indents and spacing with `MeasureField`, line spacing as swatches 1 · 1.15 · 1.5 · 2 · More, preview with grey lines
  `ParagraphSampleView`); `IListFormatTabs`/`IListFormatDialog` with `ListLevelsEditor` (thumbnail gallery of bullets and
  numberings, levels 1–n, symbol with `SymbolPickerView`, text before/after, start, levels shown, indents) and multi-level
  preview `ListSampleView`, "headings" mode for chapter numbering. Clean interfaces, ready for Ledger
  (block D).
- `Pages/`: `PageMarginsEditor` (preset margins as thumbnails: Normal, Narrow, Moderate, Wide, Mirrored),
  `ColumnLayoutEditor` (1–4 column gallery + number, spacing, line), `PageNumberPositions` (gallery top/bottom ×
  left/centre/right).
- Icons `TextFormatIcons` (`text.*`, `page.orientation.*`). 128 new keys in the five languages.
- `IRichTextFormatter`: `MapParagraphs`, `MapRuns`, `ParagraphsIn`, `RunsIn`.

Surfaces:
- Folio's Character → shared view (two tabs with preview); Deck's → same view (previously a flat form), with only the
  properties PPTX/ODP write: added colour, highlighting, superscript/subscript (already in the model and in both formats).
- Folio's Paragraph → first shared tab (merges indents and alignment), then flow, tabs, borders, drop cap;
  Deck → shared view (indents, spacing before/after, proportional line spacing; previously only alignment and 1/1.5/2).
- Folio and Deck lists, chapter numbering → shared view. Folio edits all levels of the style in use.
- Page setup → Format/Margins/Layout sections, orientation as icons, live `PagePreview` on every tab,
  page borders with `BorderEditor`, columns as a gallery.
- Styles: the Organizer tab has the paragraph preview; Character/Paragraph shared as tabs.
- Sections: columns as a gallery, groups; the section choice is skipped if there is only one.
- Page numbers: gallery of positions (default bottom centre).

No new loss: no DOCX200–204/ODT200–204/PPTX210–214/ODP210–214 code used.

## Resume point

1. `test## Second part (resumed)

- **Sections…**: a single dialog, one tab per section (opens on the cursor's one) with columns as a gallery, protection
  and hide; protected sections ask for the password in their own tab and are modified only if it is correct (the other
  changes are applied anyway, in a single transaction). Instead of the vertical list on the left there is the tab
  strip: same role, no new component.
- **New style / derived style**: name with a preview of the base style (paragraph: grey lines; character: the
  name written in the style's font). `IFolioStyleDialogs.NameAsync(editor, …, basis, …)`.
- **Line numbering**: margin preview (`Pages/LineNumberSampleView`, shared), side as icons, fields hidden
  while numbering is off.
- **Comment format**: `FontField` instead of the text box, and a font preview.
- **Phonetic guide**: preview of the text with the guide above.
- **Sort**: "Sort key" section, type and order as segments, column visible only with a separator.
- **Theme**: thumbnail gallery of palettes (`Galleries/ThemePaletteGallery`, shared).
- `IFolioForms.AskAsync(…, preview, …)`: single form with live preview.
- Tests: `TextFormatTests` (11, UI.Controls) and `TextFormatPixelTests` (4, Desktop: right alignment, underline,
  line-number side, page columns, measured on rendered pixels).

## What remains

- Ledger (block D): reuse `ICharacterFormatTabs`/`IParagraphFormatTabs` and `PageMarginsEditor`, `PageNumberPositions`.
- To be tried live: all surfaces, light and dark, keyboard in the galleries and in the list editor.
