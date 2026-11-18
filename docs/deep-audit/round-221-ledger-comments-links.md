# Round 221: Ledger cell comments and cell hyperlinks

Area: Ledger cell comments (notes), their editing, showing, following cells under structure edits, paste and sort,
protection, printing and review panel; XLSX (`comments*.xml`, VML note shapes, threaded comments, persons) and ODS
(`office:annotation`) round trip; cell hyperlinks (insert, edit, remove, follow, screen tip) and their round trip.

## Defects fixed

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | ODS reading took a link written inside a cell comment as the cell's own hyperlink. | The cell hyperlink ignores `text:a` inside `office:annotation`. `Round221OdsCommentLinkTests`. |
| 2 | Medium | An XLSX `hyperlink` whose `ref` is a range linked only its first cell. | Every cell of the range is linked (up to 10,000 cells; larger ranges keep the first cell). `Round221XlsxLinkTests`. |
| 3 | Medium | The screen tip of a cell hyperlink (XLSX `tooltip`, ODS `office:title` on `text:a`) was dropped silently. | `Cell.HyperlinkTip` in the model, written and read in both formats, kept by row/column shifts and filter copies, cleared with the link. |
| 4 | Medium | The ordinary paste (Ctrl+V, transpose, paste from the exchange) dropped cell comments and validation; only paste special "All" carried them. | Ordinary paste uses `PasteParts.Everything`. |
| 5 | Minor | Pasting with comments left the destination's old comments on cells whose source had none. | The pasted area's uncopied comments are cleared in the same undo step (not with skip blanks). `Round221CommentPasteTests`. |
| 6 | Medium | The comment printing choice (XLSX `pageSetup/@cellComments`, ODS `style:print` `annotations`) was dropped silently. | `PageSetup.PrintComments` (none, at end, as displayed); "as displayed" has no ODS value and rides on `annotations` plus our `urn:pulseoffice:spreadsheet` `comments-as-displayed` attribute. |
| 7 | Minor | Comment dates are lost in XLSX (legacy notes have no date) without saying so. | Declared as XLSX243 in the fidelity check, translated in the five languages. |
| 8 | Medium | A cell hyperlink could not be followed at all; object links to places in the workbook did nothing from the "open" command. | New `ledger.cell.hyperlink.open` (menu and cell context menu) and `ILedgerPlaceNavigation`, shared by the object command. `Round221CellLinkTests`. |
| 9 | Minor | Hovering a linked cell showed nothing. | The hover tip shows the screen tip, otherwise the target (the comment still wins). |
| 10 | Medium | Commands could add comments to a protected sheet (paste now carries them) while the comment commands refuse it. | `LedgerProtectionGuard` refuses a command that adds comments on a protected sheet; moves and removals by allowed structure edits pass. |
| 11 | Minor | Notes written by Excel start with a bold "Author:" run; it ended up in the comment text, so the author appeared twice. | The heading is dropped when it matches the note's author. |

## Verified without defects

Comments follow row and column insertion and deletion (`CommentsPart`), range moves (`MovedCommentsPart`), sort
(one step with the sort), sheet duplication and snapshots; edit and delete are one undo step each; show all, hide all
and delete all are one step and refuse on a protected sheet; the review panel lists, edits and deletes comments with
undo and respects protection; XLSX visibility comes from the VML `x:Visible`, ODS from `office:display`; author and
date cross ODS.

## Research notes

Excel legacy notes have no date field, and `CT_Comment` has no extension list, so the date has no native or extension
place without switching notes to threaded comments, which Excel shows as a different kind of object; the loss is
declared instead. ODF 1.3 `style:print` has a single `annotations` token, so "as displayed" uses our own attribute.

No tests or builds were run in this round (coordinator runs them after the join).
