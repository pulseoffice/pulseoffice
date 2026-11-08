using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Typography.OpenType;

namespace PulseOffice.Typography.Embedding;

public interface IUsedFontCollector
{
    bool Handles(object model);

    IReadOnlyCollection<string> Families(object model);
}

public interface IUsedFonts
{
    IReadOnlyCollection<string> Of(object? model);
}

public sealed class UsedFonts(IEnumerable<IUsedFontCollector> collectors) : IUsedFonts
{
    private readonly IReadOnlyList<IUsedFontCollector> _collectors = [.. collectors];

    public IReadOnlyCollection<string> Of(object? model) =>
        model is null
            ? []
            : _collectors.Where(collector => collector.Handles(model))
                .SelectMany(collector => collector.Families(model))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}

public sealed record FontEmbeddingReport(
    IReadOnlyList<string> Embedded,
    IReadOnlyList<string> Restricted,
    IReadOnlyList<string> Unavailable)
{
    public static readonly FontEmbeddingReport Nothing = new([], [], []);

    public bool HasRefusals => Restricted.Count > 0;
}

public interface IDocumentFontEmbedder
{
    Task<FontEmbeddingReport> PrepareAsync(object model, CancellationToken ct = default);
}

public sealed class DocumentFontEmbedder(IFontCatalog catalog, IUsedFonts used, IOpenTypeReader reader) : IDocumentFontEmbedder
{
    private const long MaximumFamilySize = 24L * 1024 * 1024;

    public async Task<FontEmbeddingReport> PrepareAsync(object model, CancellationToken ct = default)
    {
        if (model is not IFontDocument document) return FontEmbeddingReport.Nothing;

        await catalog.ListAsync(ct).ConfigureAwait(false);

        var families = used.Of(model);
        var current = document.Fonts;
        var files = new List<EmbeddedFontFile>();
        var embedded = new List<string>();
        var restricted = new List<string>();
        var unavailable = new List<string>();

        foreach (var family in families)
        {
            var kept = current.FilesOf(family).ToList();
            if (kept.Count > 0)
            {
                files.AddRange(kept);
                embedded.Add(family);
                continue;
            }

            if (catalog.Find(family) is not { } info) continue;

            var wanted = info.Origin == FontOrigin.Downloaded || (current.EmbedSystemFonts && info.Origin == FontOrigin.System && info.Faces.Count > 0);
            if (!wanted) continue;

            if (!info.Embedding.AllowsEmbedding && info.Embedding.Level != EmbeddingLevel.Unknown)
            {
                restricted.Add(family);
                continue;
            }

            var acquired = await catalog.AcquireAsync(info, null, ct).ConfigureAwait(false);
            if (!acquired.Succeeded || acquired.Value is not { Count: > 0 } loaded || loaded.Sum(file => (long)file.Data.Length) > MaximumFamilySize)
            {
                unavailable.Add(family);
                continue;
            }

            if (loaded.Any(file => !Permits(file)))
            {
                restricted.Add(family);
                continue;
            }

            files.AddRange(loaded);
            embedded.Add(family);
        }

        document.Fonts = current with { Files = files };
        return new FontEmbeddingReport(embedded, restricted, unavailable);
    }

    private bool Permits(EmbeddedFontFile file)
    {
        try
        {
            return reader.Read(file.Data).All(face => face.Embedding.AllowsEmbedding);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentException)
        {
            return false;
        }
    }
}
