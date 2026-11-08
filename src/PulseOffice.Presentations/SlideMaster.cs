using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record PlaceholderTemplate(PlaceholderRole Role, DocumentRect Bounds, TextParagraph Seed)
{
    public TextFrame Frame { get; init; } = TextFrame.Default;
}

public sealed record SlideLayoutTemplate
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required SlideLayout Kind { get; init; }

    public ColorRole? Background { get; init; }

    public IReadOnlyList<PlaceholderTemplate> Placeholders { get; init; } = [];

    public IReadOnlyList<SlideElement> Objects { get; init; } = [];

    public bool HideMasterObjects { get; init; }
}

public sealed record MasterElements(bool Date = true, bool Footer = true, bool SlideNumber = true)
{
    public static readonly MasterElements All = new();
}

public sealed record SlideMaster
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required Theme Theme { get; init; }

    public ColorRole Background { get; init; } = ColorRole.Background;

    public uint? BackgroundColor { get; init; }

    public PulseOffice.Drawing.Assets.RasterImageObject? BackgroundImage { get; init; }

    public PulseOffice.Drawing.Shapes.ShapeFill? BackgroundFill { get; init; }

    public BackgroundImageLayout BackgroundLayout { get; init; } = BackgroundImageLayout.Stretched;

    public uint BackgroundOf(Theme theme, SlideLayout kind) => BackgroundOf(theme, Template(kind));

    public uint BackgroundOf(Theme theme, Slide slide) => BackgroundOf(theme, Template(slide));

    private uint BackgroundOf(Theme theme, SlideLayoutTemplate layout) =>
        layout.Background is { } role ? theme.Colors.Resolve(role) : BackgroundColor ?? theme.Colors.Resolve(Background);

    public IReadOnlyList<SlideLayoutTemplate> Layouts { get; init; } = [];

    public MasterElements Elements { get; init; } = MasterElements.All;

    public IReadOnlyList<SlideElement> Objects { get; init; } = [];

    public IEnumerable<SlideElement> DecorationFor(SlideLayout kind) => DecorationOf(Template(kind));

    public IEnumerable<SlideElement> DecorationFor(Slide slide) => DecorationOf(Template(slide));

    private IEnumerable<SlideElement> DecorationOf(SlideLayoutTemplate layout) =>
        layout.HideMasterObjects ? layout.Objects : Objects.Concat(layout.Objects);

    public SlideLayoutTemplate Template(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return Template(slide.Layout, slide.LayoutId);
    }

    public SlideLayoutTemplate Template(SlideLayout kind, string? layoutId) =>
        layoutId is not null && Layouts.FirstOrDefault(l => l.Kind == kind && string.Equals(l.Id, layoutId, StringComparison.Ordinal)) is { } exact
            ? exact
            : Template(kind);

    public SlideLayoutTemplate Template(SlideLayout kind) =>
        Layouts.FirstOrDefault(l => l.Kind == kind)
            ?? Layouts.FirstOrDefault(l => l.Kind == SlideLayout.Blank)
            ?? new SlideLayoutTemplate { Id = "blank", Name = "Blank", Kind = SlideLayout.Blank };
}
