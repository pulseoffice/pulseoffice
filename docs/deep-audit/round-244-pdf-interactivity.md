# Round 244 (2026-11-19): PDF interactivity

Area: links, navigation, page labels and form fields in PDF export.

## Defects found and fixed
- **Medium**: Deck text elements with several different run links, and links inside shape text, produced no link
  area. Link areas are now computed per run from the composed text (`SlideLinkAreas`, `RichTextLinkHit`).
- **Medium**: slide navigation actions (go to slide, next, previous, first, last) were not internal links; they now
  become `GoTo` destinations to the target slide's page.
- **Medium**: page labels were written only for Folio. Deck slide numbering start and Ledger first page number now
  produce `/PageLabels` with the right start.
- **Medium**: Folio content controls and converted legacy form fields were exported only as painted text. Plain text and
  date controls become text fields, check boxes check-box fields (with on and off appearances), drop-down lists
  choice fields and combo boxes editable choice fields, all listed in an `/AcroForm` with unique names, the title as
  tooltip and content locking as read-only. `IPageCanvas.MarkField` carries them, `IPdfFormWriter` writes them; the page
  content still paints the value, so text and choice widgets have an empty appearance with a white background for the
  viewer to regenerate on edit. `PdfExportOptions.Forms` turns them off.

## Open (queued)
- Widgets are not linked into the structure tree (`/StructParent`, `OBJR`); round 243 handles annotations there and
  can hook `IPdfFormWriter`.
- Rich text, picture and multi-paragraph content controls are not exported as fields; the export dialog has no
  switch for form fields.
- Fields were not opened in an external PDF viewer.
