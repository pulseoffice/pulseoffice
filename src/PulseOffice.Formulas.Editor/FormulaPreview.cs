using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Formulas;
using PulseOffice.UI.Controls.Formulas;

namespace PulseOffice.Formulas.Editor;

public sealed class FormulaPreview : PulseOffice.UI.Controls.Theming.ThemedSurface
{
    private readonly IFormulaRenderer _renderer;

    private IReadOnlyList<FormulaHit> _hits = [];
    private MathFormula _formula = MathFormula.Empty;
    private MathNode? _highlight;
    private bool _repainting;

    public event EventHandler<MathNode>? NodePicked;

    public double GlyphSize { get; set; } = 30;

    public FormulaPreview(IFormulaRenderer? renderer = null)
    {
        Focusable = false;
        _renderer = renderer ?? new FormulaRenderer();
    }

    public void Show(MathFormula formula)
    {
        _formula = formula;
        InvalidateVisual();
    }

    public void Highlight(MathNode? node)
    {
        if (ReferenceEquals(node, _highlight)) return;

        _highlight = node;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);

        var picked = _hits
            .Where(hit => hit.Bounds.Contains(point))
            .OrderBy(hit => hit.Bounds.Width * hit.Bounds.Height)
            .Select(hit => (MathNode?)hit.Node)
            .FirstOrDefault();

        if (picked is null) return;

        Highlight(picked);
        NodePicked?.Invoke(this, picked);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size).Deflate(10);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var ink = this.FindResource("PulseTextPrimary") as IBrush ?? Brushes.Black;

        var marked = _highlight is null ? null : Rect();

        if (marked is { } area && this.FindResource("PulseAccent") is ISolidColorBrush accent)
            context.FillRectangle(new SolidColorBrush(accent.Color, 0.18), area.Inflate(3), 4);

        var painted = marked is not null;

        _hits = _renderer.RenderMapped(context, _formula, bounds, GlyphSize, ink);

        if (painted || _highlight is null || _repainting) return;
        if (Rect() is null) return;

        _repainting = true;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _repainting = false;
            InvalidateVisual();
        });
    }

    private Rect? Rect() =>
        _hits.Where(hit => ReferenceEquals(hit.Node, _highlight))
            .Select(hit => (Rect?)hit.Bounds)
            .FirstOrDefault();
}
