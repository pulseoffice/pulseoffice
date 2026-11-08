using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Spelling;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class TextReviewWatchTests(HeadlessDesktopSession headless)
{
    [Fact]
    public Task Una_modifica_al_documento_mentre_il_pannello_aspetta_fa_rileggere_il_testo() => headless.Session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        provider.GetRequiredService<ShellWindow>().Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var folio = (PulseOffice.Documents.UI.FolioView)workspace.Active!.View!;

        var prompt = provider.GetRequiredService<ISpellingPrompt>();
        var pending = prompt.AskAsync(new SpellingFinding("perche", "Paragrafo 1", ["perché"]), "command.folio.spelling.check.title", CancellationToken.None);
        Dispatcher.UIThread.RunJobs();
        folio.Editor.Type("x");
        Dispatcher.UIThread.RunJobs();

        Assert.True(pending.IsCompleted);
        Assert.Equal(SpellingDecision.Recheck, (await pending).Decision);
    }, CancellationToken.None);
}
