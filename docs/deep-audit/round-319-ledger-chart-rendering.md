# Round 319: Ledger chart rendering and editing

Area: rendering of every chart type against Excel (axes, ticks, gridlines, label placement, stacking, 100% stacking,
negative values, legends, data labels), moving and resizing charts, and the chart editor per type. The chart codecs
were left to round 318.

## Defects found and fixed

| Severity | Defect | Fix |
|---|---|---|
| Medium | Stacked and 100% stacked areas filled every series down to the baseline, so the translucent fills overlapped and the lower bands took mixed colours instead of Excel's separate bands. | `LinePlot` fills each stacked series down to the series beneath it on the same axis (`AreaOutline`). |
| Medium | Data labels of combination (column and line) charts used the slots of all series, so column labels sat beside their columns and line labels off the line. | Column labels use the drawn column slots, line labels the category centre. |
| Medium | Pie and doughnut data labels carried no series or point index, so "category name" and "percentage" label options showed nothing. | Pie labels carry series 0 and their slice index and are centred in the slice. |
| Medium | Stacked bar and column labels sat at the segment end, overlapping the next segment; Excel centres them. | `ChartLabelPlacement.Centre` with the segment middle. |
| Medium | Labels of negative columns were drawn inside the bar end, and labels of horizontal bars over the bar end; Excel places them outside the end (below a negative column, beside a bar). | Placement Above, Below, After or Before from the sign against the axis crossing; `ChartLabelLayout` turns it into an origin. |
| Medium | Radar rings were four fixed fractions of the radius, unrelated to the value scale, with no value or category labels and ignoring fixed axis bounds. | Rings on the scale divisions, value labels on the first spoke, category labels outside the spokes, axis bounds honoured. |
| Medium | Moving or nudging a chart (or any object) past A1 gave negative offsets, which XLSX and ODS cannot hold. | `GridViewport.ChangeOf` keeps the box inside the sheet. |
| Minor | 100% stacked value axes read 0 to 100 without a percent sign. | Automatic axis format `0"%"` for the four 100% stacked types when no format is set. |
| Minor | An area with only negative values filled to the scale minimum's clamp rather than to the zero line in every case. | The baseline is zero clamped into the scale. |
| Minor | The chart editor offered axis titles, gridlines and axis scales for pie, doughnut, hierarchy and meter types, where they do nothing. | Disabled when the type draws no axes (`ChartRenderer.DrawsAxes`). |

## Still open (queued)

- Resizing an object past the sheet's left or top edge keeps the size, so the opposite edge moves; Excel stops the
  dragged edge. Needs the gesture to say which edge moves.
- Radar charts honour value axis bounds in rendering, but the editor disables the axis page for them.
- Pie of pie and bar of pie draw as plain pies; legends of horizontal stacked bars list series top to bottom in data
  order, while Excel reverses them to match the stack.
- Data labels have no per-chart position option (inside end, inside base, outside end, best fit) in the model; the
  placement is Excel's default per type.
- Chart styles and colour palettes (the Excel chart style gallery) and keyboard resizing of charts were not audited
  in this round; stock, statistical, hierarchy and meter plots were read only for label and axis consistency.
