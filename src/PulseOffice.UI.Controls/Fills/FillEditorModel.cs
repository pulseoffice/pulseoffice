using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.UI.Controls.Fills;

public enum FillPage
{
    None,
    Solid,
    Gradient,
    Pattern,
    Hatch,
    Image,
}

public sealed record FillEditorOptions(IReadOnlyList<FillPage> Pages)
{
    public bool ShowTransparency { get; init; } = true;

    public bool ShowPreview { get; init; } = true;
}

public sealed class FillEditorModel
{
    private readonly IGradientStopEditing _stops;
    private readonly IFillPatternCatalog _patterns;

    public FillEditorModel(ShapeFill initial, FillEditorOptions options, IGradientStopEditing stops, IFillPatternCatalog patterns)
    {
        Fill = initial ?? throw new ArgumentNullException(nameof(initial));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _stops = stops ?? throw new ArgumentNullException(nameof(stops));
        _patterns = patterns ?? throw new ArgumentNullException(nameof(patterns));
        if (options.Pages.Count == 0) throw new ArgumentException("At least one fill page is required.", nameof(options));
        Page = PageOf(initial);
        if (!options.Pages.Contains(Page)) Page = options.Pages.Contains(FillPage.Solid) ? FillPage.Solid : options.Pages[0];
    }

    public ShapeFill Fill { get; private set; }

    public FillEditorOptions Options { get; }

    public FillPage Page { get; private set; }

    public int SelectedStop { get; private set; }

    public IReadOnlyList<FillPattern> Patterns => _patterns.Patterns;

    public FillPattern? CurrentPattern => Fill.Kind == ShapeFillKind.Hatch ? _patterns.Match(Fill.Hatch) : null;

    public double Transparency => 1 - Fill.Opacity;

    public event EventHandler? Changed;

    public void ShowPage(FillPage page)
    {
        if (!Options.Pages.Contains(page)) return;
        Page = page;
        var kind = page switch
        {
            FillPage.None => ShapeFillKind.None,
            FillPage.Gradient => ShapeFillKind.Gradient,
            FillPage.Pattern or FillPage.Hatch => ShapeFillKind.Hatch,
            FillPage.Image when Fill.Bitmap is not null => ShapeFillKind.Bitmap,
            FillPage.Image => Fill.Kind,
            _ => ShapeFillKind.Solid,
        };
        var fill = Fill with { Kind = kind };
        if (page == FillPage.Pattern && _patterns.Match(fill.Hatch) is null) fill = fill with { Hatch = _patterns.Apply(_patterns.Patterns[0], fill.Hatch) };
        Set(fill);
    }

    public void SetColor(string hex) => Set(Fill with { ColorHex = hex });

    public void SetTransparency(double transparency)
    {
        if (double.IsFinite(transparency)) Set(Fill with { Opacity = Math.Clamp(1 - transparency, 0, 1) });
    }

    public void SetGradientKind(GradientKind kind) => Gradient(gradient => gradient with { Kind = kind });

    public void SetGradientAngle(double degrees)
    {
        if (double.IsFinite(degrees)) Gradient(gradient => gradient with { AngleDegrees = ((degrees % 360) + 360) % 360 });
    }

    public void SelectStop(int index)
    {
        SelectedStop = Math.Clamp(index, 0, Fill.Gradient.Stops.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void AddStop(double position)
    {
        if (!double.IsFinite(position)) return;
        var gradient = _stops.Add(Fill.Gradient, position);
        SelectedStop = Index(gradient, position);
        Set(Fill with { Kind = ShapeFillKind.Gradient, Gradient = gradient });
    }

    public void RemoveStop()
    {
        var gradient = _stops.Remove(Fill.Gradient, SelectedStop);
        SelectedStop = Math.Clamp(SelectedStop, 0, gradient.Stops.Count - 1);
        Set(Fill with { Gradient = gradient });
    }

    public void MoveStop(double position)
    {
        if (!double.IsFinite(position)) return;
        var color = Fill.Gradient.Stops[SelectedStop].ColorHex;
        var gradient = _stops.Move(Fill.Gradient, SelectedStop, position);
        SelectedStop = Index(gradient, Math.Clamp(position, 0, 1), color);
        Set(Fill with { Gradient = gradient });
    }

    public void RecolorStop(string hex) => Gradient(gradient => _stops.Recolor(gradient, SelectedStop, hex));

    public void ReverseGradient() => Gradient(_stops.Reverse);

    public void ApplyPattern(FillPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        Page = Options.Pages.Contains(FillPage.Pattern) ? FillPage.Pattern : Page;
        Set(Fill with { Kind = ShapeFillKind.Hatch, Hatch = _patterns.Apply(pattern, Fill.Hatch) });
    }

    public void SetHatch(Func<ShapeHatch, ShapeHatch> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Set(Fill with { Kind = ShapeFillKind.Hatch, Hatch = change(Fill.Hatch) });
    }

    public void SetHatchDistance(double points)
    {
        if (double.IsFinite(points)) SetHatch(hatch => hatch with { Distance = Twip.FromPoints(Math.Clamp(points, 1, 40)) });
    }

    public void SetImage(byte[] data, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(data);
        Set(Fill with { Kind = ShapeFillKind.Bitmap, Bitmap = new ShapeBitmap { Data = data, MediaType = mediaType, Tiled = Fill.Bitmap?.Tiled ?? false } });
    }

    public void SetTiled(bool tiled)
    {
        if (Fill.Bitmap is not { } bitmap) return;
        Set(Fill with { Bitmap = bitmap with { Tiled = tiled } });
    }

    private void Gradient(Func<ShapeGradient, ShapeGradient> change) =>
        Set(Fill with { Kind = ShapeFillKind.Gradient, Gradient = change(Fill.Gradient) });

    private int Index(ShapeGradient gradient, double position, string? color = null)
    {
        var best = 0;
        for (var i = 0; i < gradient.Stops.Count; i++)
        {
            var stop = gradient.Stops[i];
            if (Math.Abs(stop.Position - position) < 1e-9 && (color is null || stop.ColorHex == color)) return i;
            if (Math.Abs(stop.Position - position) < Math.Abs(gradient.Stops[best].Position - position)) best = i;
        }
        return best;
    }

    private FillPage PageOf(ShapeFill fill) => fill.Kind switch
    {
        ShapeFillKind.None => FillPage.None,
        ShapeFillKind.Gradient => FillPage.Gradient,
        ShapeFillKind.Hatch => _patterns.Match(fill.Hatch) is not null && Options.Pages.Contains(FillPage.Pattern) ? FillPage.Pattern : FillPage.Hatch,
        ShapeFillKind.Bitmap => FillPage.Image,
        _ => FillPage.Solid,
    };

    private void Set(ShapeFill fill)
    {
        Fill = fill;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
