using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Exchange;

public interface IDeckSlideClipboard
{
    ExchangePackage? Capture(string id, DeckTarget target, bool cut);

    IReadOnlyList<ExchangePasteMode> Modes(DeckTarget? target, DeckSlidesNative copied);

    bool Receive(DeckTarget target, DeckSlidesNative copied, ExchangePasteMode mode);
}

public sealed class DeckSlideClipboard(ISlideFactory slideFactory) : IDeckSlideClipboard
{
    public ExchangePackage? Capture(string id, DeckTarget target, bool cut)
    {
        ArgumentNullException.ThrowIfNull(target);
        var slides = target.SlidesToCopy();
        if (slides.Count == 0) return null;
        var copies = slides.Select(slide =>
        {
            var copy = slideFactory.Clone(slide, slide.Id);
            copy.MasterId = slide.MasterId;
            return copy;
        }).ToList();
        var titles = slides
            .Select(slide => slide.Placeholder(PlaceholderRole.Title)?.Text.PlainText ?? string.Empty)
            .Where(title => title.Length > 0)
            .ToList();
        var package = new ExchangePackage
        {
            Id = id,
            Origin = ModuleKind.Presentations,
            Native = new DeckSlidesNative(target.Deck, copies),
            Representations = titles.Count > 0 ? [ExchangeRepresentation.OfText(ExchangeFlavor.Text, string.Join("\n", titles))] : [],
        };
        if (cut && !target.Actions.IsReadOnly) target.Actions.DeleteSlides(slides, "deck.action.cut");
        return package;
    }

    public IReadOnlyList<ExchangePasteMode> Modes(DeckTarget? target, DeckSlidesNative copied)
    {
        ArgumentNullException.ThrowIfNull(copied);
        return target is not null && !ReferenceEquals(target.Deck, copied.Source)
            ? [ExchangePasteMode.Native, ExchangePasteMode.KeepSourceFormatting]
            : [ExchangePasteMode.Native];
    }

    public bool Receive(DeckTarget target, DeckSlidesNative copied, ExchangePasteMode mode)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(copied);
        var after = target.CurrentIndex();
        switch (mode)
        {
            case ExchangePasteMode.KeepSourceFormatting when !ReferenceEquals(target.Deck, copied.Source):
                target.Actions.ImportSlidesKeepingFormatting(after, copied.Source, copied.Slides);
                return true;
            case ExchangePasteMode.Native or ExchangePasteMode.KeepSourceFormatting:
                target.Actions.PasteSlides(after, copied.Source, copied.Slides);
                return true;
            default:
                return false;
        }
    }
}
