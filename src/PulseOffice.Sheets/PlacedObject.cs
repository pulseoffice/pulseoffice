using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public sealed record PlacedObject
{
    public required string Id { get; init; }
    public required EmbeddedObject Content { get; init; }

    public required CellAddress Anchor { get; init; }

    public Twip OffsetX { get; init; } = Twip.Zero;
    public Twip OffsetY { get; init; } = Twip.Zero;

    public DocumentSize Size { get; init; } =
        new(Twip.FromCentimeters(12), Twip.FromCentimeters(7));

    public int ZOrder { get; init; }

    public ObjectAnchoring Anchoring { get; init; } = ObjectAnchoring.Move;

    public string? Hyperlink { get; init; }

    public bool IsSelected { get; init; }
}
