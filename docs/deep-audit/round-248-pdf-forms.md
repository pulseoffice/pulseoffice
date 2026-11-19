# Round 248: PDF form fields and export options

Area: AcroForm fields written by `PdfFormWriter` (round 244), Folio content controls marked as fields by
`FolioPrintMarks`, the PDF export dialog (`PdfExportView`) and the PDF options used when printing.

## Defects found

| # | Severity | Defect | Fix |
|---|---|---|---|
| 1 | Medium | Widgets in a tagged PDF had no `/StructParent` and no structure element, so PDF/UA checkers report untagged annotations and screen readers skip the fields. | `IPdfAnnotationTagging.Field` declares a `Form` element (alternate text from the tooltip or name) reached through an `OBJR`; `PdfFormWriter.Widgets` reserves the widget id first and takes the page's tagging. New `PrintRole.Form`. |
| 2 | Medium | Rich text and multi-paragraph content controls were not exported as fields. | Rich text controls become multiline text fields; a control spanning paragraphs is indexed on every paragraph it covers and becomes one multiline field whose value joins the paragraphs with line breaks. A rich text control that encloses another exported control is left out, so the fields do not overlap. |
| 3 | Medium | Picture content controls were not exported. | `PrintFormFieldKind.Picture` becomes a push button (`/Ff 65536`, `/MK /TP 1`) with a transparent appearance, so the picture drawn on the page stays visible and no white background is painted over it. |
| 4 | Medium | The PDF export dialog had no switch for `PdfExportOptions.Forms`. | "Form fields as fillable fields" check box, translated in the five languages. |
| 5 | Medium | The dialog had no switch for `PdfExportOptions.Tagged` either: tagging could not be turned off. | "Tagged PDF for accessibility" check box, translated. |
| 6 | Medium | Print to PDF and print through a temporary PDF (with imposition: booklets, several pages per sheet) kept form fields, whose positions do not follow the imposed sheet, while links and notes were already turned off there. | `Forms = false` in both print paths of `ShellPrintCommands`. |
| 7 | Minor | A field without a tooltip had no `/TU`, which PDF/UA requires for form fields. | `/TU` falls back to the field name. |

## Tests

`Round244PdfFormFieldTests` (tagged widget with `StructParent`, `Form` and `OBJR`; picture push button, no
`StructParent` when untagged), `Round244FolioFormFieldMarkTests` (rich text over two paragraphs is one multiline field),
`PdfExportPromptTests` (both new switches reach the exporter options).

## Still open

- Text and choice widgets keep a blank appearance stream while the value is drawn in the page content; a viewer that
  regenerates appearances draws the value twice. Real appearance streams (or no page text under the widget) are needed.
- The `Form` elements are root-level structure elements, not children of the paragraph or cell they sit in.
- A field that crosses a page break becomes one widget per page with numbered names, instead of one field with
  several widgets (`/Kids`).
- Not yet opened in an external PDF viewer.
