# Round 304: Ledger conditional formatting leftovers

Area: the items of round 299 queued for Ledger conditional formatting. No builds or tests were run in the round (audit
cycle rule); the coordinator runs them at the join.

## Defects

| # | Severity | Defect | Status |
|---|---|---|---|
| 1 | Medium | ODS writing emitted only the extension element for conditional formats: ODF readers without it saw no conditional formatting at all. | Fixed |
| 2 | Medium | `style:map` reading ignored cells that take a mapped style only through a column's `table:default-cell-style-name`. | Fixed |
| 3 | Medium | Rules manager, "this selection" scope: Up/Down swapped the rule with the next visible one, so the neighbour jumped over the hidden rules between them and their priorities changed. | Fixed |
| 4 | Minor | Duplicate inserted the copy above the original. | Fixed |
| 5 | Medium | Rule editor had no data bar negative colour, border, axis, axis colour or direction controls, and no icon set per-icon thresholds or icons (kept only when read from files). | Fixed |
| 6 | Minor | Top/bottom count accepted up to 1000 for percent too, with only the generic missing-value warning. | Fixed |
| 7 | Minor | Transpose of mixed absolute references in pasted rule formulas. | Open: needs Excel |

## Fixes

1. `OdfConditionalMapper.StyleMapGroups` turns every rule written as a plain condition (comparisons, between, not
   between, formula) into `style:map` elements (`of:cell-content()>5`, `of:cell-content-is-between(…)`,
   `of:is-true-formula(…)`), grouped as the extension groups them. `OdsStyleMapCells` post-processes the row spans
   before they are emitted: it splits repeated rows and cells at the rule range edges, pads short rows up to the range,
   and gives every cell in a range a derived automatic style (`cfN`) that copies its own style, its column default or
   `Default`, plus the maps of every group that covers it. Declarations are produced after the body, when all base
   automatic styles are known. Rule types ODF cannot express in `style:map` (top/bottom, average, text, dates,
   duplicates, visuals) stay only in the extension. On reading, the extension still wins: the maps are read only when
   the extension element is absent, so our own files are not read twice.
2. `OdsStyleMaps.Collect` resolves each cell's style as its own, then the row default, then the column default, and
   splits unstyled repeated cells at column default boundaries.
3. Up/Down move only the selected rule: before the previous visible rule or after the next one; hidden rules keep their
   order relative to the neighbour. Duplicate inserts the copy after the original and selects it.
4. `ConditionalVisualOptions` (new, Sheets.UI) builds the extra bar controls and an icon rules grid: one row per icon,
   highest first, with an icon picker over every icon of every set plus "no icon", comparison (>= or >), value and type
   (number, percent, percentile, formula); the lowest row reads "otherwise". Picking an icon materialises `CustomIcons`
   in effective order and clears reversal; changing set or reversal clears custom icons and rebuilds the rows.
5. The top/bottom field limit follows the operator (100 for percent, 1000 for items) and the editor rebuilds when the
   operator changes; confirmation shows "conditional.error.percent" or "conditional.error.count".

## Open

- Transposed paste of rule formulas with mixed absolute references keeps the current behaviour (relative parts shifted
  by the cell move); Excel and the reference suite were not available to compare.
- Derived `cfN` styles give every cell of a conditional range an explicit style in ODS, including empty padded cells;
  watch file size on whole-column rules and the effect on the reader's style blocks.
- The icon picker lists all icons in one long combo box; a grouped gallery would read better.
