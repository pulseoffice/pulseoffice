namespace PulseOffice.Formats.OpenXml.Diagrams;

internal interface IDiagramAlgorithm
{
    string Type { get; }

    void Arrange(DiagramLayoutContext context);
}

internal interface IDiagramAlgorithmRegistry
{
    IDiagramAlgorithm For(string type);
}

internal sealed class DiagramAlgorithmRegistry(IEnumerable<IDiagramAlgorithm> algorithms) : IDiagramAlgorithmRegistry
{
    private readonly Dictionary<string, IDiagramAlgorithm> _algorithms = algorithms.ToDictionary(algorithm => algorithm.Type, StringComparer.Ordinal);

    public DiagramAlgorithmRegistry() : this(new CompositeDiagramAlgorithm()) { }

    private DiagramAlgorithmRegistry(CompositeDiagramAlgorithm composite) : this(
    [
        composite, new LinearDiagramAlgorithm(), new SnakeDiagramAlgorithm(), new CycleDiagramAlgorithm(),
        new HierarchyDiagramAlgorithm("hierRoot"), new HierarchyDiagramAlgorithm("hierChild"), new PyramidDiagramAlgorithm(),
        new ConnectorDiagramAlgorithm(composite), new TextDiagramAlgorithm(composite), new SpaceDiagramAlgorithm(),
    ]) { }

    public IDiagramAlgorithm For(string type) =>
        _algorithms.TryGetValue(type, out var algorithm) ? algorithm : _algorithms["composite"];
}

internal abstract class DiagramAlgorithm : IDiagramAlgorithm
{
    public abstract string Type { get; }

    public abstract void Arrange(DiagramLayoutContext context);

    protected IReadOnlyList<DiagramBox> Placed(DiagramLayoutContext context) =>
        [.. context.Box.Children.Where(child => !child.IsSpacer || context.Of(child).Has("w") || context.Of(child).Has("h"))];

    protected (double Width, double Height) Requested(DiagramLayoutContext context, DiagramBox child, double width, double height)
    {
        var properties = context.Of(child);
        var w = properties["w"] ?? (properties["l"] is { } l && properties["r"] is { } r ? r - l : width);
        var h = properties["h"] ?? (properties["t"] is { } t && properties["b"] is { } b ? b - t : height);
        return (Math.Max(0, w), Math.Max(0, h));
    }
}

internal sealed class CompositeDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "composite";

    public override void Arrange(DiagramLayoutContext context)
    {
        var parent = context.Box.Bounds;
        foreach (var child in context.Box.Children)
        {
            var properties = context.Of(child);
            var (width, height) = Requested(context, child, parent.Width, parent.Height);
            var left = properties["l"] ?? (properties["r"] is { } right ? right - width : properties["ctrX"] is { } centreX ? centreX - width / 2 : 0);
            var top = properties["t"] ?? (properties["b"] is { } bottom ? bottom - height : properties["ctrY"] is { } centreY ? centreY - height / 2 : 0);
            child.Bounds = new DiagramRect(parent.X + left, parent.Y + top, width, height);
        }
        if (context.Box.Rules.Any(rule => rule.Type == "h" && double.IsInfinity(rule.Value)) && context.Box.Children.Count > 0)
        {
            var lowest = context.Box.Children.MaxBy(child => child.Bounds.Y + child.Bounds.Height)!;
            var bottom = parent.Y + parent.Height;
            if (lowest.Bounds.Y + lowest.Bounds.Height < bottom)
                lowest.Bounds = lowest.Bounds with { Height = bottom - lowest.Bounds.Y };
        }
    }
}

internal sealed class LinearDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "lin";

    public override void Arrange(DiagramLayoutContext context)
    {
        var parent = context.Box.Bounds;
        var children = Placed(context);
        if (children.Count == 0 || parent.Width <= 0 || parent.Height <= 0) return;
        var direction = context.Box.Algorithm.Parameter("linDir", "fromL");
        var horizontal = direction is "fromL" or "fromR";
        var reversed = direction is "fromR" or "fromB";
        var main = horizontal ? parent.Width : parent.Height;
        var cross = horizontal ? parent.Height : parent.Width;
        var even = main / children.Count;
        var space = Math.Max(0, context.Self["sibSp"] ?? 0);

        var sizes = children.Select(child =>
        {
            var (w, h) = Requested(context, child, horizontal ? even : parent.Width, horizontal ? parent.Height : even);
            if (!context.Of(child).Has(horizontal ? "w" : "h")) (w, h) = horizontal ? (even, h) : (w, even);
            return (Main: Math.Min(horizontal ? w : h, main), Cross: Math.Min(horizontal ? h : w, cross));
        }).ToList();
        var total = sizes.Sum(size => size.Main) + space * (children.Count - 1);
        var scale = total > main ? main / total : 1;
        var crossScale = sizes.Max(size => size.Cross) > cross ? cross / sizes.Max(size => size.Cross) : 1;
        var used = total * scale;
        var position = (main - used) / 2;
        var angle = direction switch { "fromR" => 180.0, "fromT" => 90.0, "fromB" => 270.0, _ => 0.0 };

        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            var length = sizes[index].Main * scale;
            var breadth = sizes[index].Cross * (horizontal ? crossScale : crossScale);
            var start = reversed ? main - position - length : position;
            child.Bounds = horizontal
                ? new DiagramRect(parent.X + start, parent.Y + (cross - breadth) / 2, length, breadth)
                : new DiagramRect(parent.X + (cross - breadth) / 2, parent.Y + start, breadth, length);
            if (child.IsConnector) child.Rotation = angle;
            position += length + space * scale;
        }
        if (context.Box.ChildOrder == "t") context.Box.Children.Reverse();
    }
}

internal sealed class SnakeDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "snake";

    public override void Arrange(DiagramLayoutContext context)
    {
        var parent = context.Box.Bounds;
        var children = context.Box.Children.Where(child => !child.IsSpacer && !child.IsConnector).ToList();
        if (children.Count == 0 || parent.Width <= 0 || parent.Height <= 0) return;
        var first = context.Of(children[0]);
        var aspect = first["h"] is { } h && first["w"] is { } w && w > 0 ? Math.Min(h / w, 0.54) : 0.54;
        var spacing = context.Box.Constraints.FirstOrDefault(constraint => constraint.Type == "sp" && constraint.For == "self" && constraint.ForName.Length == 0) is { } constraint
            && Math.Abs(constraint.Factor - 1) > 1e-9 ? constraint.Factor : 0.3;
        var direction = context.Box.Algorithm.Parameter("grDir", "tL");
        var columnsFirst = context.Box.Algorithm.Parameter("flowDir", "row") == "col";
        var reverse = context.Box.Algorithm.Parameter("contDir", "sameDir") == "revDir";

        var best = (Size: 0.0, Columns: children.Count, Rows: 1);
        for (var rows = 1; rows <= children.Count; rows++)
        {
            var columns = (int)Math.Ceiling(children.Count / (double)rows);
            best = (0, columns, rows);
            if (rows * parent.Height / (columns * parent.Width) >= 0.54) break;
        }
        var byWidth = parent.Width / (best.Columns + (best.Columns - 1) * spacing);
        var byHeight = parent.Height / (best.Rows * aspect + (best.Rows - 1) * spacing);
        best = best with { Size = byWidth <= byHeight * 1.6 ? byWidth : byHeight };
        var cell = best.Size;
        var gap = cell * spacing;
        var gridWidth = best.Columns * cell + (best.Columns - 1) * gap;
        var rowGap = cell * aspect * spacing;
        var gridHeight = best.Rows * cell * aspect + (best.Rows - 1) * rowGap;
        var originX = parent.X + (parent.Width - gridWidth) / 2;
        var originY = parent.Y + Math.Max(0, (parent.Height - gridHeight) / 2);
        var lastRowCount = children.Count - (best.Rows - 1) * best.Columns;

        for (var index = 0; index < children.Count; index++)
        {
            var (row, column) = columnsFirst ? (index % best.Rows, index / best.Rows) : (index / best.Columns, index % best.Columns);
            if (!columnsFirst && reverse && row % 2 == 1) column = best.Columns - 1 - column;
            if (direction is "tR" or "bR") column = best.Columns - 1 - column;
            if (direction is "bL" or "bR") row = best.Rows - 1 - row;
            var shift = !columnsFirst && index / best.Columns == best.Rows - 1 && lastRowCount < best.Columns ? (best.Columns - lastRowCount) * (cell + gap) / 2 : 0;
            if (direction is "tR" or "bR") shift = -shift;
            children[index].Bounds = new DiagramRect(originX + column * (cell + gap) + shift, originY + row * (cell * aspect + rowGap), cell, cell * aspect);
        }
        foreach (var hidden in context.Box.Children.Where(child => child.IsSpacer || child.IsConnector)) hidden.Bounds = new DiagramRect(0, 0, 0, 0);
    }
}

internal sealed class CycleDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "cycle";

    public override void Arrange(DiagramLayoutContext context)
    {
        var parent = context.Box.Bounds;
        var nodes = context.Box.Children.Where(child => !child.IsConnector && !child.IsSpacer).ToList();
        if (nodes.Count == 0) return;
        var connectors = context.Box.Children.Where(child => child.IsConnector).ToList();
        var start = context.Box.Algorithm.Number("stAng", 0);
        var span = context.Box.Algorithm.Number("spanAng", 360);
        var centred = context.Box.Algorithm.Parameter("ctrShpMap", "none") == "fNode" && nodes.Count > 1;
        var along = context.Box.Algorithm.Parameter("rotPath", "none") == "alongPath";
        var ring = centred ? nodes.Skip(1).ToList() : nodes;
        var count = Math.Max(1, ring.Count);
        var width = parent.Width / 4;
        var height = parent.Height / 4;
        var radius = Math.Min((parent.Width - width) / 2, (parent.Height - height) / 2);

        if (centred) nodes[0].Bounds = new DiagramRect(parent.CentreX - width / 2, parent.CentreY - height / 2, width, height);

        for (var index = 0; index < ring.Count; index++)
        {
            var angle = start + index * span / count;
            var (x, y) = Point(parent, radius, angle);
            ring[index].Bounds = new DiagramRect(x - width / 2, y - height / 2, width, height);
            if (along) ring[index].Rotation = angle;
        }

        var connectorWidth = parent.Width / 12;
        var connectorHeight = parent.Height / 12;
        var connectorRadius = radius * Math.Cos(span / (count + connectors.Count) * Math.PI / 180);
        for (var index = 0; index < connectors.Count; index++)
        {
            var angle = start + (index + 0.5) * span / count;
            var (x, y) = Point(parent, connectorRadius, angle);
            connectors[index].Bounds = new DiagramRect(x - connectorWidth / 2, y - connectorHeight / 2, connectorWidth, connectorHeight);
            connectors[index].Rotation = (span > 0 ? 0 : 180) + angle;
        }
    }

    private (double X, double Y) Point(DiagramRect parent, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return (parent.CentreX + radius * Math.Sin(radians), parent.CentreY - radius * Math.Cos(radians));
    }
}

internal sealed class HierarchyDiagramAlgorithm(string type) : DiagramAlgorithm
{
    private const double SpaceWidth = 0.1;
    private const double SpaceHeight = 0.3;

    public override string Type => type;

    public override void Arrange(DiagramLayoutContext context)
    {
        var box = context.Box;
        var parent = box.Bounds;
        var children = box.Children.Where(child => !child.IsSpacer).ToList();
        if (children.Count == 0 || parent.Width <= 0 || parent.Height <= 0) return;
        var direction = Type == "hierRoot" ? "fromT" : box.Algorithm.Parameter("linDir", "fromL");
        var secondary = box.Algorithm.Parameter("secLinDir", string.Empty);
        var placed = children.Where(child => !child.IsConnector).ToList();
        var count = placed.Count;
        var across = secondary == "fromT" ? 2 : direction is "fromL" or "fromR" ? Math.Max(1, placed.Sum(Width)) : 1;
        var levels = Math.Max(1, Vertical(box));
        var unitWidth = parent.Width / (across + (across - 1) * SpaceWidth);
        var unitHeight = parent.Height / (levels + (levels - 1) * SpaceHeight);
        var x = 0.0;
        var y = 0.0;
        var index = 0;
        var rowHeight = 0.0;
        foreach (var child in placed)
        {
            var span = direction is "fromL" or "fromR" && secondary != "fromT" ? Width(child) : 1;
            var width = unitWidth * span + (span - 1) * unitWidth * SpaceWidth;
            var height = unitHeight * Vertical(child) + (Vertical(child) - 1) * unitHeight * SpaceHeight;
            var indent = Type == "hierChild" && across == 1 && direction is "fromT" or "fromB" ? unitWidth * 0.1 : 0;
            child.Bounds = new DiagramRect(parent.X + x + indent, parent.Y + y, width - 2 * indent, height);
            if (direction is "fromT" or "fromB") y += height + unitHeight * SpaceHeight;
            else x += width + unitWidth * SpaceWidth;
            rowHeight = Math.Max(rowHeight, height);
            if (secondary == "fromT" && index % 2 == 1)
            {
                x = 0;
                y += rowHeight + unitHeight * SpaceHeight;
                rowHeight = 0;
            }
            index++;
        }
        if (count > 0 && Type == "hierChild" && direction is "fromL" or "fromR")
        {
            var used = x - unitWidth * SpaceWidth;
            var shift = (parent.Width - used) / 2;
            if (shift > 0)
                foreach (var child in placed) child.Bounds = child.Bounds with { X = child.Bounds.X + shift };
        }
        foreach (var connector in children.Where(child => child.IsConnector)) connector.Bounds = new DiagramRect(0, 0, 0, 0);
    }

    private int Width(DiagramBox box)
    {
        var widths = box.Children.Where(child => !child.IsConnector && !child.IsSpacer).Select(Width).ToList();
        if (widths.Count == 0) return 1;
        var horizontal = box.Algorithm.Type == "hierChild" && box.Algorithm.Parameter("linDir", "fromL") is "fromL" or "fromR"
            && box.Algorithm.Parameter("secLinDir", string.Empty) != "fromT";
        return Math.Max(1, horizontal ? widths.Sum() : widths.Max());
    }

    private int Vertical(DiagramBox box)
    {
        var children = box.Children.Where(child => !child.IsConnector && !child.IsSpacer).ToList();
        if (children.Count == 0) return box.IsConnector ? 0 : 1;
        return box.Algorithm.Type switch
        {
            "hierRoot" => children.Sum(Vertical),
            "hierChild" when box.Algorithm.Parameter("linDir", "fromL") is "fromT" or "fromB" => children.Sum(Vertical),
            "hierChild" when box.Algorithm.Parameter("secLinDir", string.Empty) == "fromT" => (children.Sum(Vertical) + 1) / 2,
            _ => children.Max(Vertical),
        };
    }
}

internal sealed class PyramidDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "pyra";

    public override void Arrange(DiagramLayoutContext context)
    {
        var parent = context.Box.Bounds;
        var children = context.Box.Children.Where(child => !child.IsSpacer && !child.IsConnector).ToList();
        if (children.Count == 0) return;
        var upward = context.Box.Algorithm.Parameter("linDir", "fromT") == "fromT";
        var height = parent.Height / children.Count;
        for (var index = 0; index < children.Count; index++)
        {
            var level = upward ? index : children.Count - 1 - index;
            var width = parent.Width * (level + 1) / children.Count;
            children[index].Bounds = new DiagramRect(parent.X + (parent.Width - width) / 2, parent.Y + index * height, width, height);
            children[index].PresetOverride = level == 0 ? "triangle" : null;
        }
    }
}

internal sealed class ConnectorDiagramAlgorithm(CompositeDiagramAlgorithm composite) : DiagramAlgorithm
{
    public override string Type => "conn";

    public override void Arrange(DiagramLayoutContext context)
    {
        var box = context.Box;
        var begin = box.Algorithm.Parameter("begSty", "noArr");
        var end = box.Algorithm.Parameter("endSty", "arr");
        if (box.Shape?.Type == "conn")
            box.PresetOverride = box.Algorithm.Parameter("connRout", "stra") == "bend" ? "none"
                : begin == "arr" && end == "arr" ? "leftRightArrow" : begin == "arr" ? "leftArrow" : "rightArrow";
        composite.Arrange(context);
    }
}

internal sealed class TextDiagramAlgorithm(CompositeDiagramAlgorithm composite) : DiagramAlgorithm
{
    public override string Type => "tx";

    public override void Arrange(DiagramLayoutContext context) => composite.Arrange(context);
}

internal sealed class SpaceDiagramAlgorithm : DiagramAlgorithm
{
    public override string Type => "sp";

    public override void Arrange(DiagramLayoutContext context) => context.Box.Paragraphs.Clear();
}
