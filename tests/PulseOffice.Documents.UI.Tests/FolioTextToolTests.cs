using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Redaction;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Prompts;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class AnsweringForms(IReadOnlyDictionary<string, object> answer) : IFolioForms
{
    public Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, string explanationKey, IReadOnlyList<FormField> fields, CancellationToken ct, double width = 460) =>
        Task.FromResult<IReadOnlyDictionary<string, object>?>(answer);
}

public sealed class AgreeingRedaction : IRedactionPrompt
{
    public Task<bool> ConfirmAsync(string question, string titleKey, CancellationToken ct) => Task.FromResult(true);

    public Task<IReadOnlyList<RedactionPattern>?> ChoosePatternsAsync(string titleKey, string explanationKey, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RedactionPattern>?>(new TextRedactor().Patterns);
}

public sealed class SyllableHyphenation : IHyphenationProvider, IHyphenator
{
    public LanguageTag Language => LanguageTag.Parse("it");

    public IHyphenator? For(LanguageTag language) => this;

    public IReadOnlyList<int> Positions(string word, int minimumEdge = 2) => word.Length >= 8 ? [4] : [];
}

public sealed class FolioTextToolTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static void Run(FolioScene scene, string id, IReadOnlyDictionary<string, object>? answer = null)
    {
        var services = scene.Services;
        var commands = new FolioTextToolCommands(services.GetRequiredService<IActiveFolioContext>(), null!, new AgreeingRedaction(), new TextRedactor(),
            new SyllableHyphenation(), services.GetRequiredService<IFolioLanguages>(), new AnsweringForms(answer ?? new Dictionary<string, object>()),
            services.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>(), services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            services.GetRequiredService<IMetadataRedaction>(), services.GetRequiredService<IDirectDocumentEditor>());
        commands.GetCommands().Single(command => command.Id.Value == id).Execute(Context, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public Task OscurareLaSelezioneSostituisceIlTestoESiAnnulla() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome segreto qui");
        var id = scene.Editor.Caret.ParagraphId;
        scene.Editor.Select(new DocumentPosition(id, 5), new DocumentPosition(id, 12));

        Run(scene, "folio.tools.redact");

        Assert.Equal("Nome ■■■■■■■ qui", scene.Paragraphs[0]);
        scene.History.Undo();
        Assert.Equal("Nome segreto qui", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OscurareConLeRevisioniAttiveNonLasciaIlTestoInUnaRevisione() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome segreto qui");
        var id = scene.Editor.Caret.ParagraphId;
        scene.Document.Changes.IsRecording = true;
        scene.Editor.Select(new DocumentPosition(id, 5), new DocumentPosition(id, 12));

        Run(scene, "folio.tools.redact");

        Assert.Equal("Nome ■■■■■■■ qui", scene.Paragraphs[0]);
        Assert.Empty(scene.Document.Changes.All);
        scene.History.Undo();
        Assert.Equal("Nome segreto qui", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task OscurareNonSiPuoQuandoLeRevisioniSonoImposteDallaProtezione() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome segreto qui");
        var id = scene.Editor.Caret.ParagraphId;
        scene.Document.Protection = new PulseOffice.Documents.Protection.DocumentProtection(PulseOffice.Documents.Protection.DocumentRestriction.TrackedChanges, null);
        scene.Document.Changes.IsRecording = true;
        scene.Editor.Select(new DocumentPosition(id, 5), new DocumentPosition(id, 12));
        var services = scene.Services;
        var commands = new FolioTextToolCommands(services.GetRequiredService<IActiveFolioContext>(), null!, new AgreeingRedaction(), new TextRedactor(),
            new SyllableHyphenation(), services.GetRequiredService<IFolioLanguages>(), new AnsweringForms(new Dictionary<string, object>()),
            services.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>(), services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
            services.GetRequiredService<IMetadataRedaction>(), services.GetRequiredService<IDirectDocumentEditor>()).GetCommands().ToList();

        Assert.False(commands.Single(command => command.Id.Value == "folio.tools.redact").EnabledWhen!(Context));
        Assert.False(commands.Single(command => command.Id.Value == "folio.tools.autoRedact").EnabledWhen!(Context));
        return Task.CompletedTask;
    });

    [Fact]
    public Task LOscuramentoAutomaticoConLeRevisioniAttiveNonLasciaRevisioni() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Scrivi a mario@example.com domani");
        scene.Document.Changes.IsRecording = true;

        Run(scene, "folio.tools.autoRedact");

        Assert.DoesNotContain("mario@example.com", scene.Paragraphs[0]);
        Assert.Empty(scene.Document.Changes.All);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LOscuramentoAutomaticoTrovaIDatiRiconoscibili() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Scrivi a mario@example.com domani");

        Run(scene, "folio.tools.autoRedact");

        Assert.DoesNotContain("mario@example.com", scene.Paragraphs[0]);
        Assert.StartsWith("Scrivi a ", scene.Paragraphs[0]);
        Assert.EndsWith(" domani", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });

    [Fact]
    public Task LOscuramentoAutomaticoArrivaAIntestazioniENote() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("Corpo");
        scene.Editor.Insert((documents, at) => documents.InsertNote(scene.Document, scene.History, at, PulseOffice.Documents.Annotations.NoteKind.Footnote, "nota mario@example.com"), "insert-note");
        var page = scene.Document.Styles.Page.All[0];
        page.Header.Enabled = true;
        page.Header.Default.Add(new PulseOffice.Documents.Blocks.ParagraphBlock(scene.Document.NextId("p"), "capo mario@example.com"));

        Run(scene, "folio.tools.autoRedact");

        var texts = scene.Services.GetRequiredService<IStoryCatalog>().Paragraphs(scene.Document).Select(paragraph => paragraph.Text).ToList();
        Assert.DoesNotContain(texts, text => text.Contains("mario@example.com", StringComparison.Ordinal));
        return Task.CompletedTask;
    });

    [Fact]
    public Task LaSillabazioneInserisceTrattiniFacoltativiNelleParoleLunghe() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        scene.Type("un risultato straordinario");
        scene.Editor.Select(scene.Editor.Caret, scene.Editor.Caret);

        Run(scene, "folio.tools.hyphenate", new Dictionary<string, object> { ["length"] = 8.0 });

        Assert.Equal("un risu­ltato stra­ordinario", scene.Paragraphs[0]);
        return Task.CompletedTask;
    });
}
