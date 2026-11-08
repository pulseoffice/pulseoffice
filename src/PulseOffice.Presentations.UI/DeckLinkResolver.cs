using PulseOffice.Drawing.Assets;

namespace PulseOffice.Presentations.UI;

public interface IDeckLinkResolver
{
    void Resolve(Presentation deck, string documentPath);
}

public sealed class DeckLinkResolver : IDeckLinkResolver
{
    public void Resolve(Presentation deck, string documentPath)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        foreach (var slide in deck.Slides)
            foreach (var element in slide.Elements.OfType<ObjectElement>().ToList())
            {
                if (element.Content is not RasterImageObject { LinkPath: { Length: > 0 } link } image) continue;
                if (Path.IsPathRooted(link) || Uri.TryCreate(link, UriKind.Absolute, out _)) continue;

                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(documentPath)) ?? string.Empty, link.Replace('/', Path.DirectorySeparatorChar)));
                slide.Replace(element with { Content = image with { LinkPath = resolved } });
            }
    }
}
