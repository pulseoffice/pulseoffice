using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Presentations.UI;

public sealed class DeckMediaCommands(IActiveDeckContext context, IMediaPlayer player, IDeckFileInsertion insertion) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.tools.mediaPlayer") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => ShowAsync(ct),
        },
    ];

    private async Task ShowAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;

        var media = t.Deck.Slides.SelectMany(slide => slide.Elements.OfType<MediaElement>().Select(element => (slide, element))).ToList();
        var items = media.Select((entry, index) => new MediaItem(
            entry.element.Id,
            entry.element.FileName ?? $"{t.Deck.Slides.ToList().IndexOf(entry.slide) + 1}.{index + 1}",
            entry.element.MimeType,
            entry.element.Data)).ToList();

        if (await player.ShowAsync(items, "command.deck.tools.mediaPlayer.title", ct).ConfigureAwait(true) is not { } chosen) return;

        if (!chosen.FromFile)
        {
            var found = media.FirstOrDefault(entry => entry.element.Id == chosen.Id);
            if (found.slide is not null)
            {
                t.GoToSlide(t.Deck.Slides.ToList().IndexOf(found.slide));
                t.Select([found.element]);
            }
            return;
        }

        if (t.Actions.IsReadOnly || t.CurrentSlide() is not { } slide) return;
        insertion.PlaceMedia(t, slide, chosen.Name, chosen.MediaType, chosen.Data.ToArray());
    }
}
