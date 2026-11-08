using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Presentations.UI;

public interface IDeckFileInsertion
{
    Task InsertImageAsync(CancellationToken ct);

    Task InsertMediaAsync(CancellationToken ct);

    MediaElement PlaceMedia(DeckTarget target, Slide slide, string fileName, string mediaType, byte[] data);
}

public sealed class DeckFileInsertion(IActiveDeckContext context, IRasterImageFiles images, IPackagedFilePrompt files) : IDeckFileInsertion
{
    public async Task InsertImageAsync(CancellationToken ct)
    {
        if (context.Target is not { } target || target.CurrentSlide() is null) return;
        if (await images.PickAsync(ct).ConfigureAwait(true) is not { } source) return;
        target.InsertImageBytes(source.Data, source.FileName);
    }

    public async Task InsertMediaAsync(CancellationToken ct)
    {
        if (context.Target is not { } target || target.CurrentSlide() is null) return;
        if (await files.PickAsync(PackagedObjectKind.Media, Guid.NewGuid().ToString("N"), ct).ConfigureAwait(true) is not { } media) return;
        if (target.CurrentSlide() is not { } slide) return;
        PlaceMedia(target, slide, media.FileName, media.MediaType, media.Data.ToArray());
    }

    public MediaElement PlaceMedia(DeckTarget target, Slide slide, string fileName, string mediaType, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(slide);
        var video = mediaType.StartsWith("video", StringComparison.Ordinal);
        var width = Twip.FromCentimeters(video ? 16 : 4);
        var height = Twip.FromCentimeters(video ? 9 : 4);
        var element = new MediaElement
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = video ? MediaKind.Video : MediaKind.Audio,
            Data = data,
            MimeType = mediaType,
            FileName = fileName,
            Bounds = new DocumentRect(new Twip((target.Deck.Size.Width.Value - width.Value) / 2), new Twip((target.Deck.Size.Height.Value - height.Value) / 2), width, height),
            ZOrder = slide.Elements.Count,
        };
        target.Actions.InsertMedia(slide, element);
        target.Select([element]);
        return element;
    }
}
