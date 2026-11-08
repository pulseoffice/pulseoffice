using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioBibliographyUndoTests
{
    private sealed class FixedTabs(IReadOnlyDictionary<string, object> answer) : ITabbedForms
    {
        public Task<IReadOnlyDictionary<string, object>?> AskAsync(string titleKey, IReadOnlyList<FormTab> tabs, CancellationToken ct, int initialTab = 0, double width = 620) =>
            Task.FromResult<IReadOnlyDictionary<string, object>?>(answer);
    }

    private static CommandDefinition Command(FolioScene scene, string id, IReadOnlyDictionary<string, object> form, IReadOnlyDictionary<string, object> tabs) =>
        new FolioIndexCommands(
                scene.Services.GetRequiredService<IActiveFolioContext>(), new AnsweringForms(form), new FixedTabs(tabs),
                scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(), null!,
                scene.Services.GetRequiredService<PulseOffice.Documents.UI.Editing.IFolioFieldRefresh>(), null!,
                scene.Services.GetRequiredService<BibliographyFieldNames>(), null!, null!, null!, null!,
                scene.Services.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>())
            .GetCommands().Single(command => command.Id.Value == id);

    private static readonly CommandContext Open = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private static IReadOnlyList<TextRun> Citations(FolioScene scene) =>
        [.. scene.Document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Where(run => run.Field == TextFieldKind.Citation)];

    [Fact]
    public Task RinominareLaFonteSpostaLeCitazioniESiAnnullaInUnPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("p1", "Vedi ")]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 5), false);
        Command(scene, "folio.insert.bibliographyEntry", new Dictionary<string, object> { ["source"] = 0 },
            new Dictionary<string, object> { ["identifier"] = "Eco1980", ["type"] = 0 }).Execute(Open, CancellationToken.None).GetAwaiter().GetResult();
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 6), false);

        Command(scene, "folio.edit.citation", new Dictionary<string, object>(),
            new Dictionary<string, object> { ["identifier"] = "Eco1983", ["type"] = 0 }).Execute(Open, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal("Eco1983", Assert.Single(scene.Document.Bibliography.All).Identifier);
        Assert.Equal(("Eco1983", "[Eco1983]"), (Assert.Single(Citations(scene)).FieldArgument, Citations(scene)[0].Text));

        scene.History.Undo();

        Assert.Equal("Eco1980", Assert.Single(scene.Document.Bibliography.All).Identifier);
        Assert.Equal(("Eco1980", "[Eco1980]"), (Assert.Single(Citations(scene)).FieldArgument, Citations(scene)[0].Text));
    }, CancellationToken.None);

    [Fact]
    public Task UnaFonteNuovaNonSovrascriveQuellaConLoStessoIdentificatore() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("p1", "Vedi ")]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 5), false);
        Command(scene, "folio.insert.bibliographyEntry", new Dictionary<string, object> { ["source"] = 0 },
            new Dictionary<string, object> { ["identifier"] = "Eco1980", ["type"] = 0, ["title"] = "Il nome della rosa" }).Execute(Open, CancellationToken.None).GetAwaiter().GetResult();

        Command(scene, "folio.insert.bibliographyEntry", new Dictionary<string, object> { ["source"] = 1 },
            new Dictionary<string, object> { ["identifier"] = "Eco1980", ["type"] = 0, ["title"] = "Altro" }).Execute(Open, CancellationToken.None).GetAwaiter().GetResult();

        Assert.Equal("Il nome della rosa", Assert.Single(scene.Document.Bibliography.All).Value("title"));
        Assert.Single(Citations(scene));
    }, CancellationToken.None);

    [Fact]
    public Task CitareUnaFonteNuovaSiAnnullaInUnSoloPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("p1", "Vedi ")]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 5), false);
        var command = Command(scene, "folio.insert.bibliographyEntry",
            new Dictionary<string, object> { ["source"] = 0 },
            new Dictionary<string, object> { ["identifier"] = "Eco1980", ["type"] = 0, ["title"] = "Il nome della rosa" });

        command.Execute(new CommandContext(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen), CancellationToken.None).GetAwaiter().GetResult();
        Assert.Single(scene.Document.Bibliography.All);
        Assert.Contains(scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field == TextFieldKind.Citation);

        scene.History.Undo();

        Assert.Empty(scene.Document.Bibliography.All);
        Assert.Equal("Vedi ", scene.Paragraphs[0]);
    }, CancellationToken.None);
}
