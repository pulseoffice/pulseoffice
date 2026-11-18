using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlCategoryLevels
{
    private const string Uri = "{3C7E0F5A-9B21-4D8E-A6C4-195C47E6A1B0}";
    private const string Namespace = "urn:pulseoffice:chart";
    private const int MostLevels = 16;

    public OpenXmlElement Referenced(string formula, ChartData data)
    {
        if (!data.HasCategoryLevels)
            return new C.CategoryAxisData(new C.StringReference { Formula = new C.Formula(formula), StringCache = Cache(data.Categories) });

        var cache = new C.MultiLevelStringCache(new C.PointCount { Val = (uint)data.Categories.Count });
        cache.Append(Level(data.Categories, false));
        foreach (var level in data.CategoryLevels!.Reverse()) cache.Append(Level(level, true));
        return new C.CategoryAxisData(new C.MultiLevelStringReference(new C.Formula(formula), cache));
    }

    public C.StringLiteral Literal(ChartData data)
    {
        var literal = new C.StringLiteral(new C.PointCount { Val = (uint)data.Categories.Count });
        for (var i = 0; i < data.Categories.Count; i++)
            literal.Append(new C.StringPoint { Index = (uint)i, NumericValue = new C.NumericValue(data.Categories[i]) });
        if (!data.HasCategoryLevels) return literal;

        var marker = new OpenXmlUnknownElement("pulseoffice", "categoryLevels", Namespace);
        foreach (var level in data.CategoryLevels!)
        {
            var lane = new OpenXmlUnknownElement("pulseoffice", "level", Namespace);
            for (var i = 0; i < level.Count; i++)
            {
                if (level[i].Length == 0) continue;
                var point = new OpenXmlUnknownElement("pulseoffice", "pt", Namespace);
                point.SetAttribute(new OpenXmlAttribute("idx", string.Empty, i.ToString(CultureInfo.InvariantCulture)));
                point.SetAttribute(new OpenXmlAttribute("v", string.Empty, level[i]));
                lane.AppendChild(point);
            }
            marker.AppendChild(lane);
        }
        literal.Append(new C.StrDataExtensionList(new C.StrDataExtension(marker) { Uri = Uri }));
        return literal;
    }

    public IReadOnlyList<IReadOnlyList<string>>? Read(OpenXmlElement? axis, int count)
    {
        if (axis is null || count <= 0) return null;
        if (axis.Descendants<C.MultiLevelStringCache>().FirstOrDefault() is { } cache)
        {
            var outer = cache.Elements<C.Level>().Skip(1).Take(MostLevels).Select(level => Labels(level.Elements<C.StringPoint>()
                .Select(point => (point.Index?.Value, point.NumericValue?.Text ?? string.Empty)), count)).Reverse().ToList();
            return outer.Count > 0 ? outer : null;
        }

        var levels = axis.Descendants()
            .Where(element => element.LocalName == "categoryLevels" && element.NamespaceUri == Namespace)
            .SelectMany(marker => marker.ChildElements.Where(element => element.LocalName == "level"))
            .Take(MostLevels)
            .Select(level => Labels(level.ChildElements.Where(point => point.LocalName == "pt").Select(point => (Index(point), Attribute(point, "v"))), count))
            .ToList();
        return levels.Count > 0 ? levels : null;
    }

    public IEnumerable<C.StringPoint> Leaves(OpenXmlElement axis) =>
        axis.Descendants<C.MultiLevelStringCache>().FirstOrDefault() is { } cache
            ? cache.Elements<C.Level>().FirstOrDefault()?.Elements<C.StringPoint>() ?? []
            : axis.Descendants<C.StringPoint>();

    private C.StringCache Cache(IReadOnlyList<string> categories)
    {
        var cache = new C.StringCache(new C.PointCount { Val = (uint)categories.Count });
        for (var i = 0; i < categories.Count; i++) cache.AppendChild(new C.StringPoint(new C.NumericValue(categories[i])) { Index = (uint)i });
        return cache;
    }

    private C.Level Level(IReadOnlyList<string> labels, bool sparse)
    {
        var level = new C.Level();
        for (var i = 0; i < labels.Count; i++)
            if (!sparse || labels[i].Length > 0)
                level.Append(new C.StringPoint(new C.NumericValue(labels[i])) { Index = (uint)i });
        return level;
    }

    private List<string> Labels(IEnumerable<(uint? Index, string Text)> points, int count)
    {
        var labels = Enumerable.Repeat(string.Empty, count).ToList();
        foreach (var (index, text) in points)
            if (index is { } position && position < count) labels[(int)position] = text;
        return labels;
    }

    private uint? Index(OpenXmlElement point) =>
        uint.TryParse(Attribute(point, "idx"), NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : null;

    private string Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value ?? string.Empty;
}
