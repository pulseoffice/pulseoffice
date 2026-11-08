using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.Formatting;

public enum ConditionOperator
{
    Equal, NotEqual, GreaterThan, GreaterOrEqual, LessThan, LessOrEqual,
    Between, NotBetween,
    ContainsText, NotContainsText, BeginsWith, EndsWith,
    IsBlank, IsNotBlank, IsError, IsNotError,
    Duplicate, Unique,
    TopItems, BottomItems, TopPercent, BottomPercent,
    AboveAverage, BelowAverage, AboveOrEqualAverage, BelowOrEqualAverage,
    DateIs,
}

public enum DatePeriod
{
    Today, Yesterday, Tomorrow, Last7Days,
    ThisWeek, LastWeek, NextWeek,
    ThisMonth, LastMonth, NextMonth,
    ThisYear, LastYear, NextYear,
}

public enum ConditionalVisual
{
    Style,

    DataBar,

    ColorScale,

    IconSet,
}

public enum ConditionalThresholdKind
{
    Minimum, Maximum, Number, Percent, Percentile, Formula, AutomaticMinimum, AutomaticMaximum,
}

public enum DataBarAxis
{
    Automatic, Middle, None,
}

public enum DataBarDirection
{
    Context, LeftToRight, RightToLeft,
}

public sealed record DataBarOptions
{
    public bool Gradient { get; init; } = true;

    public DataBarAxis Axis { get; init; } = DataBarAxis.Automatic;

    public string AxisColorHex { get; init; } = "#000000";

    public string? BorderColorHex { get; init; }

    public string? NegativeBorderColorHex { get; init; }

    public DataBarDirection Direction { get; init; } = DataBarDirection.Context;

    public double MinimumLength { get; init; }

    public double MaximumLength { get; init; } = 100;

    public static readonly DataBarOptions Default = new();
}

public sealed record ConditionalThreshold(ConditionalThresholdKind Kind, double Value = 0, string? Formula = null, bool GreaterOrEqual = true);

public sealed record ConditionalIconChoice(string Set, int Index)
{
    public const string NoIcons = "NoIcons";

    public bool IsNone => Index < 0 || Set == NoIcons;
}

public sealed record ConditionalRule
{
    public required CellRange Range { get; init; }

    public IReadOnlyList<CellRange> AdditionalRanges { get; init; } = [];

    public IEnumerable<CellRange> Ranges => AdditionalRanges.Prepend(Range);

    public CellAddress Anchor => Range.Start;

    public bool Covers(CellAddress address) => Ranges.Any(range => range.Contains(address));

    public string RangeText => string.Join(' ', Ranges.Select(range => range.ToString()));

    public int Priority { get; init; }

    public ConditionalVisual Visual { get; init; } = ConditionalVisual.Style;

    public ConditionOperator Operator { get; init; } = ConditionOperator.GreaterThan;

    public IReadOnlyList<string> Operands { get; init; } = [];

    public DatePeriod DatePeriod { get; init; } = DatePeriod.Today;

    public int StandardDeviations { get; init; }

    public string? Formula { get; init; }

    public CellStyle Style { get; init; } = CellStyle.Default;

    public bool StopIfTrue { get; init; }

    public string BarColorHex { get; init; } = "#638EC6";

    public string? NegativeBarColorHex { get; init; }

    public DataBarOptions Bar { get; init; } = DataBarOptions.Default;

    public IReadOnlyList<string> ScaleColors { get; init; } = ["#F8696B", "#FFEB84", "#63BE7B"];

    public IReadOnlyList<ConditionalThreshold> Thresholds { get; init; } = [];

    public string IconSetName { get; init; } = "3Arrows";

    public bool ShowValue { get; init; } = true;

    public bool ReverseIcons { get; init; }

    public IReadOnlyList<ConditionalIconChoice> CustomIcons { get; init; } = [];

    public bool HasCustomIcons => CustomIcons.Count > 0;

    public int IconCount => IconSetName.Length > 0 && int.TryParse(IconSetName.AsSpan(0, 1), out var count) && count >= 3 ? count : 3;

    public string Describe() => Visual switch
    {
        ConditionalVisual.DataBar => $"DataBar {RangeText}",
        ConditionalVisual.ColorScale => $"ColorScale {RangeText}",
        ConditionalVisual.IconSet => $"IconSet {IconSetName} {RangeText}",
        _ => $"{DescribeOperator()} {RangeText}",
    };

    private string DescribeOperator() => Operator switch
    {
        ConditionOperator.Between or ConditionOperator.NotBetween => $"{Operator} {First} {Second}",
        ConditionOperator.ContainsText or ConditionOperator.NotContainsText
            or ConditionOperator.BeginsWith or ConditionOperator.EndsWith => $"{Operator} \"{First}\"",
        ConditionOperator.IsBlank or ConditionOperator.IsNotBlank
            or ConditionOperator.IsError or ConditionOperator.IsNotError
            or ConditionOperator.Duplicate or ConditionOperator.Unique
            or ConditionOperator.AboveAverage or ConditionOperator.BelowAverage
            or ConditionOperator.AboveOrEqualAverage or ConditionOperator.BelowOrEqualAverage => Operator.ToString(),
        ConditionOperator.DateIs => $"{Operator} {DatePeriod}",
        _ => $"{Operator} {First}",
    };

    private string First => Operands.Count > 0 ? Operands[0] : string.Empty;
    private string Second => Operands.Count > 1 ? Operands[1] : string.Empty;
}

public sealed record DataBarShape(double Start, double End, double? Axis, bool Gradient, string FillHex, string? BorderHex, string AxisColorHex);

public sealed record ConditionalAppearance
{
    public CellStyle? Style { get; init; }

    public string? ScaleColorHex { get; init; }

    public double? BarFraction { get; init; }

    public string? BarColorHex { get; init; }

    public DataBarShape? BarShape { get; init; }

    public string? IconSetName { get; init; }

    public int? IconIndex { get; init; }

    public bool HideValue { get; init; }

    public bool IsEmpty => Style is null && ScaleColorHex is null && BarFraction is null && IconIndex is null && !HideValue;

    public CellStyle Over(CellStyle baseStyle)
    {
        ArgumentNullException.ThrowIfNull(baseStyle);
        if (Style is not { } overlay) return baseStyle;

        return baseStyle with
        {
            BackgroundHex = overlay.BackgroundHex ?? baseStyle.BackgroundHex,
            Font = baseStyle.Font with
            {
                Bold = overlay.Font.Bold || baseStyle.Font.Bold,
                Italic = overlay.Font.Italic || baseStyle.Font.Italic,
                ColorHex = overlay.Font.ColorHex == CellFont.Default.ColorHex ? baseStyle.Font.ColorHex : overlay.Font.ColorHex,
            },
        };
    }

    public static readonly ConditionalAppearance None = new();
}
