using Avalonia;
using Avalonia.Media;
using PulseOffice.Formulas;

namespace PulseOffice.UI.Controls.Formulas;

public readonly record struct FormulaHit(MathNode Node, Rect Bounds);

public interface IFormulaRenderer
{
    Size Measure(MathFormula formula, double size);

    void Render(DrawingContext context, MathFormula formula, Rect bounds, double size, IBrush ink);

    IReadOnlyList<FormulaHit> RenderMapped(
        DrawingContext context, MathFormula formula, Rect bounds, double size, IBrush ink);
}

public sealed class FormulaRenderer : IFormulaRenderer
{
    private readonly IFormulaLayout _layout;

    public FormulaRenderer(IFormulaLayout layout) => _layout = layout;

    public FormulaRenderer() : this(new FormulaLayout(new TypefaceMetrics())) { }

    public Size Measure(MathFormula formula, double size)
    {
        ArgumentNullException.ThrowIfNull(formula);

        var box = _layout.Measure(formula.Root, size);

        return new Size(box.Width, box.Height);
    }

    private List<FormulaHit>? _hits;

    public IReadOnlyList<FormulaHit> RenderMapped(
        DrawingContext context, MathFormula formula, Rect bounds, double size, IBrush ink)
    {
        var collected = new List<FormulaHit>();

        _hits = collected;

        try
        {
            Render(context, formula, bounds, size, ink);
        }
        finally
        {
            _hits = null;
        }

        return collected;
    }

    public void Render(
        DrawingContext context, MathFormula formula, Rect bounds, double size, IBrush ink)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(formula);
        ArgumentNullException.ThrowIfNull(ink);

        if (bounds.Width <= 2 || bounds.Height <= 2) return;

        var measured = Measure(formula, size);

        var scale = measured.Width <= 0 || measured.Height <= 0
            ? 1
            : System.Math.Min(bounds.Width / measured.Width, bounds.Height / measured.Height);

        scale = System.Math.Min(scale, 1);

        var transform = Matrix.CreateScale(scale, scale)
            * Matrix.CreateTranslation(bounds.X + 2, bounds.Y + 2);

        using (context.PushTransform(transform))
        {
            new FormulaPainter(context, _layout, ink, _hits).Draw(formula.Root, 0, 0, size);
        }

        if (_hits is not { Count: > 0 }) return;

        for (var index = 0; index < _hits.Count; index++)
        {
            var hit = _hits[index];
            var topLeft = hit.Bounds.TopLeft.Transform(transform);
            var bottomRight = hit.Bounds.BottomRight.Transform(transform);

            _hits[index] = hit with { Bounds = new Rect(topLeft, bottomRight) };
        }
    }

    private sealed class TypefaceMetrics : IGlyphMetrics
    {
        public double Width(string text, double size)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            var formatted = new FormattedText(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Times New Roman")),
                size,
                Brushes.Black);

            return formatted.WidthIncludingTrailingWhitespace;
        }

        public double Height(double size) => size * 1.15;
    }
}

internal sealed class FormulaPainter(DrawingContext context, IFormulaLayout layout, IBrush ink, List<FormulaHit>? hits)
{
    public IFormulaLayout Layout => layout;

    public DrawingContext Context => context;

    public IBrush Ink => ink;

    public void Draw(MathNode node, double x, double y, double size)
    {
        var box = layout.Measure(node, size);
        hits?.Add(new FormulaHit(node, new Rect(x, y, box.Width, box.Height)));
        node.Accept(new NodePainter(this, x, y, size, box));
    }

    public double Aligned(double y, MathBox parent, MathBox child) => y + System.Math.Max(0, parent.Baseline - child.Baseline);

    public void Text(string text, double x, double y, double size, bool italic)
    {
        if (string.IsNullOrEmpty(text)) return;

        context.DrawText(new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Times New Roman"), italic ? FontStyle.Italic : FontStyle.Normal),
            size,
            ink), new Point(x, y));
    }
}

internal sealed class NodePainter(FormulaPainter painter, double x, double y, double size, MathBox box) : MathNodeVisitor<bool>
{
    private const double Script = 0.7;

    public override bool VisitRow(MathRow node)
    {
        var cursor = x;
        foreach (var child in node.Children)
        {
            var childBox = Measure(child, size);
            painter.Draw(child, cursor, painter.Aligned(y, box, childBox), size);
            cursor += childBox.Width;
        }
        return true;
    }

    public override bool VisitNumber(MathNumber node) => Text(node.Text, x, false);

    public override bool VisitIdentifier(MathIdentifier node) => Text(node.Text, x, node.Text.Length == 1);

    public override bool VisitOperator(MathOperator node) => Text(node.Text, x + 2, false);

    public override bool VisitText(MathText node) => Text(node.Content, x, false);

    public override bool VisitFraction(MathFraction node)
    {
        var numerator = Measure(node.Numerator, size);
        var denominator = Measure(node.Denominator, size);
        var width = System.Math.Max(numerator.Width, denominator.Width);
        var centre = x + 2 + (width / 2);

        painter.Draw(node.Numerator, centre - (numerator.Width / 2), y, size);
        var rule = y + numerator.Height + 2;
        painter.Context.DrawLine(new Pen(painter.Ink, System.Math.Max(0.8, size / 14)), new Point(x + 1, rule), new Point(x + 3 + width, rule));
        painter.Draw(node.Denominator, centre - (denominator.Width / 2), rule + 2, size);
        return true;
    }

    public override bool VisitRoot(MathRoot node)
    {
        var inner = Measure(node.Radicand, size);
        var indexWidth = node.Index is null ? 0 : Measure(node.Index, size * Script).Width;
        if (node.Index is not null) painter.Draw(node.Index, x, y, size * Script);

        var left = x + indexWidth;
        var pen = new Pen(painter.Ink, System.Math.Max(0.8, size / 16));
        var bottom = y + inner.Height;
        var context = painter.Context;
        context.DrawLine(pen, new Point(left, y + (inner.Height * 0.55)), new Point(left + (size * 0.2), bottom));
        context.DrawLine(pen, new Point(left + (size * 0.2), bottom), new Point(left + (size * 0.45), y + 1));
        context.DrawLine(pen, new Point(left + (size * 0.45), y + 1), new Point(left + (size * 0.6) + inner.Width + 2, y + 1));

        painter.Draw(node.Radicand, left + (size * 0.6), y + 3, size);
        return true;
    }

    public override bool VisitSuperscript(MathSuperscript node)
    {
        var body = Measure(node.Base, size);
        painter.Draw(node.Base, x, painter.Aligned(y, box, body), size);
        painter.Draw(node.Exponent, x + body.Width, y, size * Script);
        return true;
    }

    public override bool VisitSubscript(MathSubscript node)
    {
        var body = Measure(node.Base, size);
        var top = painter.Aligned(y, box, body);
        painter.Draw(node.Base, x, top, size);
        painter.Draw(node.Index, x + body.Width, top + (body.Height * 0.55), size * Script);
        return true;
    }

    public override bool VisitSubSuperscript(MathSubSuperscript node)
    {
        var body = Measure(node.Base, size);
        var top = painter.Aligned(y, box, body);
        painter.Draw(node.Base, x, top, size);
        painter.Draw(node.Exponent, x + body.Width, y, size * Script);
        painter.Draw(node.Index, x + body.Width, top + (body.Height * 0.55), size * Script);
        return true;
    }

    public override bool VisitFenced(MathFenced node)
    {
        var inner = Measure(node.Content, size);
        var stretched = size * System.Math.Clamp(inner.Height / (size * 1.15), 1, 3);
        var fenceTop = y + ((box.Height - (stretched * 1.15)) / 2);

        painter.Text(node.Open, x, fenceTop, stretched, false);
        painter.Draw(node.Content, x + (size * 0.4), painter.Aligned(y, box, inner), size);
        painter.Text(node.Close, x + (size * 0.4) + inner.Width, fenceTop, stretched, false);
        return true;
    }

    public override bool VisitFunction(MathFunction node)
    {
        var name = Measure(new MathIdentifier(node.Name), size);
        var argument = Measure(node.Argument, size);
        painter.Text(node.Name, x, painter.Aligned(y, box, name), size, false);
        painter.Draw(node.Argument, x + name.Width + 2, painter.Aligned(y, box, argument), size);
        return true;
    }

    public override bool VisitLargeOperator(MathLargeOperator node)
    {
        var symbolSize = size * 1.6;
        var upper = node.Upper is null ? default : Measure(node.Upper, size * Script);
        var lower = node.Lower is null ? default : Measure(node.Lower, size * Script);
        var symbolWidth = Measure(new MathOperator(node.Symbol), symbolSize).Width;
        var column = System.Math.Max(symbolWidth, System.Math.Max(upper.Width, lower.Width));

        if (node.Upper is not null) painter.Draw(node.Upper, x + ((column - upper.Width) / 2), y, size * Script);
        painter.Text(node.Symbol, x + ((column - symbolWidth) / 2), y + upper.Height, symbolSize, false);
        if (node.Lower is not null) painter.Draw(node.Lower, x + ((column - lower.Width) / 2), y + upper.Height + symbolSize, size * Script);
        painter.Draw(node.Body, x + column + 3, painter.Aligned(y, box, Measure(node.Body, size)), size);
        return true;
    }

    public override bool VisitMatrix(MathMatrix node)
    {
        var layout = painter.Layout.MeasureMatrix(node, size);
        var top = y;

        for (var rowIndex = 0; rowIndex < node.Rows.Count; rowIndex++)
        {
            var row = node.Rows[rowIndex];
            var cursor = x;
            for (var column = 0; column < row.Count; column++)
            {
                var cell = Measure(row[column], size);
                var width = layout.Columns[column];
                painter.Draw(row[column], cursor + 2 + ((width - cell.Width) / 2), top + (layout.RowBaselines[rowIndex] - cell.Baseline), size);
                cursor += width + 4;
            }
            top += layout.RowHeights[rowIndex] + 2;
        }
        return true;
    }

    public override bool VisitAccent(MathAccent node)
    {
        var body = Measure(node.Base, size);
        painter.Text(node.Accent, x + (body.Width / 2) - (size * 0.2), y - (size * 0.15), size, false);
        painter.Draw(node.Base, x, painter.Aligned(y, box, body), size);
        return true;
    }

    protected override bool Otherwise(MathNode node) => false;

    private MathBox Measure(MathNode node, double at) => painter.Layout.Measure(node, at);

    private bool Text(string text, double left, bool italic)
    {
        painter.Text(text, left, y, size, italic);
        return true;
    }
}
