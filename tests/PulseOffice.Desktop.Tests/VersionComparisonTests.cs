using PulseOffice.Testing;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Document.Versions;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using PulseOffice.UI.Controls.Prompts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class VersionComparisonTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pulseoffice-versions-" + Guid.NewGuid().ToString("N"));

    public VersionComparisonTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _folder);
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public Task Le_differenze_dalla_versione_si_trovano_nei_tre_moduli() => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        provider.GetRequiredService<ShellWindow>().Show();
        var workspace = provider.GetRequiredService<IDocumentWorkspace>();
        var executor = provider.GetRequiredService<CommandExecutor>();
        var operations = provider.GetRequiredService<IDocumentOperationsCatalog>();

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.sheets"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var sheetDocument = workspace.Active!;
        var workbook = (PulseOffice.Sheets.Workbook)sheetDocument.Model!;
        var a1 = new PulseOffice.Sheets.Addressing.CellAddress(0, 0);
        var b2 = new PulseOffice.Sheets.Addressing.CellAddress(1, 1);
        workbook.Sheets[0].SetValue(a1, PulseOffice.Sheets.Values.CellValue.Text("Bilancio 2025"));
        var sheets = operations.For(sheetDocument)!;
        var sheetVersion = Path.Combine(_folder, "v.ods");
        Assert.True(sheets.WriteTo(sheetDocument, sheetVersion));
        workbook.Sheets[0].SetValue(a1, PulseOffice.Sheets.Values.CellValue.Text("Bilancio 2026"));
        workbook.Sheets[0].SetValue(b2, PulseOffice.Sheets.Values.CellValue.Number(42));

        var cells = Assert.IsType<VersionComparison>(sheets.CompareVersion(sheetDocument, sheetVersion));
        var changed = Assert.Single(cells.Changes, change => change.Kind == VersionChangeKind.Changed);
        Assert.EndsWith("!A1", changed.Location, StringComparison.Ordinal);
        Assert.Contains(changed.Runs, run => run.Kind == VersionRunKind.Removed && run.Text == "2025");
        Assert.Contains(changed.Runs, run => run.Kind == VersionRunKind.Added && run.Text == "2026");
        Assert.Contains(cells.Changes, change => change.Kind == VersionChangeKind.Added && change.Location.EndsWith("!B2", StringComparison.Ordinal));

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.presentations"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var deckDocument = workspace.Active!;
        var deck = (Presentation)deckDocument.Model!;
        deck.Slides[0].Add(Box("uno", "Ricavi in crescita"));
        var slides = operations.For(deckDocument)!;
        var deckVersion = Path.Combine(_folder, "v.odp");
        Assert.True(slides.WriteTo(deckDocument, deckVersion));
        deck.Slides[0].Replace(Box("uno", "Ricavi in calo"));

        var deckChanges = Assert.IsType<VersionComparison>(slides.CompareVersion(deckDocument, deckVersion));
        var slide = Assert.Single(deckChanges.Changes);
        Assert.Equal(VersionChangeKind.Changed, slide.Kind);
        Assert.Contains("1", slide.Location, StringComparison.Ordinal);
        Assert.Contains(slide.Runs, run => run.Kind == VersionRunKind.Removed && run.Text == "crescita");
        Assert.Contains(slide.Runs, run => run.Kind == VersionRunKind.Added && run.Text == "calo");

        await executor.ExecuteAsync(registry.Find(new CommandId("shell.file.new.documents"))!, workspace.Context);
        Dispatcher.UIThread.RunJobs();
        var textDocument = workspace.Active!;
        var folio = (PulseOffice.Documents.UI.FolioView)textDocument.View!;
        folio.Editor.Type("Primo paragrafo");
        var texts = operations.For(textDocument)!;
        var textVersion = Path.Combine(_folder, "v.odt");
        Assert.True(texts.WriteTo(textDocument, textVersion));
        folio.Editor.Type(" esteso");

        var paragraphs = Assert.IsType<VersionComparison>(texts.CompareVersion(textDocument, textVersion));
        var paragraph = Assert.Single(paragraphs.Changes);
        Assert.Equal(VersionChangeKind.Changed, paragraph.Kind);
        Assert.Contains(paragraph.Runs, run => run.Kind == VersionRunKind.Added && run.Text.Contains("esteso", StringComparison.Ordinal));

        var view = new VersionComparisonView(cells, provider.GetRequiredService<PulseOffice.Localization.ITextCatalog>());
        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(cells.Changes.Count, view.RowCount);
        object? answer = null;
        view.Completed += (_, value) => answer = value;
        window.Close();
        Assert.Null(answer);
    }, CancellationToken.None);

    private TextElement Box(string id, string text) => new()
    {
        Id = id,
        Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(10), Twip.FromCentimeters(3)),
        Text = new PulseOffice.Text.Rich.RichText(PulseOffice.Text.Rich.PlaceholderRole.Free, [new PulseOffice.Text.Rich.TextParagraph(text)]),
    };
}
