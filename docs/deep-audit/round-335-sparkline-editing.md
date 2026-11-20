# Round 335: Ledger sparkline editing, dialog options and foreign sources

Area: the sparkline items left open by round 333 (edit group, group and ungroup; dialog options; sources on another
sheet under structure edits and sheet removal; rendering tests).

## Defects found

| Severity | Defect | Fix |
|---|---|---|
| Serious | A sparkline whose source is on another sheet kept its range when rows or columns were inserted or deleted on that sheet, so it drew the wrong cells; a source naming its own host sheet explicitly did not follow either | `SparklinesPart` walks every sheet's groups: cells move only on the edited host, sources and date ranges move whenever their sheet (explicit or host) is the edited one; a source deleted whole drops the sparkline, a date range deleted whole drops the dates |
| Serious | Local date ranges did not follow row and column edits at all | Same part |
| Medium | Deleting the source sheet left sparklines pointing at a missing sheet (drawn empty, written to files as a dangling reference) | `ChartSheetReferences.Removed` plans a `SparklineSourceChange` per owner: sparklines whose source was on the removed sheet are dropped and dates on it cleared; undoing the deletion restores them. The delete-sheet command and the script `RemoveSheet` use this plan already |
| Medium | No command to edit an existing group (Excel's Edit Group Data and the design tab options) | `ledger.sparkline.edit` (menu, context menu, ribbon) reopens the dialog filled from the group (data, location and dates described by `ISparklineGroups.Describe`, sheet-qualified when foreign) and replaces the group in one undo step |
| Medium | No group and ungroup | `ledger.sparkline.group` merges every group touched by the selection into one, taking the options of the group at the active cell; `ledger.sparkline.ungroup` gives each sparkline of the touched groups its own group with the same options |
| Medium | The dialog had no custom minimum and maximum, no axis colour, no high/low/first/last colours, no date range | `SparklineBoundField` (automatic per sparkline / same for all / custom value) for each bound; swatch strips for axis and the four point colours; an optional date range field; the result carries them and the command writes them to the group |
| Medium | The data field could not take a range on another sheet | The command reads the data and dates with the sheet-aware reader; `ISparklineGroups.Lay` (Engine, injectable) builds the lines with `SourceSheet` and `DateSheet` and clips to the used range of the source sheet; the preview reads the foreign sheet too |
| Minor | Date spacing, right to left, axis and per-point colours had no rendering test | `Round335SparklineRenderingTests` renders 60×20 bitmaps and checks pixels |

## Tests

- `Sheets.Engine.Tests/Round335SparklineSourceTests`: foreign source and dates follow row insertion on the source
  sheet; host insertion moves the cell only; deleting the source rows drops the sparkline; local source and dates
  shrink with column deletion; removing the source sheet drops and undo restores; removing the date sheet clears dates.
- `Sheets.Engine.Tests/Round335SparklineGroupsTests`: layout from another sheet with sheet-qualified description;
  grouping takes the active group's options; ungrouping keeps options and gives distinct ids.
- `Sheets.UI.Tests/Round335SparklineRenderingTests`: date positions, right to left, axis at zero in its colour,
  high and low column colours.
- `Desktop.Tests/LedgerFormatSurfacesTests`: the dialog in edit mode starts from the group, previews a foreign source
  with its custom minimum, and returns bounds, dates and right to left.

## Still open

- Edit Group Data describes the group's data and location as the bounding ranges of its sparklines; a group whose
  sparklines were not laid out as one block (for example after Group) is rewritten as one block when edited.
- A sparkline whose source sheet or rows are deleted is dropped, where Excel keeps it with a `#REF!` source and draws
  nothing; undo restores it.
- Ungroup and Group are in the menu and ribbon but not in the cell context menu.
