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

    public uint? FirstBandBackground { get; init; }

    public TableBandAxes? BandAxes { get; init; }

    public TableBandAxes? FirstBandAxes { get; init; }

    public double? CellPadding { get; init; }

    public double? VerticalCellPadding { get; init; }

    public uint? HeaderColor { get; init; }

    public bool? InnerLines { get; init; }

    public TableRegionFormat? TotalRow { get; init; }

    public TableRegionFormat? FirstColumn { get; init; }

    public TableRegionFormat? LastColumn { get; init; }

    public TableRegionFormat? NorthWestCell { get; init; }

    public TableRegionFormat? NorthEastCell { get; init; }

    public TableRegionFormat? SouthWestCell { get; init; }

    public TableRegionFormat? SouthEastCell { get; init; }

    public TableRegionFormat? HeaderRow { get; init; }

    public TableRegionFormat? FirstBand { get; init; }

    public TableRegionFormat? SecondBand { get; init; }

    public TableRegionFormat? Region(TableRegion region) => region switch
    {
        TableRegion.TotalRow => TotalRow,
        TableRegion.FirstColumn => FirstColumn,
        TableRegion.LastColumn => LastColumn,
        TableRegion.NorthWestCell => NorthWestCell,
        TableRegion.NorthEastCell => NorthEastCell,
        TableRegion.SouthWestCell => SouthWestCell,
        TableRegion.HeaderRow => HeaderRow,
        TableRegion.FirstBand => FirstBand,
        TableRegion.SecondBand => SecondBand,
        _ => SouthEastCell,
    };

    public TableProperties WithRegion(TableRegion region, TableRegionFormat? format)
    {
        var kept = format is { IsEmpty: false } ? format : null;
        return region switch
        {
            TableRegion.TotalRow => this with { TotalRow = kept },
            TableRegion.FirstColumn => this with { FirstColumn = kept },
            TableRegion.LastColumn => this with { LastColumn = kept },
            TableRegion.NorthWestCell => this with { NorthWestCell = kept },
            TableRegion.NorthEastCell => this with { NorthEastCell = kept },
            TableRegion.SouthWestCell => this with { SouthWestCell = kept },
            TableRegion.HeaderRow => this with { HeaderRow = kept },
            TableRegion.FirstBand => this with { FirstBand = kept },
            TableRegion.SecondBand => this with { SecondBand = kept },
            _ => this with { SouthEastCell = kept },
        };
    }

    public TableProperties Over(TableProperties under) => new()
    {
        TotalRow = TableRegionFormat.Merge(TotalRow, under.TotalRow),
        FirstColumn = TableRegionFormat.Merge(FirstColumn, under.FirstColumn),
        LastColumn = TableRegionFormat.Merge(LastColumn, under.LastColumn),
        NorthWestCell = TableRegionFormat.Merge(NorthWestCell, under.NorthWestCell),
        NorthEastCell = TableRegionFormat.Merge(NorthEastCell, under.NorthEastCell),
        SouthWestCell = TableRegionFormat.Merge(SouthWestCell, under.SouthWestCell),
        SouthEastCell = TableRegionFormat.Merge(SouthEastCell, under.SouthEastCell),
        HeaderRow = TableRegionFormat.Merge(HeaderRow, under.HeaderRow),
        FirstBand = TableRegionFormat.Merge(FirstBand, under.FirstBand),
        SecondBand = TableRegionFormat.Merge(SecondBand, under.SecondBand),
        HeaderColor = HeaderColor ?? under.HeaderColor,
        InnerLines = InnerLines ?? under.InnerLines,
        BorderColor = BorderColor ?? under.BorderColor,
        BorderWidth = BorderWidth ?? under.BorderWidth,
        Background = Background ?? under.Background,
        HeaderBackground = HeaderBackground ?? under.HeaderBackground,
        HeaderBold = HeaderBold ?? under.HeaderBold,
        BandBackground = BandBackground ?? under.BandBackground,
        FirstBandBackground = FirstBandBackground ?? under.FirstBandBackground,
        BandAxes = BandBackground is null ? BandAxes ?? under.BandAxes : BandAxes,
        FirstBandAxes = FirstBandBackground is null ? FirstBandAxes ?? under.FirstBandAxes : FirstBandAxes,
        CellPadding = CellPadding ?? under.CellPadding,
        VerticalCellPadding = VerticalCellPadding ?? under.VerticalCellPadding,
    };
}

public enum TableRegion
{
    TotalRow,
    FirstColumn,
    LastColumn,
    NorthWestCell,
    NorthEastCell,
    SouthWestCell,
    SouthEastCell,
    HeaderRow,
    FirstBand,
    SecondBand,
}

public sealed record TableRegionFormat
{
    public const uint NoColour = 0x00FFFFFF;

    public uint? Background { get; init; }

    public uint? Ink { get; init; }

    public bool? Bold { get; init; }

    public bool? Italic { get; init; }

    public string? Font { get; init; }

    public double? FontSize { get; init; }

    public BoxBorders? Borders { get; init; }

    public bool IsEmpty => Background is null && Ink is null && Bold is null && Italic is null && Font is null && FontSize is null && Borders is null;

    public static TableRegionFormat? Merge(TableRegionFormat? over, TableRegionFormat? under) =>
        over is null ? under
        : under is null ? over
        : new TableRegionFormat
        {
            Background = over.Background ?? under.Background,
            Ink = over.Ink ?? under.Ink,
            Bold = over.Bold ?? under.Bold,
            Italic = over.Italic ?? under.Italic,
            Font = over.Font ?? under.Font,
            FontSize = over.FontSize ?? under.FontSize,
            Borders = MergeBorders(over.Borders, under.Borders),
        };

    public static BoxBorders? MergeBorders(BoxBorders? over, BoxBorders? under) =>
        over is null ? under
        : under is null ? over
        : over with { Top = over.Top ?? under.Top, Bottom = over.Bottom ?? under.Bottom, Left = over.Left ?? under.Left, Right = over.Right ?? under.Right };
}
