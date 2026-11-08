using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public sealed record CharacterProperties
{
    public static readonly CharacterProperties None = new();

    public string? Font { get; init; }

    public double? FontSize { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public bool? Underline { get; init; }

    public bool? DoubleUnderline { get; init; }

    public bool? Strikethrough { get; init; }

    public bool? Overline { get; init; }

    public bool? Shadow { get; init; }

    public bool? Outline { get; init; }

    public bool? SmallCaps { get; init; }

    public TextBaseline? Baseline { get; init; }

    public uint? Color { get; init; }

    public uint? Highlight { get; init; }

    public string? Language { get; init; }

    public double? LetterSpacing { get; init; }

    public double? ScaleWidth { get; init; }

    public bool? Kerning { get; init; }

    public TextCase? Case { get; init; }

    public FontRelief? Relief { get; init; }

    public bool? Hidden { get; init; }

    public bool? RightToLeft { get; init; }

    public CharacterProperties Over(CharacterProperties under) => new()
    {
        Font = Font ?? under.Font,
        FontSize = FontSize ?? under.FontSize,
        Bold = Bold ?? under.Bold,
        Italic = Italic ?? under.Italic,
        Underline = Underline ?? under.Underline,
        DoubleUnderline = DoubleUnderline ?? under.DoubleUnderline,
        Strikethrough = Strikethrough ?? under.Strikethrough,
        Overline = Overline ?? under.Overline,
        Shadow = Shadow ?? under.Shadow,
        Outline = Outline ?? under.Outline,
        SmallCaps = SmallCaps ?? under.SmallCaps,
        Baseline = Baseline ?? under.Baseline,
        Color = Color ?? under.Color,
        Highlight = Highlight ?? under.Highlight,
        Language = Language ?? under.Language,
        LetterSpacing = LetterSpacing ?? under.LetterSpacing,
        ScaleWidth = ScaleWidth ?? under.ScaleWidth,
        Kerning = Kerning ?? under.Kerning,
        Case = Case ?? under.Case,
        Relief = Relief ?? under.Relief,
        Hidden = Hidden ?? under.Hidden,
        RightToLeft = RightToLeft ?? under.RightToLeft,
    };
}

public sealed record ParagraphProperties
{
    public static readonly ParagraphProperties None = new();

    public TextAlign? Align { get; init; }

    public double? LineSpacing
    {
        get;
        init => field = value is { } factor ? LineSpacingFactor.Bounded(factor) : null;
    }

    public double? SpaceBefore { get; init; }

    public double? SpaceAfter { get; init; }

    public double? MarginLeft { get; init; }

    public double? MarginRight { get; init; }

    public double? Indent { get; init; }

    public bool? KeepWithNext { get; init; }

    public bool? KeepTogether { get; init; }

    public int? Widows { get; init; }

    public int? Orphans { get; init; }

    public bool? PageBreakBefore { get; init; }

    public bool? ColumnBreakBefore { get; init; }

    public string? PageStyleName { get; init; }

    public int? PageNumberRestart { get; init; }

    public IReadOnlyList<TabStop>? TabStops { get; init; }

    public ParagraphLineHeight? LineHeight { get; init; }

    public ParagraphHyphenation? Hyphenation { get; init; }

    public bool? ContextualSpacing { get; init; }

    public PulseOffice.Text.Rich.Bidi.TextDirection? Direction { get; init; }

    public uint? Background { get; init; }

    public BoxBorders? Borders { get; init; }

    public DropCap? DropCap { get; init; }

    public ParagraphProperties Over(ParagraphProperties under) => new()
    {
        Align = Align ?? under.Align,
        LineSpacing = LineSpacing ?? under.LineSpacing,
        SpaceBefore = SpaceBefore ?? under.SpaceBefore,
        SpaceAfter = SpaceAfter ?? under.SpaceAfter,
        MarginLeft = MarginLeft ?? under.MarginLeft,
        MarginRight = MarginRight ?? under.MarginRight,
        Indent = Indent ?? under.Indent,
        KeepWithNext = KeepWithNext ?? under.KeepWithNext,
        KeepTogether = KeepTogether ?? under.KeepTogether,
        Widows = Widows ?? under.Widows,
        Orphans = Orphans ?? under.Orphans,
        PageBreakBefore = PageBreakBefore ?? under.PageBreakBefore,
        ColumnBreakBefore = ColumnBreakBefore ?? under.ColumnBreakBefore,
        PageStyleName = PageStyleName ?? under.PageStyleName,
        PageNumberRestart = PageNumberRestart ?? under.PageNumberRestart,
        TabStops = TabStops ?? under.TabStops,
        LineHeight = LineHeight ?? under.LineHeight,
        Hyphenation = Hyphenation ?? under.Hyphenation,
        ContextualSpacing = ContextualSpacing ?? under.ContextualSpacing,
        Direction = Direction ?? under.Direction,
        Background = Background ?? under.Background,
        Borders = Borders ?? under.Borders,
        DropCap = DropCap ?? under.DropCap,
    };
}

public sealed record FrameProperties
{
    public static readonly FrameProperties None = new();

    public Objects.WrapMode? Wrap { get; init; }

    public uint? Background { get; init; }

    public uint? BorderColor { get; init; }

    public double? BorderWidth { get; init; }

    public double? Padding { get; init; }

    public Objects.HorizontalRelation? Horizontal { get; init; }

    public Objects.VerticalRelation? Vertical { get; init; }

    public FrameProperties Over(FrameProperties under) => new()
    {
        Wrap = Wrap ?? under.Wrap,
        Background = Background ?? under.Background,
        BorderColor = BorderColor ?? under.BorderColor,
        BorderWidth = BorderWidth ?? under.BorderWidth,
        Padding = Padding ?? under.Padding,
        Horizontal = Horizontal ?? under.Horizontal,
        Vertical = Vertical ?? under.Vertical,
    };
}

public sealed record TableProperties
{
    public static readonly TableProperties None = new();

    public uint? BorderColor { get; init; }

    public double? BorderWidth { get; init; }

    public uint? Background { get; init; }

    public uint? HeaderBackground { get; init; }

    public bool? HeaderBold { get; init; }

    public uint? BandBackground { get; init; }

    public double? CellPadding { get; init; }

    public double? VerticalCellPadding { get; init; }

    public uint? HeaderColor { get; init; }

    public bool? InnerLines { get; init; }

    public TableProperties Over(TableProperties under) => new()
    {
        HeaderColor = HeaderColor ?? under.HeaderColor,
        InnerLines = InnerLines ?? under.InnerLines,
        BorderColor = BorderColor ?? under.BorderColor,
        BorderWidth = BorderWidth ?? under.BorderWidth,
        Background = Background ?? under.Background,
        HeaderBackground = HeaderBackground ?? under.HeaderBackground,
        HeaderBold = HeaderBold ?? under.HeaderBold,
        BandBackground = BandBackground ?? under.BandBackground,
        CellPadding = CellPadding ?? under.CellPadding,
        VerticalCellPadding = VerticalCellPadding ?? under.VerticalCellPadding,
    };
}
