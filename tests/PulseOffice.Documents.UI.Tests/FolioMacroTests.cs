using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Scripting;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioMacroTests
{
    private const string Source = """
        doc.SetParagraph(1, "Rapporto");
        doc.SetStyle(1, "Heading 1");
        var n = doc.AppendParagraph("Il gatto e il cane");
        doc.Replace("gatto", "topo");
        doc.Bold(n, true);
        doc.SelectParagraph(n);
        doc.AddBookmark("fine");
        app.Log(doc.ParagraphCount + "|" + doc.Paragraph(2) + "|" + doc.Style(1) + "|" + doc.Bookmarks().length);
        """;

    [Fact]
    public Task UnaMacroModificaIlDocumentoEsiAnnullaInUnPasso() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var globals = scene.Services.GetRequiredService<FolioScriptGlobals>();
        var engine = scene.Services.GetServices<IScriptEngine>().Single();
        var lines = new List<string>();

        ScriptResult result;
        using (scene.Services.GetRequiredService<IScriptOutput>().Capture(lines.Add))
        using (scene.History.BeginTransaction("macro"))
        {
            var running = engine.ExecuteAsync(Source, new ScriptContext
            {
                Module = ModuleKind.Documents,
                Globals = globals.Globals,
                OnOutput = lines.Add,
            });
            while (!running.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            result = running.Result;
        }

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("2|Il topo e il cane|Heading 1|1", string.Join("", lines).Trim());
        Assert.Equal("Rapporto", scene.Paragraphs[0]);

        scene.History.Undo();

        Assert.Equal([string.Empty], scene.Paragraphs);
        Assert.Empty(scene.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks));
        return Task.CompletedTask;
    });

    [Fact]
    public Task LeTabelleSiScrivonoESiLeggonoPerCella() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var doc = scene.Services.GetRequiredService<FolioFacade>();

        Assert.True(doc.InsertTable(2, 3));
        Assert.True(doc.SetCell(1, 2, 3, "sei"));

        Assert.Equal(1, doc.TableCount);
        Assert.Equal("sei", doc.Cell(1, 2, 3));
        Assert.Equal(string.Empty, doc.Cell(1, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.SetCell(1, 3, 1, "fuori"));
        return Task.CompletedTask;
    });

    [Fact]
    public Task LeRevisioniSiAccendonoESiAccettano() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var doc = scene.Services.GetRequiredService<FolioFacade>();

        Assert.True(doc.SetTrackChanges(true));
        doc.InsertText("nuovo");

        Assert.True(doc.TrackChanges);
        Assert.True(doc.ChangeCount > 0);
        Assert.True(doc.AcceptAll() > 0);
        Assert.Equal(0, doc.ChangeCount);
        Assert.Equal("nuovo", doc.Text);
        return Task.CompletedTask;
    });

    [Fact]
    public Task AggiungereUnParagrafoAUnDocumentoNuovoNonLasciaUnParagrafoVuoto() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var doc = scene.Services.GetRequiredService<FolioFacade>();

        Assert.Equal(1, doc.AppendParagraph("primo"));
        Assert.Equal(2, doc.AppendParagraph("secondo"));
        Assert.Equal(["primo", "secondo"], scene.Paragraphs);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Leggere_un_paragrafo_o_una_cella_che_non_esistono_da_errore_e_non_testo_vuoto() => FolioScene.RunAsync(() =>
    {
        var scene = new FolioScene();
        var doc = scene.Services.GetRequiredService<FolioFacade>();
        doc.AppendParagraph("primo");

        Assert.Equal("primo", doc.Paragraph(1));
        Assert.Contains("1 paragraphs", Assert.Throws<ArgumentOutOfRangeException>(() => doc.Paragraph(0)).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Style(9));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.Cell(1, 1, 1));
        return Task.CompletedTask;
    });
}
