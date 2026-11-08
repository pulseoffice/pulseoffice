using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Documents.Objects;

public interface IGroupStories
{
    EmbeddedContent Member(string id, TextBoxContent box);

    void Collect(EmbeddedContent member, IDictionary<string, BlockContainer> stories);

    EmbeddedContent Child(EmbeddedObject member, IReadOnlyDictionary<string, BlockContainer> stories);

    EmbeddedContent Released(EmbeddedObject member, string id, IReadOnlyDictionary<string, BlockContainer> stories);
}

public sealed class GroupStories : IGroupStories
{
    private readonly BlockText _text = new();

    public EmbeddedContent Member(string id, TextBoxContent box) => new(new ShapeObject
    {
        Id = id,
        Shape = ShapeKind.Rectangle,
        Style = new ShapeStyle
        {
            Fill = box.Look?.Background is { } background ? ShapeFill.Default with { ColorHex = Hex(background) } : ShapeFill.None,
            Line = box.Look is { BorderWidth: not null } or { BorderColor: not null }
                ? ShapeLine.Default with { ColorHex = Hex(box.Look.BorderColor ?? 0xFF000000), Width = PulseOffice.Foundation.Units.Twip.FromPoints(box.Look.BorderWidth ?? 0.5) }
                : ShapeLine.None,
            Text = new ShapeTextStyle { ColorHex = "#000000", Vertical = TextVerticalPlacement.Top },
        },
        Text = _text.Of(box.Content) is { Length: > 0 } text ? text : null,
    }, box.Content);

    private string Hex(uint argb) => "#" + (argb & 0xFFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);

    public void Collect(EmbeddedContent member, IDictionary<string, BlockContainer> stories)
    {
        if (member.Story is { Blocks.Count: > 0 } story) stories[member.Value.Id] = story;
        foreach (var (id, nested) in member.MemberStories) stories[id] = nested;
    }

    public EmbeddedContent Child(EmbeddedObject member, IReadOnlyDictionary<string, BlockContainer> stories) => member switch
    {
        ShapeObject shape when stories.TryGetValue(shape.Id, out var story) => new EmbeddedContent(shape with { Text = null }, story),
        ShapeGroupObject group => new EmbeddedContent(group, null, Nested(group, stories)),
        _ => new EmbeddedContent(member),
    };

    public EmbeddedContent Released(EmbeddedObject member, string id, IReadOnlyDictionary<string, BlockContainer> stories)
    {
        var child = Child(member, stories);
        return new EmbeddedContent(child.Value with { Id = id }, child.Story, child.MemberStories);
    }

    private Dictionary<string, BlockContainer> Nested(ShapeGroupObject group, IReadOnlyDictionary<string, BlockContainer> stories)
    {
        var found = new Dictionary<string, BlockContainer>(StringComparer.Ordinal);
        foreach (var child in group.Children)
        {
            if (child.Content is ShapeGroupObject inner)
            {
                foreach (var (id, story) in Nested(inner, stories)) found[id] = story;
            }
            else if (stories.TryGetValue(child.Content.Id, out var story)) found[child.Content.Id] = story;
        }
        return found;
    }
}
