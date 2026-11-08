using PulseOffice.Document.Embedding;

namespace PulseOffice.Formats.OpenXml.Embedding;

public sealed class OoxmlOleReplacement
{
    private readonly byte[] _placeholder = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    public byte[] Picture(PackagedObject package)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.Replacement is { Length: > 0 } replacement ? replacement.ToArray() : (byte[])_placeholder.Clone();
    }

    public ReadOnlyMemory<byte>? Replacement(byte[]? picture) =>
        picture is { Length: > 0 } && !picture.AsSpan().SequenceEqual(_placeholder) ? picture : null;
}
