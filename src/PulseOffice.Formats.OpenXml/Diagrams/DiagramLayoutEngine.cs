using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml.Diagrams;

internal sealed record DiagramSources(XDocument Data, XDocument Layout, XDocument? Colors, XDocument? Style);

internal interface IDiagramLayoutEngine
{
    DiagramBox? Layout(DiagramDataModel model, XDocument layout, double width, double height);
}

internal sealed class DiagramLayoutEngine(
    IDiagramAlgorithmRegistry algorithms,
    DiagramConstraintSolver solver,
    DiagramLayoutDefinitionReader definitions,
    DiagramTextFitter fitter) : IDiagramLayoutEngine
{
    public DiagramLayoutEngine() : this(new DiagramAlgorithmRegistry(), new DiagramConstraintSolver(), new DiagramLayoutDefinitionReader(), new DiagramTextFitter()) { }

    public DiagramBox? Layout(DiagramDataModel model, XDocument layout, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (definitions.Read(layout) is not { } definition || width <= 0 || height <= 0) return null;
        var root = new DiagramTreeBuilder(model).Build(definition);
        root.Bounds = new DiagramRect(0, 0, width, height);
        Arrange(root, [], null, []);
        fitter.Fit(root);
        return root;
    }

    private void Arrange(DiagramBox box, IReadOnlyList<DiagramResolvedConstraint> inherited, DiagramProperties? own, IReadOnlyList<DiagramRule> rules)
    {
        var context = solver.Solve(box, inherited);
        if (own?["primFontSz"] is { } size) box.FontSize = size;
        else if (context.Self["primFontSz"] is { } self) box.FontSize = self;
        box.MinimumFontSize = rules.Concat(box.Rules).Where(rule => rule.Type == "primFontSz" && (rule.ForName.Length == 0 || rule.ForName == box.Name) && !double.IsNaN(rule.Value))
            .Select(rule => rule.Value).DefaultIfEmpty(box.MinimumFontSize).Min();
        algorithms.For(box.Algorithm.Type).Arrange(context);
        var passed = context.PassedDown;
        var passedRules = rules.Concat(box.Rules.Where(rule => rule.For == "des" || rule.ForName.Length > 0)).ToList();
        foreach (var child in box.Children.ToList())
        {
            if (child.FontSize == 0) child.FontSize = box.FontSize;
            Arrange(child, passed, context.Of(child), passedRules);
        }
    }
}

internal sealed class DiagramTextFitter
{
    private const double DefaultFontSize = 18;
    private const double MaxFontSize = 4000;
    private const double CharacterWidth = 0.52;
    private const double LineHeight = 1.2;

    public void Fit(DiagramBox root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var texts = root.SelfAndDescendants().Where(box => box.HasText).ToList();
        foreach (var box in texts)
        {
            var size = box.FontSize > 0 ? Math.Min(box.FontSize, MaxFontSize) : DefaultFontSize;
            var minimum = Math.Clamp(box.MinimumFontSize, 1, MaxFontSize);
            while (size > minimum && !Fits(box, size)) size -= 0.5;
            box.FontSize = Math.Max(minimum, size);
        }
        foreach (var group in texts.GroupBy(box => box.Name))
        {
            var smallest = group.Min(box => box.FontSize);
            foreach (var box in group) box.FontSize = smallest;
        }
    }

    private bool Fits(DiagramBox box, double size)
    {
        var margin = size * 0.1 * ((double)PulseOffice.Foundation.Units.Emu.PerPoint) * 2 + box.Bounds.Width * 0.06;
        var width = Math.Max(1, box.Bounds.Width - margin);
        var height = Math.Max(1, box.Bounds.Height - margin);
        var lines = 0;
        foreach (var paragraph in box.Paragraphs)
        {
            var indent = paragraph.Level * size * ((double)PulseOffice.Foundation.Units.Emu.PerPoint);
            var available = Math.Max(1, width - indent);
            var lineWidth = 0.0;
            var count = 1;
            foreach (var word in paragraph.Source.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var wordWidth = word.Length * size * CharacterWidth * ((double)PulseOffice.Foundation.Units.Emu.PerPoint);
                if (wordWidth > available) return false;
                var next = lineWidth == 0 ? wordWidth : lineWidth + size * CharacterWidth * ((double)PulseOffice.Foundation.Units.Emu.PerPoint) + wordWidth;
                if (next > available)
                {
                    count++;
                    lineWidth = wordWidth;
                }
                else lineWidth = next;
            }
            lines += count;
        }
        return lines * size * LineHeight * ((double)PulseOffice.Foundation.Units.Emu.PerPoint) <= height;
    }
}

internal interface IDiagramDrawingGenerator
{
    List<XElement> Generate(DiagramSources sources, double width, double height);
}

internal sealed class DiagramDrawingGenerator(IDiagramLayoutEngine engine, DiagramDataModelReader models, DiagramDrawingComposer composer) : IDiagramDrawingGenerator
{
    private static readonly XNamespace DataNamespace = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    public DiagramDrawingGenerator() : this(new DiagramLayoutEngine(), new DiagramDataModelReader(), new DiagramDrawingComposer()) { }

    public List<XElement> Generate(DiagramSources sources, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (models.Read(sources.Data) is not { } model) return [];
        return engine.Layout(model, sources.Layout, width, height) is { } root
            ? composer.Compose(root, new DiagramStyleSheet(sources.Colors, sources.Style), sources.Data.Root?.Element(DataNamespace + "bg"))
            : [];
    }
}
