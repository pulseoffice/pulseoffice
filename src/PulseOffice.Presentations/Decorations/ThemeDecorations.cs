namespace PulseOffice.Presentations.Decorations;

public sealed record LayoutDecoration(IReadOnlyList<SlideElement> Objects, bool HidesMaster);

public sealed record MasterDecoration(IReadOnlyList<SlideElement> Master, IReadOnlyDictionary<SlideLayout, LayoutDecoration> Layouts)
{
    public static readonly MasterDecoration Empty = new([], new Dictionary<SlideLayout, LayoutDecoration>());
}

public interface IThemeDecoration
{
    ThemeDecorationKind Kind { get; }

    MasterDecoration Build(ColorScheme colors);
}

public interface IThemeDecorations
{
    MasterDecoration Build(Theme theme);
}

public sealed class ThemeDecorations : IThemeDecorations
{
    private readonly Dictionary<ThemeDecorationKind, IThemeDecoration> _decorations;

    public ThemeDecorations()
        : this([
            new SideBandDecoration(), new TopBarDecoration(), new BottomStripesDecoration(), new CornersDecoration(),
            new ArcsDecoration(), new WashDecoration(), new FrameDecoration(), new MosaicDecoration(),
            new OrbsDecoration(), new SliceDecoration(),
        ])
    {
    }

    public ThemeDecorations(IEnumerable<IThemeDecoration> decorations)
    {
        _decorations = decorations.ToDictionary(decoration => decoration.Kind);
        var missing = Enum.GetValues<ThemeDecorationKind>().Where(kind => kind != ThemeDecorationKind.None && !_decorations.ContainsKey(kind)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Theme decorations not registered: {string.Join(", ", missing)}");
    }

    public MasterDecoration Build(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return _decorations.TryGetValue(theme.Decoration, out var decoration) ? decoration.Build(theme.Colors) : MasterDecoration.Empty;
    }
}

public abstract class ThemeDecoration : IThemeDecoration
{
    public abstract ThemeDecorationKind Kind { get; }

    public MasterDecoration Build(ColorScheme colors)
    {
        ArgumentNullException.ThrowIfNull(colors);
        var master = new DecorationCanvas("deco.m");
        var title = new DecorationCanvas("deco.t");
        var section = new DecorationCanvas("deco.s");
        Master(master, colors);
        TitleSlide(title, colors);
        Section(section, colors);
        return new MasterDecoration(master.Elements, new Dictionary<SlideLayout, LayoutDecoration>
        {
            [SlideLayout.TitleSlide] = new(title.Elements, true),
            [SlideLayout.SectionHeader] = new(section.Elements, true),
        });
    }

    protected abstract void Master(DecorationCanvas canvas, ColorScheme colors);

    protected abstract void TitleSlide(DecorationCanvas canvas, ColorScheme colors);

    protected abstract void Section(DecorationCanvas canvas, ColorScheme colors);
}

public sealed class SideBandDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.SideBand;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, 0.9, DecorationCanvas.Height, colors.Accent1)
            .Rect(0.9, 0, 0.12, DecorationCanvas.Height, colors.Accent2);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, 1.9, DecorationCanvas.Height, colors.Accent1)
            .Rect(1.9, 0, 0.18, DecorationCanvas.Height, colors.Accent2)
            .Rect(2.5, 5.5, 4.0, 0.14, colors.Accent1)
            .Rect(2.5, 12.4, 28.87, 0.04, colors.Text2, 0.4);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, 1.9, DecorationCanvas.Height, colors.Accent1)
            .Rect(1.9, 0, 0.18, DecorationCanvas.Height, colors.Accent2)
            .Rect(2.5, 7.3, 3.2, 0.16, colors.Accent2);
}

public sealed class TopBarDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.TopBar;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, DecorationCanvas.Width, 0.45, colors.Accent1)
            .Rect(0, 0.45, 8.5, 0.12, colors.Accent2)
            .Rect(1.9, 3.62, 30.0, 0.04, colors.Accent1, 0.55);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, DecorationCanvas.Width, 4.6, colors.Accent1)
            .Rect(0, 4.6, 12.0, 0.22, colors.Accent2)
            .Rect(0, DecorationCanvas.Height - 0.35, DecorationCanvas.Width, 0.35, colors.Accent1)
            .Rect(13.93, 12.5, 6.0, 0.06, colors.Accent2);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Rect(0, 0, DecorationCanvas.Width, 6.6, colors.Accent1)
            .Rect(0, 6.6, 9.0, 0.22, colors.Accent2)
            .Rect(0, DecorationCanvas.Height - 0.35, DecorationCanvas.Width, 0.35, colors.Accent1);
}

public sealed class BottomStripesDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.BottomStripes;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) => Stripes(canvas, colors, 0.3);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors)
    {
        canvas.Rect(0, 13.4, DecorationCanvas.Width, DecorationCanvas.Height - 13.4, colors.Accent1);
        canvas.Rect(0, 12.9, 18.0, 0.5, colors.Accent2).Rect(18.0, 12.9, 8.0, 0.5, colors.Accent3)
            .Rect(26.0, 12.9, DecorationCanvas.Width - 26.0, 0.5, colors.Accent4);
        canvas.Rect(2.5, 5.6, 4.5, 0.12, colors.Accent1);
    }

    protected override void Section(DecorationCanvas canvas, ColorScheme colors)
    {
        Stripes(canvas, colors, 0.9);
        canvas.Rect(2.5, 7.4, 4.5, 0.12, colors.Accent1);
    }

    private void Stripes(DecorationCanvas canvas, ColorScheme colors, double height)
    {
        var top = DecorationCanvas.Height - height;
        canvas.Rect(0, top, 20.0, height, colors.Accent1)
            .Rect(20.0, top, 7.0, height, colors.Accent2)
            .Rect(27.0, top, DecorationCanvas.Width - 27.0, height, colors.Accent3);
    }
}

public sealed class CornersDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Corners;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Corner(Corner.TopRight, 2.6, colors.Accent1)
            .Corner(Corner.TopRight, 1.4, colors.Accent2)
            .Corner(Corner.TopLeft, 1.3, colors.Accent3, 0.85);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Corner(Corner.TopRight, 11.0, 5.6, colors.Accent1)
            .Corner(Corner.TopRight, 6.5, 3.3, colors.Accent2)
            .Corner(Corner.BottomLeft, 10.0, 6.4, colors.Accent3, 0.9)
            .Corner(Corner.BottomLeft, 5.5, 3.5, colors.Accent1);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Corner(Corner.TopRight, 13.0, 6.8, colors.Accent1)
            .Corner(Corner.TopRight, 7.5, 3.9, colors.Accent2)
            .Corner(Corner.BottomLeft, 4.0, 4.0, colors.Accent3, 0.85);
}

public sealed class ArcsDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Arcs;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(30.6, 15.6, 7.0, 7.0, colors.Accent1, 0.22)
            .Ring(29.4, 14.4, 9.4, 9.4, colors.Accent2, 1.5, 0.6);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(23.5, -11.0, 16.0, 16.0, colors.Accent1)
            .Ring(21.5, -13.0, 20.0, 20.0, colors.Accent2, 2.5)
            .Ellipse(-7.0, 12.6, 14.0, 14.0, colors.Accent2, 0.85)
            .Ring(-9.0, 10.6, 18.0, 18.0, colors.Accent1, 1.5, 0.6);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(-8.0, 13.6, 14.0, 14.0, colors.Accent1)
            .Ring(-10.0, 11.6, 18.0, 18.0, colors.Accent2, 2.0)
            .Ellipse(28.5, -4.0, 9.0, 9.0, colors.Accent2, 0.35);
}

public sealed class WashDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Wash;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Gradient(0, 0, DecorationCanvas.Width, DecorationCanvas.Height, colors.Background, colors.Background2, 135)
            .Rect(1.9, 3.62, 3.5, 0.08, colors.Accent1)
            .Rect(0, DecorationCanvas.Height - 0.12, DecorationCanvas.Width, 0.12, colors.Accent1, 0.7);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Gradient(0, 0, DecorationCanvas.Width, DecorationCanvas.Height,
                canvas.Blend(colors.Background, colors.Accent1, 0.08), canvas.Blend(colors.Background2, colors.Accent2, 0.22), 125)
            .Ellipse(22.0, -9.0, 18.0, 14.0, colors.Accent1, 0.12)
            .Rect(13.93, 5.6, 6.0, 0.1, colors.Accent1)
            .Rect(0, DecorationCanvas.Height - 0.2, DecorationCanvas.Width, 0.2, colors.Accent1);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Gradient(0, 0, DecorationCanvas.Width, DecorationCanvas.Height,
                colors.Background2, canvas.Blend(colors.Background2, colors.Accent1, 0.2), 90)
            .Rect(2.5, 7.4, 4.0, 0.1, colors.Accent1)
            .Rect(0, DecorationCanvas.Height - 0.2, DecorationCanvas.Width, 0.2, colors.Accent1);
}

public sealed class FrameDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Frame;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Frame(0.45, 0.45, DecorationCanvas.Width - 0.9, DecorationCanvas.Height - 0.9, colors.Accent1, 1.25)
            .Rect(1.9, 3.62, 2.5, 0.06, colors.Accent2);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Frame(0.45, 0.45, DecorationCanvas.Width - 0.9, DecorationCanvas.Height - 0.9, colors.Accent1, 3)
            .Frame(0.85, 0.85, DecorationCanvas.Width - 1.7, DecorationCanvas.Height - 1.7, colors.Accent2, 0.75)
            .Rect(14.43, 5.6, 5.0, 0.08, colors.Accent1)
            .Rect(14.43, 12.3, 5.0, 0.08, colors.Accent1);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Frame(0.45, 0.45, DecorationCanvas.Width - 0.9, DecorationCanvas.Height - 0.9, colors.Accent1, 3)
            .Rect(1.3, 7.9, 0.3, 5.4, colors.Accent2);
}

public sealed class MosaicDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Mosaic;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors)
    {
        var accents = Accents(colors);
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 2; column++)
                canvas.Rect(32.1 + column * 0.8, 0.4 + row * 0.8, 0.65, 0.65, accents[(row * 2 + column) % accents.Length], 0.55 + 0.15 * ((row + column) % 3));
    }

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors)
    {
        var accents = Accents(colors);
        for (var row = 0; row < 3; row++)
            for (var column = 0; column < 6; column++)
                canvas.Rect(24.3 + column * 1.6, 13.0 + row * 1.6, 1.4, 1.4, accents[(row * 5 + column * 2) % accents.Length], 0.45 + 0.18 * ((row * 2 + column) % 4));
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 4; column++)
                canvas.Rect(0.4 + column * 1.0, 0.4 + row * 1.0, 0.85, 0.85, accents[(row + column * 3) % accents.Length], 0.5 + 0.2 * ((row + column) % 3));
    }

    protected override void Section(DecorationCanvas canvas, ColorScheme colors)
    {
        var accents = Accents(colors);
        for (var row = 0; row < 11; row++)
            for (var column = 0; column < 2; column++)
                canvas.Rect(0.3 + column * 0.9, 0.3 + row * 1.68, 0.75, 1.5, accents[(row * 3 + column) % accents.Length], 0.4 + 0.2 * ((row + column) % 4));
    }

    private uint[] Accents(ColorScheme colors) =>
        [colors.Accent1, colors.Accent2, colors.Accent3, colors.Accent4, colors.Accent5, colors.Accent6];
}

public sealed class OrbsDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Orbs;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(32.0, 0.35, 1.3, 1.3, colors.Accent1, 0.85)
            .Ellipse(32.75, 1.85, 0.75, 0.75, colors.Accent2, 0.85)
            .Ellipse(31.85, 2.25, 0.45, 0.45, colors.Accent3, 0.85);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(26.5, -3.5, 9.0, 9.0, colors.Accent1, 0.9)
            .Ellipse(23.4, 1.2, 4.0, 4.0, colors.Accent2, 0.85)
            .Ellipse(31.0, 4.0, 2.2, 2.2, colors.Accent3, 0.8)
            .Ellipse(-2.5, 13.2, 7.0, 7.0, colors.Accent2, 0.75)
            .Ellipse(4.0, 15.6, 2.6, 2.6, colors.Accent1, 0.7);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Ellipse(25.5, 12.5, 11.0, 11.0, colors.Accent1, 0.9)
            .Ellipse(23.0, 13.8, 3.2, 3.2, colors.Accent2, 0.85)
            .Ellipse(29.8, 1.2, 2.4, 2.4, colors.Accent3, 0.8);
}

public sealed class SliceDecoration : ThemeDecoration
{
    public override ThemeDecorationKind Kind => ThemeDecorationKind.Slice;

    protected override void Master(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Slant(24.0, DecorationCanvas.Height - 0.42, 12.0, 0.42, colors.Accent1)
            .Slant(20.5, DecorationCanvas.Height - 0.42, 4.6, 0.42, colors.Accent2);

    protected override void TitleSlide(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Slant(15.0, 0, 26.0, 5.4, colors.Accent1)
            .Slant(25.5, 0, 14.0, 3.6, colors.Accent2, 0.9)
            .Slant(-6.0, 13.6, 22.0, 5.45, colors.Accent2, 0.9, mirrored: true);

    protected override void Section(DecorationCanvas canvas, ColorScheme colors) =>
        canvas.Slant(-4.0, 14.2, 44.0, 4.85, colors.Accent1)
            .Slant(18.0, 13.6, 20.0, 0.6, colors.Accent2);
}
