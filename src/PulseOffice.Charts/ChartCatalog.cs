namespace PulseOffice.Charts;

public enum ChartType
{
    Column, StackedColumn, PercentStackedColumn, ColumnLine,
    Bar, StackedBar, PercentStackedBar,

    Line, SmoothLine, SteppedLine, StackedLine, PercentStackedLine,
    Area, StackedArea, PercentStackedArea,

    Pie, Doughnut, ExplodedPie, PieOfPie, BarOfPie,
    Treemap, Sunburst, Funnel, Waterfall,

    Scatter, ScatterWithLines, Bubble,

    Histogram,
    Pareto,
    BoxPlot,
    ViolinPlot,
    DensityPlot,
    QuantileQuantile,
    CumulativeDistribution,
    StemAndLeaf,
    DotPlot,
    BeeswarmPlot,

    ControlChart,
    RunChart,

    Radar, FilledRadar, ParallelCoordinates, Heatmap, Contour, Surface, Surface3D,

    StockHighLowClose,
    StockOpenHighLowClose,
    Candlestick,

    Gauge, Bullet, Gantt, Sankey, Network, Polar, PopulationPyramid,
}
