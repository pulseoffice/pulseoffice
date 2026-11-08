namespace PulseOffice.Drawing.Shapes;

public interface IShapePresets
{
    string OdfName(ShapeKind kind);

    string OoxmlName(ShapeKind kind);

    ShapeKind KindOf(string? name);

    bool Knows(string? name);
}

public sealed class ShapePresets : IShapePresets
{
    private static readonly Dictionary<ShapeKind, (string Odf, string Ooxml)> Names = new()
    {
        [ShapeKind.Rectangle] = ("rectangle", "rect"),
        [ShapeKind.RoundedRectangle] = ("round-rectangle", "roundRect"),
        [ShapeKind.Ellipse] = ("ellipse", "ellipse"),
        [ShapeKind.Triangle] = ("isosceles-triangle", "triangle"),
        [ShapeKind.RightTriangle] = ("right-triangle", "rtTriangle"),
        [ShapeKind.Diamond] = ("diamond", "diamond"),
        [ShapeKind.Parallelogram] = ("parallelogram", "parallelogram"),
        [ShapeKind.Trapezoid] = ("trapezoid", "trapezoid"),
        [ShapeKind.Pentagon] = ("pentagon", "pentagon"),
        [ShapeKind.Hexagon] = ("hexagon", "hexagon"),
        [ShapeKind.Octagon] = ("octagon", "octagon"),
        [ShapeKind.Star4] = ("star4", "star4"),
        [ShapeKind.Star5] = ("star5", "star5"),
        [ShapeKind.Star6] = ("star6", "star6"),
        [ShapeKind.Star8] = ("star8", "star8"),
        [ShapeKind.ArrowRight] = ("right-arrow", "rightArrow"),
        [ShapeKind.ArrowLeft] = ("left-arrow", "leftArrow"),
        [ShapeKind.ArrowUp] = ("up-arrow", "upArrow"),
        [ShapeKind.ArrowDown] = ("down-arrow", "downArrow"),
        [ShapeKind.DoubleArrow] = ("left-right-arrow", "leftRightArrow"),
        [ShapeKind.Chevron] = ("chevron", "chevron"),
        [ShapeKind.Cross] = ("cross", "plus"),
        [ShapeKind.Heart] = ("heart", "heart"),
        [ShapeKind.Cylinder] = ("can", "can"),
        [ShapeKind.Callout] = ("rectangular-callout", "wedgeRectCallout"),
        [ShapeKind.Line] = ("line", "line"),
        [ShapeKind.FlowProcess] = ("flowchart-process", "flowChartProcess"),
        [ShapeKind.FlowAlternateProcess] = ("flowchart-alternate-process", "flowChartAlternateProcess"),
        [ShapeKind.FlowDecision] = ("flowchart-decision", "flowChartDecision"),
        [ShapeKind.FlowData] = ("flowchart-data", "flowChartInputOutput"),
        [ShapeKind.FlowPredefinedProcess] = ("flowchart-predefined-process", "flowChartPredefinedProcess"),
        [ShapeKind.FlowInternalStorage] = ("flowchart-internal-storage", "flowChartInternalStorage"),
        [ShapeKind.FlowDocument] = ("flowchart-document", "flowChartDocument"),
        [ShapeKind.FlowMultiDocument] = ("flowchart-multidocument", "flowChartMultidocument"),
        [ShapeKind.FlowTerminator] = ("flowchart-terminator", "flowChartTerminator"),
        [ShapeKind.FlowPreparation] = ("flowchart-preparation", "flowChartPreparation"),
        [ShapeKind.FlowManualInput] = ("flowchart-manual-input", "flowChartManualInput"),
        [ShapeKind.FlowManualOperation] = ("flowchart-manual-operation", "flowChartManualOperation"),
        [ShapeKind.FlowConnector] = ("flowchart-connector", "flowChartConnector"),
        [ShapeKind.FlowOffPageConnector] = ("flowchart-off-page-connector", "flowChartOffpageConnector"),
        [ShapeKind.FlowCard] = ("flowchart-card", "flowChartPunchedCard"),
        [ShapeKind.FlowPunchedTape] = ("flowchart-punched-tape", "flowChartPunchedTape"),
        [ShapeKind.FlowSummingJunction] = ("flowchart-summing-junction", "flowChartSummingJunction"),
        [ShapeKind.FlowOr] = ("flowchart-or", "flowChartOr"),
        [ShapeKind.FlowCollate] = ("flowchart-collate", "flowChartCollate"),
        [ShapeKind.FlowSort] = ("flowchart-sort", "flowChartSort"),
        [ShapeKind.FlowExtract] = ("flowchart-extract", "flowChartExtract"),
        [ShapeKind.FlowMerge] = ("flowchart-merge", "flowChartMerge"),
        [ShapeKind.FlowStoredData] = ("flowchart-stored-data", "flowChartOnlineStorage"),
        [ShapeKind.FlowDelay] = ("flowchart-delay", "flowChartDelay"),
        [ShapeKind.FlowSequentialAccess] = ("flowchart-sequential-access", "flowChartMagneticTape"),
        [ShapeKind.FlowMagneticDisk] = ("flowchart-magnetic-disk", "flowChartMagneticDisk"),
        [ShapeKind.FlowDirectAccess] = ("flowchart-direct-access-storage", "flowChartMagneticDrum"),
        [ShapeKind.FlowDisplay] = ("flowchart-display", "flowChartDisplay"),
        [ShapeKind.SmileyFace] = ("smiley", "smileyFace"),
        [ShapeKind.Sun] = ("sun", "sun"),
        [ShapeKind.Moon] = ("moon", "moon"),
        [ShapeKind.LightningBolt] = ("lightning", "lightningBolt"),
        [ShapeKind.Cloud] = ("cloud", "cloud"),
        [ShapeKind.NoSymbol] = ("forbidden", "noSmoking"),
        [ShapeKind.Donut] = ("ring", "donut"),
        [ShapeKind.BlockArc] = ("block-arc", "blockArc"),
        [ShapeKind.FoldedCorner] = ("paper", "foldedCorner"),
        [ShapeKind.Bevel] = ("quad-bevel", "bevel"),
        [ShapeKind.Frame] = ("frame", "frame"),
        [ShapeKind.Plaque] = ("plaque", "plaque"),
        [ShapeKind.BracketPair] = ("bracket-pair", "bracketPair"),
        [ShapeKind.BracePair] = ("brace-pair", "bracePair"),
        [ShapeKind.Arc] = ("mso-spt19", "arc"),
        [ShapeKind.Pie] = ("pie", "pie"),
        [ShapeKind.Chord] = ("chord", "chord"),
    };

    private static readonly Dictionary<string, ShapeKind> Kinds = BuildKinds();

    private static Dictionary<string, ShapeKind> BuildKinds()
    {
        var kinds = new Dictionary<string, ShapeKind>(StringComparer.OrdinalIgnoreCase);

        foreach (var (kind, names) in Names)
        {
            kinds[names.Odf] = kind;
            kinds[names.Ooxml] = kind;
        }

        return kinds;
    }

    public string OdfName(ShapeKind kind) =>
        Names.TryGetValue(kind, out var names) ? names.Odf : "rectangle";

    public string OoxmlName(ShapeKind kind) =>
        Names.TryGetValue(kind, out var names) ? names.Ooxml : "rect";

    public bool Knows(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (Kinds.ContainsKey(name)) return true;

        var dash = name.IndexOf('-');
        return dash >= 0 && Kinds.ContainsKey(name[(dash + 1)..]);
    }

    public ShapeKind KindOf(string? name)
    {
        if (string.IsNullOrEmpty(name)) return ShapeKind.Rectangle;

        if (Kinds.TryGetValue(name, out var kind)) return kind;

        var dash = name.IndexOf('-');

        return dash >= 0 && Kinds.TryGetValue(name[(dash + 1)..], out var stripped)
            ? stripped
            : ShapeKind.Rectangle;
    }
}
