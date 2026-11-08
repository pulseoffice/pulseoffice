# Deep audit round 36: picture bullets and object hyperlinks

Continues [round 35b](round-35b-folio-editing.md), gaps 1 and 2.

## Coverage

| File | Read | Outcome |
|---|---|---|
| src/PulseOffice.Documents.Layout/Flow/ListLabeling.cs | all | label carries text only: picture dropped (gap 1) |
| src/PulseOffice.Documents.Layout/Flow/ParagraphEntries.cs | all | label cache keyed on text only |
| src/PulseOffice.Documents.Layout/Inline/ParagraphShaper.cs | all | label shaped as text |
| src/PulseOffice.Documents.Layout/Inline/ShapedParagraph.cs | 1-60 | `ShapedLabel` text only |
| src/PulseOffice.Documents.UI/Rendering/PageSurfaces.cs | 1-200 | `Embedded` draws on screen and print/PDF: reusable for the picture |
| src/PulseOffice.Documents.Layout/Lines/LineComposer.cs | 1-160 | label run built without picture |
| src/PulseOffice.Documents.Layout/Lines/LineBreaker.cs | 55-90 | label width drives first-line start: picture width must be the advance |
| src/PulseOffice.Documents.UI/Rendering/PageRenderer.cs | all | label always drawn as glyphs |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxPictureBullets.cs | all | VML size read; Word scales the picture to the text: flag needed |
| src/PulseOffice.Formats.Odf/Odt/OdtListPictures.cs | all | fo:width/height read and written: fixed size |
| src/PulseOffice.UI.Controls/TextFormat/ListFormat.cs | all | no picture in the shared level format |
| src/PulseOffice.UI.Controls/TextFormat/ListLevelsEditor.cs | all | no way to set or remove the picture |
| src/PulseOffice.UI.Controls/TextFormat/ListPresets.cs | all | presets kept a stale picture on apply |
| src/PulseOffice.UI.Controls/TextFormat/ListSampleView.cs | all | sample drew the character only |
| src/PulseOffice.Documents.UI/Commands/FolioListCommands.cs | all | dialog mapping carried no picture, so the dialog could not show or change it |
| src/PulseOffice.Documents.UI/Commands/FolioFieldCommands.cs | 75-140, 285-310 | text links opened through `IShellOpener` directly: moved to the shared `ILinkFollower` |
| src/PulseOffice.Documents.UI/Commands/FolioInsertCommands.cs | 160-205 | hyperlink dialog for text only |
| src/PulseOffice.Documents.UI/Canvas/FolioCanvas.cs | 20-60, 330-420, 750-800 | no Ctrl/Cmd-click activation of object links (gap 2) |
| src/PulseOffice.Documents.UI/Editing/FolioObjectEditing.cs | 1-120 | `Change`/`Replace` are undoable: reused for links |
| src/PulseOffice.Drawing/Shapes/ShapeGroup.cs | all | `GroupChild.Hyperlink` kept, no helper to find or change it |
| src/PulseOffice.Platform/ShellOpening.cs | all | only http/https opened: the safe-scheme policy |
| src/PulseOffice.Sheets/PlacedObject.cs | all | `PlacedObject.Hyperlink` read from XLSX, never activated or edited in Ledger |
| src/PulseOffice.Sheets.UI/LedgerObjectCommands.cs | all | no object link commands |
| src/PulseOffice.Sheets.UI/GridParts/Input/GridPointerInput.cs | 1-100, 280-300 | no Ctrl/Cmd-click on object links |
| src/PulseOffice.Sheets.UI/LedgerViewParts/LedgerCellEditing.cs | 340-393 | name-box navigation inlined in the key handler: extracted to `GoTo` for link places |
| src/PulseOffice.Formats.Odf/OdpReader.cs | 195-210 | Deck: picture bullets declared lost (ODP103) |

## Changes

### Picture bullets (gap 1)

- `ListBulletPicture.ScalesWithFont`: Word draws a picture bullet at the height of the text, keeping its proportion; the DOCX
  reader sets it, ODT pictures keep their `fo:width`/`fo:height`.
- `ListPlacement.Picture` (from `ListLabeling`), `ListLabelPlacement.Picture`, `ShapedLabel.Picture`, `GlyphRun.Picture`: the
  picture flows through shaping and line composition; the label advance is the picture width (so the tab after the label is
  computed from it) and the line ascent grows to the picture height. `IBulletPictureSizing` resolves the size (font-relative or
  fixed, NaN/zero/huge bounded to 1–144 pt). The entry cache key (`ListPlacement.Key`) includes the picture, so changing it
  relays the paragraph.
- `PageRenderer` draws a label with a picture through `IPageSurface.Embedded` (screen, print and PDF share it) using
  `IBulletPictureObjects` (one `RasterImageObject` per picture data, weakly cached). The bullet character is still the label
  text, so text extraction and field facts are unchanged. With images hidden, a frame is drawn.
- Shared bullets dialog (`ListLevelsEditor`, used by Folio and Deck): `ListFeatures.Picture`, `ListLevelFormat.Picture`, a
  "Picture" row (preview, Picture…, Remove picture) shown for bullet levels when the module offers the feature;
  `IListPictureChooser` reads the file (4 MB cap, unreadable or too large reported in the status bar) and sizes it 12 pt high
  with the image proportion. Choosing a character preset drops the picture; a pictured level matches no preset; the sample
  draws the picture. Folio maps the picture both ways (`FolioListForms`), and the change goes through the existing
  `bullets-numbering` undo transaction.
- Deck: the model has no picture bullets (ODP103 declares the loss on read; PPTX `buBlip` falls back to the character). Deck
  does not offer `ListFeatures.Picture`, so its dialog is unchanged. Adding them to Deck is a separate block (model, ODP and
  PPTX, text renderer).

### Object and group-member hyperlinks (gap 2)

- `IGroupChildLinks` (Drawing, shared): member under a point (nested groups, topmost first, depth cap 64), own and effective
  link (nearest link from the member up), rewrite of one member's link.
- `ILinkFollower` (UI.Controls, shared): `#place` links go to the module's navigation, others through `IShellOpener`
  (http/https only); a refused link is reported in the status bar (`link.notOpened`, five languages). Folio text links now use
  it too.
- Folio: `IFolioObjectLinks` (pointed member remembered per editor on click, rotation and flips undone before hit testing);
  Ctrl/Cmd-click on an object with a link follows it (before selection); commands `folio.object.hyperlink` (shared hyperlink
  dialog, no text field), `folio.object.hyperlink.open`, `folio.object.hyperlink.remove`, enabled only with one selected,
  unlocked object (remove/open only when a link exists); object-level changes go through `IFolioObjectEditing.Change`, member
  changes through `Replace`: both undoable. Context menu "Object" and menu Insert ▸ Link.
- Ledger: Ctrl/Cmd-click on an object follows its link or its group member's link (places resolved like the name box,
  `Sheet.A1` accepted); commands `ledger.object.hyperlink`, `.open`, `.remove` in the object context menu and Insert ▸ Link,
  each change one undo step; protected sheets are covered by `LedgerProtectionGuard`.
- Deck: object links are interactions (`InteractiveAction`), already edited by the interaction dialog and run in the slide
  show; nothing to add.

## Tests

New: `PictureBulletLayoutTests` (Documents.Layout), `FolioBulletsAndObjectLinksTests` (Documents.UI, headless), `ListPictureAndLinkTests`
(UI.Controls, headless), `LedgerObjectLinkTests` (Sheets.UI). Run green: Documents.Layout 208, Documents.UI 307, UI.Controls 350,
Sheets.UI 427, Formats.OpenXml 2299, Architecture 40, Localization 32. Desktop.Tests: one failure that predates this round
(`folio.insert.dimensionLine` has no menu placement, the dimension-line work of round 35c).

Corpus audit: BulletAsImage.odt, linked-bullet.odt (linked picture missing: ODF178, as before), hyperlink_frame.ods: all OK.

## Remaining

- Deck picture bullets (model, ODP, PPTX, renderer): a block of its own.
- A DOCX picture bullet keeps no fixed size in Word (always scaled to text), and the font-relative flag is not stored in ODT.
- Ledger's "Open object hyperlink" command does not follow `#place` links (Ctrl/Cmd-click does).
