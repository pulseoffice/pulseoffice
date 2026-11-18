using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSlideSetupCommands(
    IActiveDeckContext context,
    IDialogService dialogs,
    Design.SlideSizeServices services) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.slideSize") with
        {
            Surface = CommandSurface.Common,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => FormatAsync(ct),
        },
    ];

    private async Task FormatAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t) return;
        var result = await dialogs.ShowAsync<Design.SlideSizeResult>(
            new Design.SlideSizeView(t.Deck.Size, services, t.Deck.FirstSlideNumber),
            new DialogOptions { TitleKey = "command.deck.slideSize.title", PreferredWidth = 820 },
            ct).ConfigureAwait(true);
        if (result is null) return;
        var resize = result.Size != t.Deck.Size;
        var renumber = result.FirstSlideNumber != t.Deck.FirstSlideNumber;
        if (!resize && !renumber) return;
        using var step = t.Actions.History.BeginTransaction(services.Texts["command.deck.slideSize.title"]);
        if (resize) t.Actions.SetSlideSize(result.Size, result.FitContents);
        if (renumber) t.Actions.SetFirstSlideNumber(result.FirstSlideNumber);
    }
}
