# Round 252 (2026-11-19): PDF form field follow-ups

Area: the PDF export of form fields and their tagging, from the items queued by rounds 243, 247 and 248.

## Defects

| Severity | Defect | Status |
|---|---|---|
| Medium | Text and choice widgets had a blank appearance stream over the value drawn in the page content: a viewer that regenerates appearances (or one that redraws after an edit) painted its own value over the page text, so the old and new values showed through each other. | Fixed |
| Medium | A content control crossing a page break became one field per page with numbered names (`Note`, `Note_2`), so a form reader saw two fields and filling one did not fill the other. | Fixed |
| Minor | `Form` structure elements were children of the document root instead of the paragraph or table cell they sit in. | Fixed |
| Minor | Deck paragraphs are matched to drawn text by text, not by layout position. | Open (queued) |

## Fixes

- Text and choice widgets now carry a real normal appearance: an opaque white background (as `/MK /BG` already
  declared), then the value in Helvetica 10 inside `/Tx BMC … EMC`, clipped to the widget, one line per value line, with
  the font in the stream's own resources. The opaque background hides the value drawn in the page content below, so a
  viewer that keeps our appearance shows the page's formatting covered by the same text, and a viewer that regenerates
  appearances paints its own value over the same white background: the value shows once either way. `NeedAppearances`
  is not set: viewers honour the appearance we write, and the flag is deprecated in PDF 2.0. The page content still
  draws the value, so printing or flattening with forms off keeps the document's own formatting.
- `PrintFormField.Identity` (Folio sets the content control span id) groups the widgets of one control across pages:
  the writer collects widget entries per export (`IPdfFormWriter.Begin` returns a `PdfFormFields` registry) and writes
  the objects at the end. A group with one widget stays a merged field-widget; a group with several becomes a parent
  field (`/T`, `/TU`, `/FT`, `/Ff`, `/V`, `/DA`, `/Opt`, `/Kids`) whose widgets carry only `/Rect`, `/AP`, `/AS`, `/MK`,
  `/StructParent` and `/Parent`. The value is the concatenation of the page fragments; each widget's appearance shows its
  own fragment. Fields without an identity that share a name still get numbered names.
- `IPdfAnnotationTagging.Field` takes the marked field with its area and declares the `Form` element under the element
  that covers it most, as links already do (round 247).

## Open

- Deck paragraphs by layout position: the shape text layout still does not expose line boxes to the print path; this
  needs a print-side line locator over Deck's shape text layout and was not done here.
- A field crossing a page joins its fragments without a separator; a break that fell between paragraphs loses the line
  break in `/V`.
- Appearance text uses Helvetica with WinAnsi encoding: characters outside it show as `?` in the appearance (the page
  content below is covered), and the font size is fixed at 10 pt rather than the control's own size.
- Not opened in an external PDF viewer.

Tests: `Round252PdfFormFieldTests` (appearance content, parent field with kids, separate same-name fields, Form under
its paragraph); `Round244PdfFormFieldTests` adjusted to the new entry order.
