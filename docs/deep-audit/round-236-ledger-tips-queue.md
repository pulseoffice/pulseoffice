# Round 236: Ledger notes and link tips, XLSX rule forms

Area: the round-232 Ledger item and the round-220 table-reference item of the audit queue. No tests or builds were run
in this round (the coordinator builds and tests after the join).

## Done

| # | Item | What changed |
|---|---|---|
| 1 | Note drag on a protected sheet | `SheetProtection.AllowEditObjects` and `BlocksObjects`: the protection dialog has an "Edit objects and notes" permission (five languages). Dragging or resizing a shown note, editing objects (`LedgerObjectEditing.CanEditObjects`) and the `LedgerProtectionGuard` object and comment checks follow it; sparklines stay forbidden. XLSX maps it to `sheetProtection/@objects` (absent means allowed, as in Excel); ODS writes our `urn:pulseoffice:spreadsheet` `edit-objects` attribute on the protected `table:table`, since the ODF table protection has no such option. `Round236ObjectProtectionTests`. |
| 2 | Mentions in the note editor | `IMentionPickers` (`MentionPickers`, shared in `PulseOffice.UI.Controls.Review`) creates a picker with its own popup; the review panel and the Ledger note dialog both use it. `TextPromptView.Editor` exposes the box; the note editor offers the people of `LedgerReviewSource` and saves the detected mentions (`ICommentMentions`, now registered). `Round236NoteMentionTests`. |
| 3 | Annotated range of formatted cells | `IAnnotatedSpanMapping` (`PulseOffice.Sheets`) maps the span from the cell value to the shown text: identical text, value embedded in a literal format, otherwise an in-order character alignment; a span whose ends are not shown has no highlight. `CellValuePainter` draws through it. `Round236AnnotatedSpanMappingTests`. |
| 4 | Shared tips in the Ledger grid | `SheetGridView` takes the shared `ILinkTips` (from `LedgerDocumentFactory` through `LedgerView`); the grid surface shows every tip through it and the pointer input computes cell and object link texts with `ILinkTips.Text`. The grid's own tooltip code is gone. |
| 5 | Folio exact hover hit and group members | `ILayoutQueries.CharacterAt` returns the character whose glyph box holds the point, or nothing (outside rotated frames); hover tips use it instead of the nearest caret position. `IFolioObjectLinks.Locate` finds the pointed group member without changing the link-edit target, and `IFolioLinkTips.OfObject` shows the member's effective link. `Round236CharacterHitTests`. |
| 6 | Deck tips in the editor and for text links | Shared `IRichTextLinkHit` (`PulseOffice.UI.Controls.Text.Rich`) finds the linked run under a point of a composed text frame by its highlight geometry. `IDeckLinkTips` (Presentations.UI) gives the tip of a text run link of a text box (unrotating the point) or of the element's hyperlink click action; the editor canvas shows it on hover and the show window uses the same service. `Round236TextLinkHitTests`. |
| 7 | XLSX rule forms after renumbering | `XlsxTableReferenceRules.RestoreRules` first settles every entry that exactly one unused rule matches by range and flat formulas; priority and ordinal decide only among several equal candidates, then the first candidate. `Round236TableReferenceRenumberTests`. |

## Choices recorded

- Matching order for structured rule forms: range plus flat formula is the identity Excel keeps when it renumbers
  priorities, so it comes first; priority and ordinal are only a tie-breaker between rules that look the same flat.
- The edit-objects permission is one switch for drawn objects and notes, as Excel's `objects` attribute covers both.

## Research notes

ECMA-376 `CT_SheetProtection/@objects` defaults to false (objects not locked). ODF 1.3 defines only
`table:select-protected-cells` and `table:select-unprotected-cells` for table protection, so object editing needs our
extension.

## Open

See the round 236 lines of `docs/audit-queue.md`.
