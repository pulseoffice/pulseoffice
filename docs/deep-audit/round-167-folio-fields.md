# Round 167: Folio fields, indexes and references

Scope: indexes (composition, update, change), index entry marks through ODT and DOCX, cross-references to headings,
captions and bookmarks. Rounds 105 and 106 covered the field kinds, field dialog and mail merge; this round read what
they left: `IndexEditing`, `IndexEntries`, `ReferenceTargets`, `ReferenceFieldSource`, the index mark parts of the ODT
body reader and writer and of the DOCX inline reader and writer.

No build or test was run by this audit (coordinator rule for the cycle); the tests below run at the join.

## Defects

- **D1 (medium, fidelity)** Contents index marks (`text:toc-mark`) were written to DOCX as `XE` fields, so in Word and
  after reading back they became alphabetical index entries; user index marks lost their index name the same way, and
  the level of an ODF `text:toc-mark` (`text:outline-level`) was dropped on reading. Fixed: `IndexEntryMark.Level`;
  contents marks cross as `TC "entry" \l n` and user marks as `XE "path" \f "name"`, both read back; ODT reads and
  writes `text:outline-level`; the contents index places marked entries at their level and honours its level limit.
  `TC` joins the structural field keywords, so it is no longer preserved as an opaque field. A ranged contents mark
  becomes a point `TC` in DOCX (Word has no ranged `TC`); the entry text is kept. Tests `Round167IndexMarkTests`.
- **D2 (medium)** Changing an index (`IndexEditing.Change`) rebuilt its section copying only protection and columns:
  the protection password, hidden flag, condition and linked file were lost. Fixed: `SectionBlock.WithContent` copies
  every property and is shared with `Clone`. Test `Round167IndexChangeTests`.
- **D3 (medium)** Heading cross-references named their bookmark `__RefHeading__<paragraph id>`. Paragraph ids are
  regenerated when a document is opened, so a new reference to a heading could take a name already carried by another
  heading: `Find` returned the first one and the reference showed the wrong heading; the field dialog also derived the
  target from the name. Fixed: `ReferenceTargets` reuses the heading's own bookmark, makes new names unique, and
  `HeadingOf` resolves a name to its heading for the dialog. Test `Round167HeadingReferenceTests`.

## Remaining

- Index entries and caption lists take paragraph text including tracked deletions; whether pending deletions should be
  left out of a regenerated index needs a decision on how the comparable word processors behave.
- Caption references prepare and check the first sequence field of a paragraph, not the one of the chosen category
  (a caption with two sequence fields). Minor, not changed.
- Outline entries of a contents index link by heading text (`#text|outline`); two headings with the same text link to
  the first. Minor, not changed.
