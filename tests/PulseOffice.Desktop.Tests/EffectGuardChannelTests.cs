using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Agent.Hosting;
using PulseOffice.Desktop.Composition;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class EffectGuardChannelTests : IDisposable
{
    private readonly UiThreadSession Session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-guards-" + Guid.NewGuid().ToString("N"));

    public EffectGuardChannelTests(HeadlessDesktopSession headless)
    {
        Session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private sealed class Folio(ServiceProvider provider, ShellWindow window) : IAsyncDisposable
    {
        public ShellWindow Window => window;

        public T Service<T>() where T : notnull => provider.GetRequiredService<T>();

        public FolioEditor Editor => Service<IActiveFolioContext>().Target!.Editor;

        public ParagraphBlock Guarded => Editor.Document.Body.Blocks.OfType<SectionBlock>().Single().Content.Blocks.OfType<ParagraphBlock>().Single();

        public void WriteIntoGuarded() =>
            Editor.Services.Editor.InsertText(Editor.Document, Editor.History, new DocumentPosition(Guarded.Id, 0), "x");

        public ValueTask DisposeAsync()
        {
            window.Close();
            return provider.DisposeAsync();
        }
    }

    private static async Task<Folio> OpenWithProtectedSection()
    {
        var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var window = provider.GetRequiredService<ShellWindow>();
        window.Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        await provider.GetRequiredService<CommandExecutor>().ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var folio = new Folio(provider, window);
        var editor = folio.Editor;
        editor.Type("Riservato");
        editor.Change(() => folio.Service<ISectionEditing>().Wrap(editor.Document, editor.History, editor.ParagraphRange(),
            new SectionSettings("Riservata", SectionColumns.Single, true, false)));
        return folio;
    }

    [Fact]
    public Task Una_modifica_dell_agente_a_una_sezione_protetta_si_annulla_e_l_agente_riceve_un_errore() => Session.Dispatch(async () =>
    {
        await using var folio = await OpenWithProtectedSection();
        var agent = folio.Service<IAgentWorkspace>();

        var error = await Assert.ThrowsAsync<AgentHostException>(() => agent.WithDocumentAsync(
            new AgentTarget(null, ModuleKind.Documents), "scrivi",
            (_, _) =>
            {
                folio.WriteIntoGuarded();
                return Task.FromResult(true);
            }, CancellationToken.None));

        Assert.Contains("protected", error.Message, StringComparison.Ordinal);
        Assert.Equal("Riservato", folio.Guarded.Text);
    }, CancellationToken.None);

    [Fact]
    public Task Una_macro_eseguita_dall_editor_che_scrive_in_una_sezione_protetta_si_annulla() => Session.Dispatch(async () =>
    {
        await using var folio = await OpenWithProtectedSection();
        var runs = folio.Service<PulseOffice.Scripting.Editor.IGuardedMacroRuns>();

        using (runs.Begin(folio.Editor.History))
            folio.WriteIntoGuarded();

        Assert.Equal("Riservato", folio.Guarded.Text);
    }, CancellationToken.None);

    [Fact]
    public Task Ripetere_l_ultimo_comando_in_una_sezione_protetta_si_rifiuta() => Session.Dispatch(async () =>
    {
        await using var folio = await OpenWithProtectedSection();
        var executor = folio.Service<CommandExecutor>();
        var registry = folio.Service<CommandRegistry>();
        var workspace = folio.Service<IDocumentWorkspace>();
        folio.Editor.Document.Body.Insert(folio.Editor.Document.Body.Count, new ParagraphBlock("libero", "Libero"));
        folio.Editor.Reflow();
        folio.Editor.Select(new DocumentPosition("libero", 0), new DocumentPosition("libero", 6));
        var first = await executor.ExecuteAsync(registry.Find(new CommandId("folio.list.style.number"))!, workspace.Context);
        Assert.Equal(CommandOutcomeKind.Ran, first.Kind);
        folio.Editor.Select(new DocumentPosition(folio.Guarded.Id, 0), new DocumentPosition(folio.Guarded.Id, 3));

        var repeated = await executor.ExecuteAsync(registry.Find(new CommandId("shell.edit.repeat"))!, workspace.Context);

        Assert.Equal(CommandOutcomeKind.Refused, repeated.Kind);
        Assert.Null(folio.Guarded.Content.ListStyleName);
        Assert.NotNull(folio.Editor.Document.Body.Blocks.OfType<ParagraphBlock>().Single(paragraph => paragraph.Id == "libero").Content.ListStyleName);
    }, CancellationToken.None);

    [Fact]
    public Task Annullare_da_tastiera_rispetta_la_sezione_protetta_come_il_comando() => Session.Dispatch(async () =>
    {
        await using var folio = await OpenWithProtectedSection();
        var view = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(folio.Window).OfType<FolioView>().Single();

        view.Perform(PulseOffice.UI.Controls.Text.TextEditingAction.Undo);

        Assert.Equal("Riservato", folio.Guarded.Text);
    }, CancellationToken.None);
}
