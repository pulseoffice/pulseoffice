using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Formulas;
using PulseOffice.UI.Controls.Formulas;

namespace PulseOffice.Formulas.Editor;

public sealed class FormulaSample : PulseOffice.UI.Controls.Theming.ThemedSurface
{
    private readonly IFormulaRenderer _renderer;
    private readonly IFormulaParser _parser;

    public FormulaSample(IFormulaParser? parser = null, IFormulaRenderer? renderer = null)
    {
        _parser = parser ?? new FormulaParser();
        _renderer = renderer ?? new FormulaRenderer();
    }

    private MathFormula _formula = MathFormula.Empty;

    public double GlyphSize { get; set; } = 20;

    public void Show(string source)
    {
        _formula = _parser.Parse(source ?? string.Empty);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size).Deflate(4);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var ink = this.FindResource("PulseTextPrimary") as IBrush ?? Brushes.Black;

        _renderer.Render(context, _formula, bounds, GlyphSize, ink);
    }
}
