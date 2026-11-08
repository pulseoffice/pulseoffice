using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Drawing.Shapes.Geometry;

public interface IPresetShapeCatalog
{
    IReadOnlyCollection<string> Names { get; }

    PresetShapeDefinition? Find(string? name);
}

public sealed class PresetShapeCatalog : IPresetShapeCatalog
{
    private const string ResourceName = "PulseOffice.Drawing.PresetShapeDefinitions.xml";

    private readonly Lazy<IReadOnlyDictionary<string, PresetShapeDefinition>> _definitions;

    public PresetShapeCatalog() => _definitions = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyCollection<string> Names => [.. _definitions.Value.Keys];

    public PresetShapeDefinition? Find(string? name) =>
        name is not null && _definitions.Value.TryGetValue(name, out var definition) ? definition : null;

    private IReadOnlyDictionary<string, PresetShapeDefinition> Load()
    {
        using var stream = typeof(PresetShapeCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The preset shape definitions are missing from the assembly.");
        var root = XDocument.Load(stream).Root!;
        return root.Elements().ToDictionary(element => element.Name.LocalName, Definition, StringComparer.Ordinal);
    }

    private readonly IReadOnlyDictionary<string, ShapeShade> _shades = new Dictionary<string, ShapeShade>(StringComparer.Ordinal)
    {
        ["darken"] = ShapeShade.Darken,
        ["darkenLess"] = ShapeShade.DarkenLess,
        ["lighten"] = ShapeShade.Lighten,
        ["lightenLess"] = ShapeShade.LightenLess,
    };

    private PresetShapeDefinition Definition(XElement shape) => new(
        shape.Name.LocalName,
        Guides(Child(shape, "avLst")),
        Guides(Child(shape, "gdLst")),
        [.. (Child(shape, "pathLst")?.Elements().Where(element => element.Name.LocalName == "path") ?? []).Select(Path)])
    {
        Handles = [.. (Child(shape, "ahLst")?.Elements() ?? []).Select(Handle).OfType<PresetHandle>()],
    };

    private PresetHandle? Handle(XElement handle)
    {
        if (Child(handle, "pos") is not { } position) return null;
        var at = new PresetPoint((string?)position.Attribute("x") ?? "0", (string?)position.Attribute("y") ?? "0");
        return handle.Name.LocalName switch
        {
            "ahXY" => new PresetHandle(at, Axis(handle, "gdRefX", "minX", "maxX"), Axis(handle, "gdRefY", "minY", "maxY")),
            "ahPolar" => new PresetHandle(at, Axis(handle, "gdRefR", "minR", "maxR"), Axis(handle, "gdRefAng", "minAng", "maxAng")) { Polar = true },
            _ => null,
        };
    }

    private PresetHandleAxis? Axis(XElement handle, string reference, string minimum, string maximum) =>
        (string?)handle.Attribute(reference) is { Length: > 0 } name
            ? new PresetHandleAxis(name, (string?)handle.Attribute(minimum) ?? "0", (string?)handle.Attribute(maximum) ?? "0")
            : null;

    private XElement? Child(XElement parent, string name) => parent.Elements().FirstOrDefault(element => element.Name.LocalName == name);

    private IReadOnlyList<DrawingGuide> Guides(XElement? list) =>
        [.. (list?.Elements().Where(element => element.Name.LocalName == "gd") ?? []).Select(Guide)];

    private DrawingGuide Guide(XElement guide)
    {
        var parts = ((string?)guide.Attribute("fmla") ?? "val 0").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new DrawingGuide((string?)guide.Attribute("name") ?? string.Empty, parts[0], [.. parts.Skip(1)]);
    }

    private PresetPath Path(XElement path) => new([.. path.Elements().Select(Command).OfType<PresetPathCommand>()])
    {
        Width = Number(path.Attribute("w")),
        Height = Number(path.Attribute("h")),
        Filled = (string?)path.Attribute("fill") != "none",
        Shade = (string?)path.Attribute("fill") is { } fill && _shades.TryGetValue(fill, out var shade) ? shade : ShapeShade.Normal,
        Stroked = (string?)path.Attribute("stroke") != "false",
    };

    private PresetPathCommand? Command(XElement command)
    {
        var points = command.Elements().Where(element => element.Name.LocalName == "pt")
            .Select(point => new PresetPoint((string?)point.Attribute("x") ?? "0", (string?)point.Attribute("y") ?? "0")).ToList();
        return command.Name.LocalName switch
        {
            "moveTo" when points.Count > 0 => new PresetMove(points[0]),
            "lnTo" when points.Count > 0 => new PresetLine(points[0]),
            "arcTo" => new PresetArc((string?)command.Attribute("wR") ?? "0", (string?)command.Attribute("hR") ?? "0",
                (string?)command.Attribute("stAng") ?? "0", (string?)command.Attribute("swAng") ?? "0"),
            "quadBezTo" when points.Count > 1 => new PresetQuadratic(points[0], points[1]),
            "cubicBezTo" when points.Count > 2 => new PresetCubic(points[0], points[1], points[2]),
            "close" => new PresetClose(),
            _ => null,
        };
    }

    private double? Number(XAttribute? attribute) =>
        attribute is not null && double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
