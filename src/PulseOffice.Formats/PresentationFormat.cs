using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;

namespace PulseOffice.Formats;

public sealed record PresentationFormat : IDocumentFormat
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Extensions { get; init; }
    public required string MimeType { get; init; }
    public bool IsNative { get; init; }

    public bool Matches(string path) =>
        Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}

public interface IPresentationReader : IDocumentReader<Presentation, PresentationFormat>;

public interface IPresentationWriter : IDocumentWriter<Presentation, PresentationFormat>;
