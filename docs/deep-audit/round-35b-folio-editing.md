# Deep audit round 35b: Folio editing of field ranges, picture bullets, text box looks, group hyperlinks

Continues [round 35](round-35-folio-ui.md).

## Coverage

| File | Outcome |
|---|---|
| src/PulseOffice.Documents/Marks/InlineMark.cs | read in full; `FieldCodeMark` code/separate parts are point marks that belong to a range |
| src/PulseOffice.Documents/Editing/ParagraphText.cs | read in full; field marks were never removed by a deletion (defect 3) |
| src/PulseOffice.Documents/Editing/RangeDeletion.cs | read in full; `MarkPurge` knew comments and controls, not fields (defect 3) |
| src/PulseOffice.Documents/Clipboard/FragmentExtractor.cs (Balance) | defect 2 |
| src/PulseOffice.Documents/Clipboard/FragmentRemapper.cs | defect 1 |
| src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxFieldRanges.cs | read in full; the writer drops unbalanced or duplicated ids, which turned defects 1-3 into silent field loss |

| src/PulseOffice.Documents/Styles/Styles.cs (ListLevel, ListBulletPicture) | the picture is model data only |
| src/PulseOffice.Documents.Layout/Flow/ListLabeling.cs | read in full; the label is text only, the picture never reaches layout (gap 1) |
| src/PulseOffice.Documents/Objects/AnchoredObject.cs (TextBoxContent, object hyperlink) | `Look` survives `WithStory`, `Clone`, link and unlink of chained frames: no defect |
| src/PulseOffice.Documents/Editing/ObjectEditing.cs (frame chains) | `Look` kept on link and unlink; changes go through `Replace`, undoable: no defect |
| src/PulseOffice.Documents.UI (object hyperlink search) | no command activates or edits an object or group-child hyperlink (gap 2) |

## Defects

1. **Serious** - `FragmentRemapper` kept the `FieldId` of pasted `FieldCodeMark`s: pasting a copied field twice (or next to
   its original) produced two ranges with the same id, and the DOCX writer, which drops ids it cannot balance, silently
   turned every copy into plain text. Pasted fields now get fresh `field` ids, as content controls get fresh `cc` ids.
   Test `FieldRangeEditingTests.PastingACopiedFieldTwiceGivesEachCopyItsOwnIdentifier` (fails before: one distinct id).
2. **Medium** - `FragmentExtractor.Balance` kept every point mark, so copying part of a field result carried the loose
   `Separate` or `Code` part without its `Begin`/`End`. `InlineMark.InsideRange` now marks the code and separate parts as
   members of a range, and they travel only with a balanced range. Test
   `CopyingPartOfAFieldResultCarriesNoLoosePartsOfIt` (fails before: the separate mark is in the fragment).
3. **Medium** - deletions never removed field marks: deleting a whole field left an invisible empty field that saved
   to both formats, and deleting the paragraph holding the start left an orphan end (ODT wrote it as is). A deletion
   that covers a field's begin and end in one paragraph now removes the field (`ParagraphText.CoveredFields`, same rule
   as content controls); a removed begin or end anywhere purges the rest of that field (`MarkPurge`), inside the same
   undo step. Deleting only the result keeps the field. Tests `DeletingAWholeFieldRemovesItsMarksAndUndoBringsThemBack`,
   `DeletingTheParagraphThatHoldsTheStartDropsTheRestOfTheField` (both fail before), `DeletingOnlyTheResultKeepsTheField`.

Field ranges have no update command (only the result text is shown), and typing inside them goes through the ordinary
mark shifting rules, so no update or find/replace path touches them; find/replace over the result edits plain text.

## Gaps left open (features, not contained fixes)

1. **Picture bullets** (`ListLevel.Picture`) are read and written but never drawn: `ListPlacement` carries text only,
   so the bullet character is drawn instead, and the bullets dialog cannot set or change the picture. Drawing it needs
   an image-carrying label through shaping, line composition, the page renderer and PDF export: a dedicated block.
2. **Object and group-child hyperlinks** are kept in the model and in both formats, but Folio has no click or
   Ctrl+click activation and no command to edit or remove them.
