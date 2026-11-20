using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFindCommands(
    IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts, PulseOffice.UI.Controls.ISidePanelHost panel, IFindPanelPages pages) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.edit.find") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, _) =>
            {
                pages.Show(panel, () => new TargetFindPanelSource(
                    () => Context.Target is null ? null : new DeckFindTarget(Context, texts),
                    query => Dialog(CancellationToken.None)));
                return Task.CompletedTask;
            },
        },
        Prototype("deck.edit.findReplace") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            DefaultGesture = "Ctrl+F",
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => Dialog(ct),
        },
    ];

    private Task Dialog(CancellationToken ct) => dialogs.ShowAsync<object>(
        new FindReplaceView(new DeckFindTarget(Context, texts), texts),
        new DialogOptions { TitleKey = "command.deck.edit.findReplace.title", PreferredWidth = 420 },
        ct);
}
