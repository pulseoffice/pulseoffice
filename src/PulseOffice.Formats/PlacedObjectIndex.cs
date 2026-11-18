using PulseOffice.Document.Embedding;
using PulseOffice.Sheets;

namespace PulseOffice.Formats;

public sealed class PlacedObjectIndex
{
    private readonly Dictionary<string, PlacedObject> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _links = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _tips = new(StringComparer.Ordinal);

    public PlacedObjectIndex(IEnumerable<PlacedObject> objects)
    {
        foreach (var placed in objects)
        {
            _byId.TryAdd(placed.Id, placed);
            if (placed.Hyperlink is { Length: > 0 } link) _links.TryAdd(placed.Id, link);
            if (placed.Hyperlink is { Length: > 0 } && placed.HyperlinkTip is { Length: > 0 } tip) _tips.TryAdd(placed.Id, tip);
        }
    }

    public EmbeddedObject? Content(string id) => _byId.TryGetValue(id, out var placed) ? placed.Content : null;

    public IReadOnlyDictionary<string, string> Links => _links;

    public IReadOnlyDictionary<string, string> Tips => _tips;
}
