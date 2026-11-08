using Microsoft.Extensions.Logging;
using PulseOffice.Exchange;
using PulseOffice.Formats;

namespace PulseOffice.Presentations.UI.Exchange;

public interface IDeckSelectionPackages
{
    IReadOnlyList<ExchangeRepresentation> Packages(Presentation deck, Slide slide, IReadOnlyList<SlideElement> selection, ElementBehaviourSet behaviours);
}

public sealed class DeckSelectionPackages(IPresentationFormatRegistry formats, ILogger<DeckSelectionPackages> logger) : IDeckSelectionPackages
{
    private readonly IReadOnlyList<(string Extension, ExchangeFlavor Flavor, string MediaType)> _packages =
    [
        (".odp", ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Presentation),
        (".pptx", ExchangeFlavor.OpenXml, ExchangeMediaTypes.Slides),
    ];

    public IReadOnlyList<ExchangeRepresentation> Packages(Presentation deck, Slide slide, IReadOnlyList<SlideElement> selection, ElementBehaviourSet behaviours)
    {
        ArgumentNullException.ThrowIfNull(behaviours);
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(selection);
        var chosen = slide.Elements.Where(selection.Contains).ToList();
        if (chosen.Count == 0) return [];

        var portion = Portion(deck, slide, chosen, behaviours);
        var result = new List<ExchangeRepresentation>();
        foreach (var (extension, flavor, mediaType) in _packages)
        {
            if (formats.WriterFor("selection" + extension) is not { } writer) continue;
            using var stream = new MemoryStream();
            var written = writer.Write(portion, stream);
            if (written.Succeeded) result.Add(new ExchangeRepresentation(flavor, stream.ToArray()) { MediaType = mediaType });
            else logger.LogWarning("Writing the copied slide objects as {Extension} failed", extension);
        }
        return result;
    }

    private Presentation Portion(Presentation deck, Slide slide, IReadOnlyList<SlideElement> chosen, ElementBehaviourSet behaviours)
    {
        var portion = new Presentation
        {
            Size = deck.Size,
            Theme = deck.ThemeOf(slide),
            Fonts = deck.Fonts,
            Master = deck.MasterOf(slide),
        };
        foreach (var style in deck.GraphicStyles.All) portion.GraphicStyles.Set(style);
        var copy = new Slide(slide.Id, slide.Layout)
        {
            LayoutId = slide.LayoutId,
            Background = slide.Background,
            BackgroundImage = slide.BackgroundImage,
            BackgroundLayout = slide.BackgroundLayout,
            BackgroundFill = slide.BackgroundFill,
            ShowMasterBackground = slide.ShowMasterBackground,
            ShowMasterObjects = slide.ShowMasterObjects,
        };
        foreach (var element in chosen) copy.Add(element);
        foreach (var effect in behaviours.Animations) copy.AddAnimation(effect);
        foreach (var action in behaviours.Interactions) copy.SetInteraction(action);
        foreach (var sound in behaviours.Sounds) portion.Sounds.Add(sound);
        portion.AddExisting(copy);
        return portion;
    }
}
