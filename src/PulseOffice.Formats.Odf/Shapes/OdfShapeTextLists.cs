using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed record OdfShapeListSource(IReadOnlyDictionary<string, XElement> Styles, Func<string, byte[]?>? ReadBytes = null);

internal sealed class OdfShapeTextLists(IOdfListPlacement placement, ListStyleBuilder builder, IBulletPictureSizing sizing) : OdfXml
{
    public const string KeyPrefix = "text:list-style:";

    public OdfShapeTextLists() : this(new ListPlacement(new OdfValues()), new ListStyleBuilder(), new BulletPictureSizing())
    {
    }

    public IReadOnlyDictionary<string, XElement> Styles(IEnumerable<XElement> candidates) =>
        candidates.Where(element => element.Name == Text + "list-style")
            .GroupBy(element => (string?)element.Attribute(Style + "name") ?? string.Empty)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public ShapeListItem? Read(XElement paragraph, XElement container, OdfShapeListSource source)
    {
        var position = placement.Of(paragraph, container, source.Styles, null, source.ReadBytes);
        if (!position.Item) return null;
        return new ShapeListItem
        {
            Level = Math.Clamp(position.Depth, 0, ShapeTextLists.LevelCount - 1),
            Numbered = position.Numbered,
            NumberFormat = position.Numbered ? position.NumberFormat : null,
            Symbol = position.Numbered ? null : position.Symbol,
            Margin = position.Margin,
            Indent = position.Indent,
        };
    }

    public IReadOnlyList<XElement> Wrap(IReadOnlyList<XElement> paragraphs, IReadOnlyList<ShapeListItem?> items, double fontSize, IOdfTextStyleSink sink)
    {
        var wrapped = new List<XElement>();
        var registered = new Dictionary<ListGroupKey, string>();
        ListGroup? group = null;

        void Close()
        {
            if (group is null) return;
            var key = new ListGroupKey(group.Marker, string.Join(';', group.Levels.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")));
            if (!registered.TryGetValue(key, out var name))
            {
                name = sink.RegisterList(ListStyle(group, fontSize));
                registered[key] = name;
            }
            group.List.SetAttributeValue(Text + "style-name", name);
            group = null;
        }

        for (var index = 0; index < paragraphs.Count; index++)
        {
            if ((index < items.Count ? items[index] : null) is not { } item)
            {
                Close();
                wrapped.Add(paragraphs[index]);
                continue;
            }
            var marker = item with { Level = 0, Margin = null, Indent = null };
            var geometry = builder.Geometry(new TextParagraph(string.Empty) { MarginLeft = item.Margin, Indent = item.Indent });
            if (group is not null && !group.Accepts(marker, item.Level, geometry)) Close();
            if (group is null)
            {
                group = new ListGroup(marker, new XElement(Text + "list"));
                wrapped.Add(group.List);
            }
            group.Claim(item.Level, geometry);
            group.List.Add(builder.Entry(paragraphs[index], new TextParagraph(string.Empty) { BulletLevel = item.Level }));
        }
        Close();
        return wrapped;
    }

    private XElement ListStyle(ListGroup group, double fontSize)
    {
        var media = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var item = group.Marker;
        var style = builder.Automatic(string.Empty, item.Numbered, item.Numbered ? null : Fixed(item.Symbol, fontSize), (href, data, _) => media[href] = data, item.Numbered ? item.NumberFormat : null);
        foreach (var (level, geometry) in group.Levels) builder.Place(style, level, geometry);
        foreach (var image in style.Elements(Text + "list-level-style-image"))
        {
            if ((string?)image.Attribute(Xlink + "href") is not { } href || !media.TryGetValue(href, out var data)) continue;
            foreach (var link in image.Attributes().Where(attribute => attribute.Name.Namespace == Xlink).ToList()) link.Remove();
            image.AddFirst(new XElement(Office + "binary-data", Convert.ToBase64String(data)));
        }
        return style;
    }

    private sealed record ListGroupKey(ShapeListItem Marker, string Levels);

    private sealed class ListGroup(ShapeListItem marker, XElement list)
    {
        private readonly Dictionary<int, ListLevelGeometry> _levels = [];

        public ShapeListItem Marker => marker;

        public XElement List => list;

        public IReadOnlyDictionary<int, ListLevelGeometry> Levels => _levels;

        public bool Accepts(ShapeListItem candidate, int level, ListLevelGeometry? geometry) =>
            candidate == marker && (geometry is not { } wanted ? !_levels.ContainsKey(level) : !_levels.TryGetValue(level, out var placed) || placed == wanted);

        public void Claim(int level, ListLevelGeometry? geometry)
        {
            if (geometry is { } wanted) _levels.TryAdd(level, wanted);
        }
    }

    private BulletSymbol? Fixed(BulletSymbol? symbol, double fontSize) =>
        symbol?.Picture is { ScalesWithFont: true } picture
            ? symbol with { Picture = sizing.Size(picture, fontSize * (symbol.RelativeSize ?? 1)) with { ScalesWithFont = false }, RelativeSize = null }
            : symbol;
}
