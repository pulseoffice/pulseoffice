namespace PulseOffice.Formulas;

public readonly record struct MathBox(double Width, double Height, double Baseline)
{
    public double Depth => Height - Baseline;
}

public sealed record MathPlacement(MathNode Node, double X, double Y, double Size, MathBox Box);

public interface IGlyphMetrics
{
    double Width(string text, double size);

    double Height(double size);
}

public sealed class ApproximateGlyphMetrics : IGlyphMetrics
{
    public double Width(string text, double size) =>
        string.IsNullOrEmpty(text) ? 0 : text.Length * size * 0.52;

    public double Height(double size) => size * 1.15;
}

public sealed record MatrixMetrics(
    IReadOnlyList<double> Columns,
    IReadOnlyList<double> RowHeights,
    IReadOnlyList<double> RowBaselines);

public interface IFormulaLayout
{
    IReadOnlyList<MathPlacement> Arrange(MathFormula formula, double size);

    MathBox Measure(MathNode node, double size);

    MatrixMetrics MeasureMatrix(MathMatrix matrix, double size);
}

public sealed class FormulaLayout(IGlyphMetrics metrics) : IFormulaLayout
{
    private const double ScriptRatio = 0.7;
    private const double Gap = 2;

    public FormulaLayout() : this(new ApproximateGlyphMetrics()) { }

    public IReadOnlyList<MathPlacement> Arrange(MathFormula formula, double size)
    {
        ArgumentNullException.ThrowIfNull(formula);

        var placements = new List<MathPlacement>();

        Place(formula.Root, 0, 0, size, placements);

        return placements;
    }

    public MathBox Measure(MathNode node, double size)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Accept(new MeasureVisitor(this, metrics, size));
    }

    public MatrixMetrics MeasureMatrix(MathMatrix matrix, double size)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        var columns = new List<double>();
        var heights = new List<double>();
        var baselines = new List<double>();

        foreach (var row in matrix.Rows)
        {
            double height = 0, baseline = 0;

            for (var index = 0; index < row.Count; index++)
            {
                var box = Measure(row[index], size);

                if (index >= columns.Count) columns.Add(box.Width);
                else columns[index] = System.Math.Max(columns[index], box.Width);

                height = System.Math.Max(height, box.Height);
                baseline = System.Math.Max(baseline, box.Baseline);
            }

            heights.Add(height);
            baselines.Add(baseline);
        }

        return new MatrixMetrics(columns, heights, baselines);
    }

    private void Place(MathNode node, double x, double y, double size, List<MathPlacement> placements)
    {
        var box = Measure(node, size);

        if (node is MathRow row)
        {
            var cursor = x;

            foreach (var child in row.Children)
            {
                Place(child, cursor, y, size, placements);
                cursor += Measure(child, size).Width;
            }

            return;
        }

        placements.Add(new MathPlacement(node, x, y, size, box));
    }

    private sealed class MeasureVisitor(FormulaLayout layout, IGlyphMetrics metrics, double size) : MathNodeVisitor<MathBox>
    {
        private double LineHeight => metrics.Height(size);

        public override MathBox VisitNumber(MathNumber node) => Simple(metrics.Width(node.Text, size));

        public override MathBox VisitIdentifier(MathIdentifier node) => Simple(metrics.Width(node.Text, size));

        public override MathBox VisitText(MathText node) => Simple(metrics.Width(node.Content, size));

        public override MathBox VisitOperator(MathOperator node) => Simple(metrics.Width(node.Text, size) + (Gap * 2));

        public override MathBox VisitRow(MathRow node)
        {
            double width = 0, above = 0, below = 0;
            foreach (var box in node.Children.Select(Measure))
            {
                width += box.Width;
                above = System.Math.Max(above, box.Baseline);
                below = System.Math.Max(below, box.Depth);
            }
            return new MathBox(width, above + below, above);
        }

        public override MathBox VisitFraction(MathFraction node)
        {
            var numerator = Measure(node.Numerator);
            var denominator = Measure(node.Denominator);
            return new MathBox(
                System.Math.Max(numerator.Width, denominator.Width) + (Gap * 2),
                numerator.Height + denominator.Height + (Gap * 3),
                numerator.Height + Gap + (LineHeight * 0.3));
        }

        public override MathBox VisitRoot(MathRoot node)
        {
            var inner = Measure(node.Radicand);
            var index = node.Index is null ? default : Script(node.Index);
            return new MathBox(inner.Width + (size * 0.7) + index.Width, inner.Height + (Gap * 2), inner.Baseline + Gap);
        }

        public override MathBox VisitSuperscript(MathSuperscript node)
        {
            var body = Measure(node.Base);
            var script = Script(node.Exponent);
            return new MathBox(body.Width + script.Width, body.Height + (script.Height * 0.5), body.Baseline + (script.Height * 0.5));
        }

        public override MathBox VisitSubscript(MathSubscript node)
        {
            var body = Measure(node.Base);
            var script = Script(node.Index);
            return new MathBox(body.Width + script.Width, body.Height + (script.Height * 0.4), body.Baseline);
        }

        public override MathBox VisitSubSuperscript(MathSubSuperscript node)
        {
            var body = Measure(node.Base);
            var lower = Script(node.Index);
            var upper = Script(node.Exponent);
            return new MathBox(
                body.Width + System.Math.Max(lower.Width, upper.Width),
                body.Height + (upper.Height * 0.5) + (lower.Height * 0.4),
                body.Baseline + (upper.Height * 0.5));
        }

        public override MathBox VisitFenced(MathFenced node)
        {
            var inner = Measure(node.Content);
            return new MathBox(inner.Width + (size * 0.8), inner.Height, inner.Baseline);
        }

        public override MathBox VisitFunction(MathFunction node)
        {
            var argument = Measure(node.Argument);
            return new MathBox(
                metrics.Width(node.Name, size) + Gap + argument.Width,
                System.Math.Max(LineHeight, argument.Height),
                System.Math.Max(LineHeight, argument.Baseline));
        }

        public override MathBox VisitLargeOperator(MathLargeOperator node)
        {
            var body = Measure(node.Body);
            var symbol = metrics.Width(node.Symbol, size * 1.6);
            var lower = node.Lower is null ? default : Script(node.Lower);
            var upper = node.Upper is null ? default : Script(node.Upper);
            var column = System.Math.Max(symbol, System.Math.Max(lower.Width, upper.Width));
            return new MathBox(column + Gap + body.Width, (size * 1.6) + lower.Height + upper.Height, upper.Height + (size * 1.6 * 0.72));
        }

        public override MathBox VisitMatrix(MathMatrix node)
        {
            var matrix = layout.MeasureMatrix(node, size);
            var total = matrix.RowHeights.Sum() + (matrix.RowHeights.Count * Gap);
            return new MathBox(matrix.Columns.Sum() + (matrix.Columns.Count * Gap * 2), total, (total / 2) + (LineHeight * 0.3));
        }

        public override MathBox VisitAccent(MathAccent node)
        {
            var body = Measure(node.Base);
            return new MathBox(body.Width, body.Height + (Gap * 2), body.Baseline + (Gap * 2));
        }

        protected override MathBox Otherwise(MathNode node) => Simple(0);

        private MathBox Measure(MathNode node) => layout.Measure(node, size);

        private MathBox Script(MathNode node) => layout.Measure(node, size * ScriptRatio);

        private MathBox Simple(double width) => new(width, LineHeight, LineHeight * 0.78);
    }
}
