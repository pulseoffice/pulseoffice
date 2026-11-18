# Round 181: the shared chart editor edits the whole chart model

Closes the editor and rendering items left open by rounds 168, 172 and 178. Not built nor run in this round (the
coordinator builds and tests after the join).

## Editor (`PulseOffice.UI.Controls/Charts`, shared by Ledger, Folio and Deck)

The options column of `ChartPickerView` is now five tabs on the shared `PageTabStrip` instead of one long form:

- **General**: title, axis titles, legend, grid lines.
- **Axes** (`ChartAxisScaleEditor`): an axis chooser (category, value, secondary value) over one form with minimum,
  maximum, major and minor unit, logarithmic (not offered on the category axis), reversed, where the other axis crosses
  (automatic, minimum, maximum, value) with the crossing value, and the axis number format. Each axis keeps its own
  working copy, so switching axes loses nothing; properties the form does not show (logarithm base) are kept.
- **Series** (`ChartSeriesFormatEditor`): a series chooser over line width in points, dash, marker symbol and size,
  and "plot on the secondary axis"; below it the existing analysis section (trendline and error bars for all series).
- **Labels** (`ChartDataLabelEditor`): show data labels, then value, percentage, category name, series name, position
  and number format.
- **Data**: range, series orientation, header row, per-series ranges.

The small field helpers live in `ChartEditorFields` (an instance holding texts, metrics and culture). All new texts are
in the five languages.

`ChartSettings` gained `CategoryAxisScale`, `SecondaryValueAxisScale`, `DataLabels` and `SeriesFormats`.
`ChartSettingsMapper.Apply` merges each series' line, marker and secondary-axis fields into the chart's own series
format, leaving trendlines and error bars to the analysis path, and rebuilds the format list only when a series really
changed, so an unchanged dialog still returns an equal chart (no empty undo step). Automatic scales and values-only
label options are normalised to their shared instances. The modules already turn the editor's result into one undo
step (`ChartEditor`, Ledger's chart commands), so every confirmed edit stays one step in all three modules.

## Rendering

- **Secondary axis on stacked and horizontal bars**: `BarPlot` now splits stacked charts too; primary and secondary
  series form two stacks side by side within each category, each on its own scale. Horizontal bar charts draw the
  secondary value labels along the top (the plot area reserves the row), vertical charts on the right as before.
- **Axis crossing**: the new pure service `ChartAxisMarks` (Charts) gives the value where the category axis crosses
  (automatic = zero clamped to the scale, minimum, maximum, value) and the category boundary where the value axis
  crosses. Bars and columns grow from the value crossing; the renderer draws both axis lines when a crossing is not
  automatic.
- **Minor ticks**: drawn as short marks inside the plot edge at each minor unit that is not a major division, on the
  value, secondary value and scatter category axes (not on logarithmic axes).

## Tests

`tests/PulseOffice.Charts.Tests/Round181ChartSettingsTests.cs` (unchanged settings give an equal chart, axis scales,
series formats merged without touching analysis, data label options), and
`tests/PulseOffice.UI.Controls.Tests/Round181ChartGeometryTests.cs` (secondary scales on stacked column, stacked bar
and bar, stacked secondary placement, value and category crossings, minor ticks).

## Open

- Category axis labels stay at the plot edge when the category axis crosses elsewhere (only the axis line moves).
- Stacked line and area charts still do not split onto a secondary axis; trendlines and error bars are still not drawn
  on horizontal bar charts.
- The reversed flag of the category axis is kept and written but not drawn.
- No test yet for the DOCX/PPTX/ODT/ODP chart loss declarations (from round 172).
