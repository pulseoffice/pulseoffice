using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Search;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Presentations.UI;

public sealed class DeckFindCommands(
    IActiveDeckContext context, IDialogService dialogs, ITextCatalog texts) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Prototype("deck.edit.findReplace") with
        {
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            DefaultGesture = "Ctrl+F",
            EnabledWhen = _ => Context.Target is not null,
            Execute = (_, ct) => dialogs.ShowAsync<object>(
                new FindReplaceView(new DeckFindTarget(Context, texts), texts),
                new DialogOptions { TitleKey = "command.deck.edit.findReplace.title", PreferredWidth = 420 },
                ct),
        },
    ];
}
