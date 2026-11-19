# Round 271: Folio table and TOC leftovers from round 269 (2026-11-19)

Area: the Folio tables (269) and Folio TOC (269) queue items. No build or test ran in the round (audit cycle rule);
the coordinator builds and tests at the join.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | Dragging a column edge of a percent table added the on-screen distance to the stored (unscaled) width, so the edge landed off the pointer by the scale factor | The drag keeps the displayed table width; `ITableGeometryEditing.StoredColumnDelta` turns the displayed distance into stored points before `ResizeColumn`, which then scales the percent. |
| Medium | Editing a table style from the styles panel did nothing: `FolioStyleDialogs.EditAsync` had no table case, and no editor existed for table style properties | `IFolioTableStyleForm` (tabs Borders, Header row, Bands: line colour and width, lines between cells, background, header background, text colour and bold, first and second band shading with their axes) through the shared tabbed form, colour and choice fields; only values that differ from the resolved style enter the style, in one `style-edit` step. |
| Medium | A cell side set to `nil` or `none` in DOCX lost to the table line its neighbour inherited, so the line Word hides was drawn | `ICellInheritedEdges` runs after DOCX cell borders are read: an explicit empty side clears the facing side of a neighbour whose side was only inherited from the table or its style. Two explicit sides still go through the weight rule of the conflict resolution. |
| Medium | A merged cell whose segments resolved to different lines kept one line for its whole side, drawn over the neighbours' segments | `CellBorderConflicts` leaves the side empty on the merged cell when every segment has a neighbour and the winners differ; each neighbour draws its own segment. |
| Medium | Automatic width ignored spanning cells and nested tables | `TableMeasure` spreads the excess of a spanning cell's content over the columns it spans, in proportion to their widths (narrower spans first), and measures a nested automatic table by its own measured columns (a fixed nested table by its stored width). |
| Minor | Word's preferred cell widths (`tcW`) of automatic tables did not take part | DOCX reading raises a grid column to the single-span `dxa` preferred width of an automatic table, so it acts as the preferred minimum. |
| Minor | The TOC styles tab could not assign a style not yet used | The tab adds an "Add a style" choice over the other visible paragraph styles and a level for it (`IFolioIndexStyleCandidates.Others`). |

Tests: `Round269TableWidthEditingTests.A_drag_on_a_scaled_percent_table_keeps_the_edge_under_the_pointer`,
`Round269CellBorderSpanTests.A_merged_cell_whose_segments_differ_leaves_each_segment_to_its_neighbour`,
`Round271CellInheritedEdgesTests` (Documents), `Round271TableMeasureTests` (Layout, one shared fixture),
`Round271TableStyleFormTests`, `Round269IndexStyleCandidatesTests.A_style_not_yet_used_is_offered_for_assignment`
(Documents.UI).

## Research and decisions

- ECMA-376 resolves conflicting cell borders by weight, with `nil` and `none` carrying no weight, so between two
  explicit sides a line still wins; what Word hides is the table line (`insideV`, `insideH`, outer sides) that a cell
  overrides, which is the case fixed here. ODT stores concrete borders per cell, so it has no inheritance to settle.
- Header and total rows, first and last columns are table look flags (`TableBanding`, `HeaderRows`), already chosen in
  the table style gallery; the style model has no separate total-row or first/last-column formatting.

## Still open (queued)

- A derived table style cannot clear an inherited colour to none from the editor (an empty colour means inherit).
- The style model has no total-row and first/last-column formatting (Word's `lastRow`, `firstCol`, `lastCol`
  conditional formats); only the header row and the bands are styled.
- Automatic width: nested percent tables count with their stored width; `tcW` in percent units is not used; the result
  is not compared with Word.
- The DOCX nil-side pass is tested on the model service, not through a DOCX file.
