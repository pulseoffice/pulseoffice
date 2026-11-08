using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Dictionaries;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellDictionaryCommands(DictionaryManagerServices services, IDialogService dialogs)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.dictionaries"),
            MenuPath = "tools",
            Surface = CommandSurface.Advanced,
            Keywords = ["dictionary", "spelling", "hyphenation", "thesaurus", "language", "download"],
            Execute = (_, ct) => OpenAsync(ct),
        },
    ];

    private async Task OpenAsync(CancellationToken ct) =>
        await dialogs.ShowAsync<object>(
            new DictionaryManagerView(services),
            new DialogOptions
            {
                TitleKey = "dictionaries.title",
                PreferredWidth = 1000,
                PreferredHeight = 700,
            },
            ct).ConfigureAwait(true);
}
