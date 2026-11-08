using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations;

public sealed record SlideComment
{
    public required string Id { get; init; }

    public required string Author { get; init; }

    public required string Text { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public Twip X { get; init; }

    public Twip Y { get; init; }
}
