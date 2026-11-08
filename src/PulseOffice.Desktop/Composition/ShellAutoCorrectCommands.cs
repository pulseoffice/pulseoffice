using PulseOffice.Localization;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.AutoCorrection;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Desktop.Composition;

public sealed class ShellAutoCorrectCommands(
    IAutoCorrectPreferences preferences,
    IAutoCorrectSettings settings,
    AutoCorrectViewServices viewServices,
    IDialogService dialogs,
    IStatusBarService status,
    ITextCatalog texts)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.autocorrect"),
            MenuPath = "tools",
            Surface = CommandSurface.Advanced,
            Keywords = ["autocorrect", "replace", "typo", "abbreviation", "capital"],
            Execute = (_, ct) => OpenAsync(ct),
        },
        new CommandDefinition
        {
            Id = new CommandId("shell.tools.autocorrectWhileTyping"),
            MenuPath = "tools",
            Surface = CommandSurface.Advanced,
            CheckedWhen = _ => settings.Enabled,
            Execute = (_, _) =>
            {
                preferences.Apply(preferences.Current with { Enabled = !preferences.Current.Enabled });
                status.Report(texts[settings.Enabled ? "autocorrect.typing.on" : "autocorrect.typing.off"]);
                return Task.CompletedTask;
            },
        },
    ];

    private async Task OpenAsync(CancellationToken ct)
    {
        var profile = await dialogs.ShowAsync<AutoCorrectProfile>(
            new AutoCorrectView(viewServices, preferences.Current),
            new DialogOptions
            {
                TitleKey = "autocorrect.title",
                PreferredWidth = 1000,
                PreferredHeight = 700,
                CanCancelWithEscape = false,
            },
            ct).ConfigureAwait(true);

        if (profile is null) return;

        preferences.Apply(profile);
        status.Report(texts["autocorrect.done"]);
    }
}
