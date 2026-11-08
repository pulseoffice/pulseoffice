# Block C — Folio: insertion, references, tables and objects

Status as of 2026-10-22: `--no-incremental` build with no warnings, full suite green. No new loss code (no change to the formats).

## Done

| Surface | Before | After |
|---|---|---|
| Hyperlink (Folio, Ledger, Deck) | flat form, different per module | `UI.Controls/Links/`: `HyperlinkView` with the type on the left (web page, this document, file, e-mail), tree of targets with search, displayed text that follows the address until you type it, "Opens: …", Remove link. `ILinkAddresses` (classifies and composes addresses), `IHyperlinkPrompt`. Targets: Folio nested headings/bookmarks/tables/sections (`IFolioLinks`), Ledger sheets and defined names, Deck first/previous/next/last and slides (GoToSlide) |
| More fields… / Cross-reference… / Edit field | chain of drop-downs | `Documents.UI/Fields/`: three-column dialog (categories, fields with search, options) with a preview of the value on paper; options as `FolioFieldOptions` strategies (date/time with sample formats, names, variables, user field, conditions, simple); Cross-references page with types, targets with search, formats with an example; Insert leaves it open, in edit mode Apply closes. In the core `IFieldUpdater.Preview` |
| Index or table of contents… | "Type" drop-down then tabs | gallery of drawn thumbnails (`FolioIndexThumbnail`), Tables of contents/Indexes groups, direct Insert, Customize → tabs |
| Manual break | drop-down | `IconChoiceField` with labels, page fields shown only for a page break |
| Object properties | anchoring and wrap drop-downs | `IconChoiceField` with the existing icons |
| Caption | form | "Figure 1: …" preview above/below the image |
| Mail merge… | one form | two-step wizard: recipients with merged letter and navigation, then result as tiles and file name |
| Shared components | — | `FormPromptView.Changed`; `FormFieldEditor.Subscribe` for custom fields |

Tests: `UI.Controls.Tests/HyperlinkViewTests`, `Documents.UI.Tests/FolioFieldsDialogTests`, `FolioIndexGalleryTests` (also pixel-level), `FolioMergeWizardTests`.

## Second round

| Surface | Before | After |
|---|---|---|
| Split cells, Text to table | flat forms | grid preview (`TableGridPreview`, `ITableTextSampler`) of the parts or of the first rows split by the chosen separator |
| Table properties | alignment as drop-down, colour always visible | vertical alignment as icons, colour shown only with "Change background"; borders already on `BorderEditor`. Position on the page (Table tab): left/centre/right as icons (`folio.table.place*`), left indent in cm shown only with "Left", live preview `TablePlacementPreview` (page with dashed margins and table proportional to the text width) |
| Object size (Folio, Deck, Ledger) | two separate measures | `UI.Controls/Fields/SizeLockField` (`SizeLock`): two `MeasureField`s and an aspect-ratio lock (closed for images); in Folio Object properties, in Deck Position and size, in Ledger the new `ledger.object.positionSize` (size and offset from the cell) |
| Go to page | modal | "Page x of y" field in the status bar (`StatusSegment.Accept`, `FolioPageStatus`), Ctrl+G focuses it, Enter jumps, Esc cancels |
| AutoText | action drop-down + entry drop-down | `AutoTextView`: list with search, preview of the entry on paper, new entry from the selection, delete |
| Envelope | form | drawn preview (`EnvelopePreview`) in the chosen size with sender, recipient and stamp |
| Index entry | modal | non-modal side panel (`FolioIndexEntryPanel`, registered on first use) that takes the current selection and stays open |
| Reply / Edit comment | modal | in the Review panel (`IReviewPanelRequests`): the command opens the panel on the comment with the box ready; editing saves with `IReviewSource.Edit` |

New tests: `SizeLockTests`, `ReviewPanelRequestTests`.

### Table position on the page

Model: `TableBlock.Alignment` (`TableAlignment` Left/Center/Right) and `TableBlock.Indent` (`Twip`, only with Left);
`ITableFormatting.SetPlacement` is undoable (extended memento), and copy/paste, table split and snapshots
carry it. Layout: `TableItem.Offset` → `TablePlacement.Offset`, added to the x coordinate in `TableComposer.Emit`
(centre and right never go left of the margin; the flow key includes the position).
Formats, lossless (no new code): ODT `table:align` (`left`/`center`/`right`; `margins` is read as
left) and `fo:margin-left` on the table's automatic style; DOCX `w:jc` (`center`/`right`) and `w:tblInd` in
dxa (a percentage indent is ignored). Tests: `TableRoundTripTests.LaPosizioneDellaTabellaNellaPaginaAttraversaIFormati`
(ODT and DOCX, three cases each), `TablePlacementLayoutTests`.

## Remaining

- Live test in light and dark of all the block's surfaces, including the table position (Table
  properties → Table: centre, right, indent; preview and document; round trip in ODT and DOCX opened in another office suite).
- Ledger and Deck do not yet implement `IReviewSource.Edit` (inline editing exists only where `Abilities.Edit`).
