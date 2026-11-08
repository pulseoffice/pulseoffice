using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.OpenXml;

public sealed class VmlShapePresets
{
    private readonly IReadOnlyDictionary<int, ShapeKind> _shapes = new Dictionary<int, ShapeKind>
    {
        [1] = ShapeKind.Rectangle, [202] = ShapeKind.Rectangle, [2] = ShapeKind.RoundedRectangle, [3] = ShapeKind.Ellipse,
        [4] = ShapeKind.Diamond, [5] = ShapeKind.Triangle, [6] = ShapeKind.RightTriangle, [7] = ShapeKind.Parallelogram,
        [8] = ShapeKind.Trapezoid, [9] = ShapeKind.Hexagon, [10] = ShapeKind.Octagon, [11] = ShapeKind.Cross,
        [12] = ShapeKind.Star5, [13] = ShapeKind.ArrowRight, [19] = ShapeKind.Arc, [21] = ShapeKind.Plaque,
        [22] = ShapeKind.Cylinder, [23] = ShapeKind.Donut, [55] = ShapeKind.Chevron, [56] = ShapeKind.Pentagon,
        [57] = ShapeKind.NoSymbol, [58] = ShapeKind.Star8, [65] = ShapeKind.FoldedCorner, [66] = ShapeKind.ArrowLeft,
        [67] = ShapeKind.ArrowDown, [68] = ShapeKind.ArrowUp, [69] = ShapeKind.DoubleArrow, [73] = ShapeKind.LightningBolt,
        [74] = ShapeKind.Heart, [84] = ShapeKind.Bevel, [95] = ShapeKind.BlockArc,
        [96] = ShapeKind.SmileyFace, [183] = ShapeKind.Sun, [184] = ShapeKind.Moon, [185] = ShapeKind.BracketPair,
        [186] = ShapeKind.BracePair, [187] = ShapeKind.Star4,
        [109] = ShapeKind.FlowProcess, [110] = ShapeKind.FlowDecision, [111] = ShapeKind.FlowData,
        [112] = ShapeKind.FlowPredefinedProcess, [113] = ShapeKind.FlowInternalStorage, [114] = ShapeKind.FlowDocument,
        [115] = ShapeKind.FlowMultiDocument, [116] = ShapeKind.FlowTerminator, [117] = ShapeKind.FlowPreparation,
        [118] = ShapeKind.FlowManualInput, [119] = ShapeKind.FlowManualOperation, [120] = ShapeKind.FlowConnector,
        [121] = ShapeKind.FlowCard, [122] = ShapeKind.FlowPunchedTape, [123] = ShapeKind.FlowSummingJunction,
        [124] = ShapeKind.FlowOr, [125] = ShapeKind.FlowCollate, [126] = ShapeKind.FlowSort, [127] = ShapeKind.FlowExtract,
        [128] = ShapeKind.FlowMerge, [130] = ShapeKind.FlowStoredData, [131] = ShapeKind.FlowSequentialAccess,
        [132] = ShapeKind.FlowMagneticDisk, [133] = ShapeKind.FlowDirectAccess, [134] = ShapeKind.FlowDisplay,
        [135] = ShapeKind.FlowDelay, [176] = ShapeKind.FlowAlternateProcess, [177] = ShapeKind.FlowOffPageConnector,
    };

    private readonly IReadOnlyDictionary<int, string> _textWarps = new Dictionary<int, string>
    {
        [136] = "textPlain", [138] = "textTriangle", [144] = "textArchUp", [145] = "textArchDown", [146] = "textCircle",
        [148] = "textArchUp", [149] = "textArchDown", [150] = "textCircle", [156] = "textWave1", [157] = "textWave1",
        [158] = "textWave1", [159] = "textWave1", [172] = "textSlantUp", [173] = "textSlantDown",
    };

    public ShapeKind? Shape(int spt) => _shapes.TryGetValue(spt, out var kind) ? kind : null;

    public bool IsText(int spt) => spt is >= 136 and <= 175;

    public string TextWarp(int spt) => _textWarps.GetValueOrDefault(spt, "textPlain");
}
