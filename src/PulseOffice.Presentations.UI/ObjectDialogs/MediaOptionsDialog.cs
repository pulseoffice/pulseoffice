using System.Globalization;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI.ObjectDialogs;

public sealed class MediaOptionsDialog(IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts)
    : DeckObjectDialog(context, dialogs, texts)
{
    private const double LongestSeconds = 86400;

    public override string Id => "deck.object.mediaOptions";

    protected override string HistoryKey => "deck.action.mediaOptions";

    protected override bool Accepts(SlideElement element) => element is MediaElement;

    protected override async Task<SlideElement?> EditAsync(SlideElement element, Presentation deck, CancellationToken ct)
    {
        var media = (MediaElement)element;
        var values = await Dialogs.ShowAsync<Dictionary<string, object>>(
            new FormPromptView(
                Texts["deck.media.options.explanation"],
                [
                    new CheckField("loop", Texts["deck.media.options.loop"], media.Loop),
                    new CheckField("mute", Texts["deck.media.options.mute"], media.Muted),
                    new NumberField("volume", Texts["deck.media.options.volume"], Math.Round(media.Volume * 100), 0, 100, 1) { Unit = "%", Integer = true, ShowSlider = true },
                    Seconds("trimStart", "deck.media.options.trimStart", media.TrimStart),
                    Seconds("trimEnd", "deck.media.options.trimEnd", media.TrimEnd),
                    Seconds("fadeIn", "deck.media.options.fadeIn", media.FadeIn),
                    Seconds("fadeOut", "deck.media.options.fadeOut", media.FadeOut),
                ],
                Texts),
            new DialogOptions { TitleKey = "command.deck.object.mediaOptions.title", PreferredWidth = 420 },
            ct).ConfigureAwait(true);

        if (values is null) return null;

        return media with
        {
            Loop = Convert.ToBoolean(values["loop"], CultureInfo.InvariantCulture),
            Muted = Convert.ToBoolean(values["mute"], CultureInfo.InvariantCulture),
            Volume = Math.Clamp(Number(values["volume"]) / 100, 0, 1),
            TrimStart = Duration(values["trimStart"]),
            TrimEnd = Duration(values["trimEnd"]),
            FadeIn = Duration(values["fadeIn"]),
            FadeOut = Duration(values["fadeOut"]),
        };
    }

    private NumberField Seconds(string key, string label, TimeSpan initial) =>
        new(key, Texts[label], Math.Round(initial.TotalSeconds, 2), 0, LongestSeconds, 0.25);

    private static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static TimeSpan Duration(object value) =>
        TimeSpan.FromMilliseconds(Math.Round(Math.Clamp(Number(value), 0, LongestSeconds) * 1000));
}
