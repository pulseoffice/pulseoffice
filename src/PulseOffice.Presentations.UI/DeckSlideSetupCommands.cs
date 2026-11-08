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
            new Design.SlideSizeView(t.Deck.Size, services),
            new DialogOptions { TitleKey = "command.deck.slideSize.title", PreferredWidth = 820 },
            ct).ConfigureAwait(true);
        if (result is null || result.Size == t.Deck.Size) return;
        t.Actions.SetSlideSize(result.Size, result.FitContents);
    }
}
