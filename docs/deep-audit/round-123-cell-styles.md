# Deep audit round 123: cell styles residuals and ODP/PPTX writers

Scope: the six minor defects left by round 118 (docs/deep-audit/round-118-deck-styles.md), then a full read of the
writer files round 118 read only partially, plus `XlsxShapeStyleMapper.cs`.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| src/PulseOffice.Presentations.UI/DeckStyleCommands.cs | 60-200 (round 118 residual area) | defect 1 fixed |
| src/PulseOffice.Presentations.UI/DeckDesignActions.cs | 85-117 | defect 1 fixed (new `ApplyGraphicStyle`) |
| src/PulseOffice.Formats.Odf/Spreadsheet/SheetWriting.cs | 1-80, 255-300 of 420 | defects 2, 3, 4 fixed |
| src/PulseOffice.Formats.Odf/OdfStyleMapper.cs | 98/98 (before change) | defect 4 fixed |
| src/PulseOffice.Formats.Odf/CellStyles/Naming.cs | 228/228 | clean |
| src/PulseOffice.Formats.Odf/CellStyles/Inheritance.cs | 144/144 | clean |
| src/PulseOffice.Formats.Odf/CellStyles/Properties.cs | 208-250 (language, script) | defect 3 fixed |
| src/PulseOffice.Formats.OpenXml/CellStyles/Codecs.cs | 240-306 | defect 5 fixed |
| src/PulseOffice.Formats.OpenXml/XlsxStyleMapper.cs | 184/184 | defect 6 fixed |
| src/PulseOffice.Formats.Odf/Presentation/Writing/ElementWriters.cs | 361/361 | defects 7, 8 fixed |
| src/PulseOffice.Formats.Odf/Presentation/OdfMasterPages.cs | 460/460 | defects 8, 9 fixed |
| src/PulseOffice.Formats.Odf/Presentation/Writing/DocumentWriters.cs | 331/331 | defect 10 fixed (reader side) |
| src/PulseOffice.Formats.OpenXml/Presentation/Writing/PresentationWriters.cs | 357/357 | defects 11, 12 fixed |
| src/PulseOffice.Formats.OpenXml/XlsxShapeStyleMapper.cs | 174/174 | residual notes only |
| src/PulseOffice.Formats.Odf/Presentation/Writing/Foundation.cs | 1-120 | new `AddOwnMedia` |
| src/PulseOffice.Formats.OpenXml/ShapeStyles/OoxmlPendingImages.cs | 1-80 | clean (pending images resolved by the XLSX writer) |

## Defects

Minor (from round 118)

1. Deck «Apply style» collected the selection in a value-equality `HashSet`, so an identical copy of a selected shape was
   restyled too. The selection is now matched by reference (`DeckDesignActions.ApplyGraphicStyle`, called by
   `DeckStyleCommands`). Test: `DeckRound123StyleApplyTests` (run red).
2. ODS: the properties of the built-in `Default` cell style were not written (an empty `Default` was emitted). It is now
   declared with its properties and no parent; the reader already read them. Test:
   `OdsCellStyleRound123Tests.The_properties_of_the_default_style_survive_a_round_trip` (run red).
3. ODS: a cell under a named style with a language could not reset it. The cell's automatic style now writes the
   document language (or `zxx` when none is known), and `zxx` reads back as no language. Test:
   `A_cell_can_reset_the_language_of_its_named_style` (run red).
4. ODS: a General cell under a named style with a number format inherited that format. The automatic style now names a
   General data style (`OdfStyleMapper.GeneralDataStyle`). Test:
   `A_general_cell_under_a_named_style_with_a_number_format_stays_general` (run red).
5. XLSX: `ReadStyleNames` did not trim names, unlike `ReadNamedStyles`, and treated a non-built-in style called
   `Normal` differently. Both now go through one `ModelName`. Test:
   `XlsxCellStyleRound123Tests.A_style_name_with_surrounding_blanks_keeps_its_cell_links` (run red).
6. XLSX: `DeclareNamedStyles` assumed `Default` was the first catalog entry. `Default` is now written first, and a
   plain `Normal` is written at index 0 when the catalog has none. Tests: `The_default_style_is_written_first_even_when_it_is_not_first_in_the_catalog`,
   `A_catalog_without_a_default_style_still_writes_the_normal_style_first` (run red).

Medium (new)

7. ODP: a packaged object (OLE) was stored at `Objects/<id>/<FileName>` with `FileName` taken verbatim from `draw:name`
   or the PPTX object name: `../../content.xml` produced a path with `..` segments, a backslash name stayed as is, and
   an empty name produced a directory-like entry with data (`ElementWriters.cs:301`). `OdfUnits.PackageFileName` now
   removes separators and control characters and falls back to `object.bin`. Test:
   `OdpWritersRound123Tests.A_packaged_object_name_never_escapes_its_folder_and_survives` (run red, four cases).
8. ODP: media paths came from element ids and `AddMedia` kept the first entry for a path, so pictures with the same id
   on two masters (both read back as `deco.m1`), or on a master and a slide, silently took the first picture's bytes
   (`OdfMasterPages.cs:151`, `ElementWriters.cs:236`). The session's new `AddOwnMedia` reuses a path only for identical
   bytes and otherwise picks a free name; slide images, media, posters, vectors, packages and master pictures use it,
   and slide image and media paths are now made file-safe. Test:
   `Pictures_with_the_same_id_on_two_masters_and_a_slide_keep_their_own_data` (run red).
9. ODP: a layout's own master page (written when the layout has its own objects) always took a plain theme colour, so
   slides on that layout lost an explicit master colour, picture or gradient background in other applications
   (`OdfMasterPages.cs:120`). Without a layout background the page now repeats the master's page properties. Test:
   `A_layout_page_without_its_own_background_repeats_the_explicit_master_background` (run red).
10. ODP: custom shows were read by splitting `presentation:pages` on commas and spaces, so a slide whose name contains
    a space dropped out of the show (`SlideReading.cs:266`). The list is split on commas only and resolved by page
    name, with the space-separated form still accepted for foreign files. Test:
    `A_custom_show_keeps_a_slide_whose_name_contains_spaces` (run red).
11. PPTX: the show range could be written as start after end or past the last slide (`PresentationWriters.cs:114`);
    it is now clamped to the slides that exist. Test:
    `PptxPresentationRound123Tests.The_show_range_written_is_always_a_valid_range_of_existing_slides` (run red).

Minor (new)

12. PPTX: a 3D scene also triggered `PPTX004`, whose text speaks of form controls, beside the right `ODP102`. Test:
    `A_three_dimensional_scene_is_not_declared_as_a_dropped_form_control` (run red).

## Residual (not changed)

- `NamedStyleCatalog.Merge` inherits only font, background, borders, horizontal alignment and number format from a
  parent style: language, wrap, vertical alignment, rotation, indent and protection of a parent named style are not
  inherited by a child named style in the model (`src/PulseOffice.Sheets/NamedStyles.cs:85`). Outside this round's area.
- A slide name containing a comma still cannot be listed in an ODP custom show (the ODF attribute is comma separated).
- `XlsxShapeStyleMapper.Run` writes `lang="en-US"` on every shape run; `Line` casts the width to `int` EMU without a cap.
- PPTX section ids are new GUIDs on every save, so two saves of the same deck differ byte-wise.

## Tests

New: `tests/PulseOffice.Presentations.UI.Tests/DeckRound123StyleApplyTests.cs`,
`tests/PulseOffice.Formats.Odf.Tests/OdsCellStyleRound123Tests.cs`,
`tests/PulseOffice.Formats.Odf.Tests/OdpWritersRound123Tests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/XlsxCellStyleRound123Tests.cs`,
`tests/PulseOffice.Formats.OpenXml.Tests/PptxPresentationRound123Tests.cs`.

## To test live

- Ledger: change the look of the `Default` style, save ODS, open it in the reference suite: unstyled cells show it.
- Ledger: a cell under a German-language style set back to the default language, and a General cell under a
  percentage style; save ODS, reopen in Ledger and in the reference suite.
- Deck: two masters with a picture each, saved as ODP: each master keeps its own picture; a master with a colour
  background and a layout with its own shape: slides on that layout keep the colour in the reference suite.
- Deck: a custom show including a slide named with spaces, saved as ODP and reopened.
