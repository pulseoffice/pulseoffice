namespace PulseOffice.UI.Commands;

public enum RibbonDisplay { Auto, Large, Small, Icon, Tile }

public enum RibbonChoiceLook { List, Gallery, Band }

public abstract record RibbonControlSpec
{
    public string? MoreCommandId { get; init; }

    public abstract double InlineWidth { get; }

    public virtual bool FillsHeight => false;

    public virtual bool FollowsDisplay => false;
}

public sealed record RibbonPalette(IReadOnlyList<IReadOnlyList<string>> ThemeColumns, IReadOnlyList<string> Standard)
{
    public static readonly RibbonPalette Empty = new([], []);
}

public sealed record RibbonColor(string? Hex);

public sealed record ColorSplitSpec(Func<RibbonPalette> Palette, Func<string?> Current, string NoneKey) : RibbonControlSpec
{
    public Func<string?>? Selected { get; init; }

    public override double InlineWidth => 40;
}

public sealed record FontFamilySpec(Func<string?> Current) : RibbonControlSpec
{
    public double Width { get; init; } = 150;

    public override double InlineWidth => Width;
}

public sealed record NumberBoxSpec(
    Func<double?> Current, IReadOnlyList<double> Presets, double Minimum, double Maximum) : RibbonControlSpec
{
    public string Suffix { get; init; } = string.Empty;

    public double Width { get; init; } = 64;

    public override double InlineWidth => Width;
}

public enum RibbonSampleAlign { Start, Center, End }

public sealed record RibbonSample(string? FillHex, string? TextHex, bool Bold = false, bool Italic = false, string? BorderHex = null)
{
    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public bool Underline { get; init; }

    public bool Strikethrough { get; init; }

    public RibbonSampleAlign Align { get; init; } = RibbonSampleAlign.Center;
}

public abstract record RibbonThumbnail;

public sealed record RibbonChoice(string Key, string Title)
{
    public RibbonThumbnail? Thumbnail { get; init; }

    public string? IconKey { get; init; }

    public string? Preview { get; init; }

    public RibbonSample? Sample { get; init; }

    public string? Section { get; init; }
}

public sealed record ChoiceSpec(Func<IReadOnlyList<RibbonChoice>> Choices, Func<string?> Current) : RibbonControlSpec
{
    public RibbonChoiceLook Look { get; init; } = RibbonChoiceLook.List;

    public double Width { get; init; } = 132;

    public int InlineTiles { get; init; } = 4;

    public double TileWidth { get; init; } = 64;

    public RibbonBandSpec Band { get; init; } = RibbonBandSpec.Default;

    public override bool FillsHeight => Look == RibbonChoiceLook.Band;

    public override double InlineWidth => Look == RibbonChoiceLook.Gallery && InlineTiles > 0 ? InlineTiles * (TileWidth + 2) + 22 : Width;
}

public sealed record RibbonBorder(string Preset, string Line, string ColorHex);

public sealed record RibbonBorderPen(string Line, string ColorHex);

public sealed record BorderGallerySpec(
    IReadOnlyList<RibbonChoice> Presets,
    IReadOnlyList<RibbonChoice> Lines,
    Func<RibbonPalette> Palette,
    Func<RibbonBorderPen> Pen,
    Func<string> Last) : RibbonControlSpec
{
    public override double InlineWidth => 40;
}

public sealed record RibbonBandSpec(int Rows, int Columns, double Aspect)
{
    public static readonly RibbonBandSpec Default = new(1, 4, 16.0 / 9.0);

    public double PopoverTileWidth { get; init; } = 168;

    public int PopoverColumns { get; init; } = 4;

    public Func<double>? Proportion { get; init; }

    public double CurrentAspect =>
        Proportion is { } proportion && proportion() is var value && double.IsFinite(value) && value > 0 ? value : Aspect;
}

public sealed record RibbonTableSize(int Rows, int Columns);

public sealed record TableGridSpec(int MaxRows, int MaxColumns) : RibbonControlSpec
{
    public override double InlineWidth => 44;

    public override bool FollowsDisplay => true;
}

public sealed record SplitGallerySpec(Func<IReadOnlyList<RibbonChoice>> Choices, Func<string?> Current) : RibbonControlSpec
{
    public RibbonBandSpec Band { get; init; } = RibbonBandSpec.Default;

    public ChoiceSpec AsChoices() => new(Choices, Current) { Look = RibbonChoiceLook.Band, Band = Band, MoreCommandId = MoreCommandId };

    public override double InlineWidth => 72;

    public override bool FollowsDisplay => true;
}
