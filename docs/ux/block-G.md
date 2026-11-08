# Block G — shell and common surfaces (closed on 2026-10-22)

## Surfaces redone

| Surface | Before → after |
|---|---|
| Command palette | grey text list → rows with icon, path, category and shortcut in drawn keys (`KeyCapsView`), selection in the subtle accent; with an empty palette "Recent" at the top (`IRecentCommands`, file `recent-commands.json`) and commands by category; case- and accent-insensitive search (`SearchFolding`) with a phrase bonus on the synonyms `command.<id>.keywords` (about 40 commands, five languages: "Slide measure" finds Slide size). Source: `ICommandPaletteSource`. |
| Zoom | a number → `ZoomView`: zoom levels as segments, "Whole page" and "Width" (Folio, Deck), percentage with `MeasureField` and slider, `ZoomGlyph` thumbnail. API `ZoomRequest`/`ZoomAnswer`; adapters `IFolioZoomChoice`, `IDeckZoomChoice`. `StatusZoomButton` in the status bar of the three modules (`IStatusZoomSource`). |
| TextPromptView | field and two buttons → inline validation with rules (`ITextInputRule`: required, forbidden characters, name already used, length), Enter confirms. Wired into Rename sheet (forbids `[ ] * ? : / \`, like Excel) and Ledger's New style. |
| New from template | drop-down → `TemplateGalleryView`: real thumbnail of the first page (`PageThumbnail`, `IDocumentOperations.PreviewFile` in the three modules), filter by application, search, large browsable preview; `ITemplateLibrary`; link on the start page. |
| Export to PDF | long form → `PdfExportView`: General, Images, Structure and links, Security, Archiving, Description sections; page preview that follows range and layout; count or error below the field. Images: lossless or JPEG with quality (slider 5–100) and maximum resolution (75–1200 DPI). Security: open and permissions passwords, each to be repeated (if they do not match the export stops with the error below the fields), printing (no, low, high resolution), copying, changes, comments and forms; with PDF/A the section is disabled and says so. |
| Colour correction | six bare numbers → `ColorCorrectionView`: eight ready corrections as thumbnails of the real image (`IColorCorrectionPresets`), sliders for light and colour, before/after comparison, Reset. |
| Spelling, Thesaurus, Language | different forms per module → one shared "Text" side panel (`TextReviewSession` implements `ISpellingPrompt`, `IThesaurusPrompt`, `ILanguagePrompt`; view `TextReviewPanel`): word in a card, suggestions, free correction, actions; synonyms grouped by part of speech; languages with search and code (`ILanguageChoices`). Wired into Folio, Ledger, Deck. The panel is not modal; closing it ends the review. |
| Digital signatures | form with a "What do I do" drop-down → `SignaturesView`: list with coloured status (valid, warning, broken), signer, issuer, expiry; certificate and password; Remove all / Sign. |
| Versions | drop-down + boxes → `VersionsView`: list from the most recent with date, author, comment; Save version with comment, **Compare with the document** (also double click), Open alongside, Delete. |
| Version comparison | opening the version in a window alongside → `VersionComparisonView` (shared): added/deleted/modified counters, one row per difference with the location (Paragraph n, Sheet!A1, Slide n), a coloured strip and the text with added words underlined in green and removed ones struck through in red; "Open the version alongside" remains as a second action. |
| "Text" panel without dictionaries | F7 said "check finished" (Folio, Ledger) or a warning in the status bar (Deck) → the panel explains that a dictionary is missing and has the **Dictionaries…** button |
| Document properties | flat form → General, Description, Custom (name/value/type rows, `CustomPropertiesField`), Statistics (`InfoField`) tabs. Custom properties were already in the model and in both formats. |
| User interface | drop-down → three drawn thumbnails (`InterfaceModeField`). |
| Settings | each entry's description goes through `HelpTip`. |

Paste special: done by block E. No new loss code (ODF220–224 and ODF230–234 free): ODF and OOXML do not change;
the new options concern only the written PDF.

## PDF export: images and security in the exporter

- `PdfExportOptions.Images` (`PdfImageOptions`: `Compression` Lossless/Jpeg, `JpegQuality`, `MaxResolution`) and
  `PdfExportOptions.Security` (`PdfSecurityOptions`). Images go through `IPdfImageWriter` (`PdfImageWriter`): the
  resolution is measured on the printed size (`PdfImagePlacement`, points recorded by `PdfContentCanvas.DrawImage`)
  and reduced with `IImageResampler` (`BoxImageResampler`, area averaging, including the alpha mask); JPEG comes from
  `IJpegEncoder` (`PulseOffice.Formats.Imaging`), implemented by `SkiaJpegEncoder` in `Formats.Rasterizing` (Skia stays
  only there). `/DCTDecode` stream; the alpha mask stays lossless. Without a JPEG encoder: lossless and warning
  `PDF020`. Identical images are written once per size.
- Encryption: standard handler **V5 R6, AES-256** (`Aes256EncryptionFactory`, `Revision6PasswordHash` = algorithm 2.B
  of ISO 32000-2, `Aes256ObjectSealer`). Encryption happens when there is a password or a denied permission; without a permissions
  password a random one is generated (the limits apply anyway). Streams and strings are encrypted (`StmF`/`StrF` =
  `StdCF`, AESV3, random IV): object strings are recognised with `PdfStringTokens` (nested literals and
  escapes, hexadecimal) and rewritten as encrypted hexadecimal; the `/Encrypt` dictionary and the `/ID` stay in clear.
  `/Perms` carries the permissions signed with the key; the catalogue declares `/Extensions /ADBE` level 8. Permissions: printing
  (bits 3 and 12), modification and assembly (4 and 11), copying (5), comments and forms (6 and 9); extraction for accessibility
  always granted. PDF/A does not allow encryption: with both, PDF/A wins and the exporter warns `PDF014` (the dialog
  already disables the section).
- Verification: `PdfImagesAndSecurityTests` (DCT with SOI marker, lower quality → smaller file, resampling to
  150 DPI, independent decryption in the test with the right password and rejection of the wrong one, encrypted title
  string, consistent `/P` and `/Perms`, owner key equal to the user key). Outside the suite: macOS
  PDFKit (Swift script) opens the files, `isLocked` with the open password, unlocking only with the right one,
  `allowsPrinting`/`allowsCopying` false with the permissions denied, text and title readable; the reference suite run headless
  converts to PNG the PDF with JPEG and the one with permissions only, and rejects the one with the open password.

## Version comparison

- Common core: `PulseOffice.Document.Comparison` (the sequence diff `SequenceDiff` and `WordTokens`, moved from
  Folio because all three need them) and `PulseOffice.Document.Versions.VersionComparison` (`VersionChange`, `VersionRun`,
  `IVersionTextDiff` by word, `IVersionSequenceComparer` which pairs removed and added blocks into "modified").
- `IDocumentOperations.CompareVersion(document, path)`: Folio compares paragraphs, Deck slides (text of the
  boxes in depth order), Ledger cells with `IWorkbookComparer` (sheets added or removed, cells with formula or
  value). "Added" means present in the document and not in the version.
- The dialog shows at most 500 differences, then "…and n more".
- `DialogOptions.TitleArguments`: dialog title with arguments, also translated on language change.

## "Text" panel and a changing document

`TextReviewEditWatch` (shell) watches the history of the active document: if it changes while the panel is waiting,
`ITextReviewSession.Invalidate()` closes the question. Spelling receives `SpellingDecision.Recheck` and rereads: Folio
from where it was in the paragraph, Ledger from the current cell, Deck from the current slide with a new pass
(ignored words and "change all" are kept). Thesaurus and language are cancelled. The panel shows the notice
(`textReview.changed.recheck` / `.cancelled`) until the user answers. If the active document changes the request
is closed.

## Other

- `ThemePaletteField` was unused (the theme gallery of Folio and Ledger is `ThemePaletteGallery`): removed.
- The selected items of drop-downs (`ComboBoxItem`) still had Fluent's accent tint: now subtle accent like the
  lists (`ListSelectionStyleTests` checks list and drop-down).

## Declared limits

- Version comparison looks at text (paragraphs, cells, slide text), not formatting,
  images or object positions.
- The PDF password is prepared with NFKC normalisation instead of full SASLprep: identical for common
  passwords, it may differ for exotic characters.
- Ledger templates without cells show a placeholder: an empty sheet has no pages.

## To try live

Palette in light and dark and from the keyboard; zoom from the status bar; template gallery with real files; PDF with
Deck layouts; colour correction on a photo; the "Text" panel with F7 in the three modules; signatures with a real certificate.
New: PDF dialog with the Images and Security sections in light and dark (quality slider disabled without JPEG,
error below passwords that do not match, section disabled with PDF/A) and the encrypted PDF opened in Preview and in Acrobat;
Versions → Compare in Folio, Ledger and Deck (word colours in the dark theme); F7 with the question open and a
change to the document; F7 without dictionaries.
