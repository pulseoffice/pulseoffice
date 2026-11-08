using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Html;

public sealed record HtmlBorderLine(double WidthPoints, uint Color)
{
    public BorderLineStyle Style { get; init; }
}

public sealed record HtmlCellEdges(HtmlBorderLine? Top, HtmlBorderLine? Right, HtmlBorderLine? Bottom, HtmlBorderLine? Left)
{
    public bool IsEmpty => Top is null && Right is null && Bottom is null && Left is null;
}

public interface IHtmlCellBorders
{
    bool TableBordered(HtmlStartTag table);

    HtmlCellEdges? Read(IReadOnlyDictionary<string, string> style, bool tableBordered);
}

public sealed class HtmlCellBorders(ICssDeclarations css) : IHtmlCellBorders
{
    private const double DefaultWidthPoints = 0.75;
    private const double SmallestWidthPoints = 0.25;
    private const double LargestWidthPoints = 12;
    private const uint Black = 0xFF000000;

    private readonly string[] _sides = ["top", "right", "bottom", "left"];

    public bool TableBordered(HtmlStartTag table) =>
        table.Attribute("border") is { } border
        && (border.Trim().Length == 0 || int.TryParse(border, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var width) && width > 0);

    public HtmlCellEdges? Read(IReadOnlyDictionary<string, string> style, bool tableBordered)
    {
        ArgumentNullException.ThrowIfNull(style);
        var edges = new EdgeDraft[_sides.Length];
        for (var side = 0; side < edges.Length; side++) edges[side] = tableBordered ? EdgeDraft.Drawn : EdgeDraft.Absent;

        if (style.GetValueOrDefault("border") is { } all)
            for (var side = 0; side < edges.Length; side++) edges[side] = Shorthand(all);
        Box(style.GetValueOrDefault("border-width"), edges, Width);
        Box(style.GetValueOrDefault("border-style"), edges, Styled);
        Box(style.GetValueOrDefault("border-color"), edges, Coloured);
        for (var side = 0; side < edges.Length; side++)
        {
            var name = "border-" + _sides[side];
            if (style.GetValueOrDefault(name) is { } shorthand) edges[side] = Shorthand(shorthand);
            if (style.GetValueOrDefault(name + "-width") is { } width) edges[side] = Width(edges[side], width);
            if (style.GetValueOrDefault(name + "-style") is { } lineStyle) edges[side] = Styled(edges[side], lineStyle);
            if (style.GetValueOrDefault(name + "-color") is { } colour) edges[side] = Coloured(edges[side], colour);
        }

        var result = new HtmlCellEdges(edges[0].Line, edges[1].Line, edges[2].Line, edges[3].Line);
        return result.IsEmpty ? null : result;
    }

    private void Box(string? declared, EdgeDraft[] edges, Func<EdgeDraft, string, EdgeDraft> apply)
    {
        if (declared is null) return;
        var values = Values(declared);
        if (values.Count == 0) return;
        for (var side = 0; side < edges.Length; side++)
        {
            var value = values.Count switch
            {
                1 => values[0],
                2 => values[side % 2],
                3 => side == 3 ? values[1] : values[side],
                _ => values[side],
            };
            edges[side] = apply(edges[side], value);
        }
    }

    private EdgeDraft Shorthand(string declared)
    {
        var edge = EdgeDraft.Drawn;
        foreach (var part in Values(declared))
        {
            if (Hides(part)) edge = edge with { Hidden = true };
            else if (LineStyle(part) is { } known) edge = edge with { Style = known };
            else if (WidthOf(part) is { } width) edge = edge with { Width = width };
            else if (css.Color(part) is { } colour) edge = edge with { Color = colour };
        }
        return edge;
    }

    private EdgeDraft Width(EdgeDraft edge, string value) =>
        WidthOf(value) is { } width ? edge with { Present = true, Width = width } : edge;

    private EdgeDraft Styled(EdgeDraft edge, string value) =>
        Hides(value) ? edge with { Hidden = true }
        : LineStyle(value) is { } known ? edge with { Present = true, Hidden = false, Style = known }
        : edge;

    private EdgeDraft Coloured(EdgeDraft edge, string value) =>
        css.Color(value) is { } colour ? edge with { Color = colour } : edge;

    private double? WidthOf(string value) => value.ToLowerInvariant() switch
    {
        "thin" => 0.75,
        "medium" => 2.25,
        "thick" => 3.75,
        _ => css.Points(value),
    };

    private bool Hides(string value) => value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Equals("hidden", StringComparison.OrdinalIgnoreCase);

    private BorderLineStyle? LineStyle(string value) => value.ToLowerInvariant() switch
    {
        "solid" or "groove" or "ridge" or "inset" or "outset" => BorderLineStyle.Solid,
        "dotted" => BorderLineStyle.Dotted,
        "dashed" => BorderLineStyle.Dashed,
        "double" => BorderLineStyle.Double,
        _ => null,
    };

    private List<string> Values(string declared)
    {
        var values = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index <= declared.Length; index++)
        {
            var character = index < declared.Length ? declared[index] : ' ';
            if (character == '(') depth++;
            else if (character == ')') depth = Math.Max(0, depth - 1);
            else if (depth == 0 && char.IsWhiteSpace(character))
            {
                if (index > start) values.Add(declared[start..index]);
                start = index + 1;
            }
        }
        return values;
    }

    private sealed record EdgeDraft(bool Present, bool Hidden, double Width, BorderLineStyle Style, uint Color)
    {
        public static readonly EdgeDraft Absent = new(false, false, DefaultWidthPoints, BorderLineStyle.Solid, Black);

        public static readonly EdgeDraft Drawn = Absent with { Present = true };

        public HtmlBorderLine? Line => !Present || Hidden || Width <= 0
            ? null
            : new HtmlBorderLine(Math.Clamp(Width, SmallestWidthPoints, LargestWidthPoints), Color) { Style = Style };
    }
}
