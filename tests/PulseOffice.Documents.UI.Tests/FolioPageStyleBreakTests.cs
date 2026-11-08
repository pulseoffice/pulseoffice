using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioPageStyleBreakTests
{
    private static readonly CommandContext Open = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    [Fact]
    public Task UnaInterruzioneConStileDiPaginaInUnaTabellaDiventaUnaInterruzioneSemplice() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Editor.Insert((documents, at) => documents.InsertTable(scene.Document, scene.History, at, 2, 2), "insert-table");
        var table = scene.Document.Body.Blocks.OfType<TableBlock>().Single();
        var cell = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        scene.Editor.PlaceCaret(new DocumentPosition(cell.Id, 0), false);
        var commands = new FolioBreakCommands(scene.Services.GetRequiredService<IActiveFolioContext>(),
            new AnsweringForms(new Dictionary<string, object> { ["kind"] = 2, ["style"] = 1 }), null!,
            scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(), null!).GetCommands().ToList();

        Assert.False(commands.Single(command => command.Id.Value == "folio.insert.landscapeBreak").EnabledWhen!(Open));
        var error = Record.Exception(() => commands.Single(command => command.Id.Value == "folio.insert.breakDialog").Execute(Open, CancellationToken.None).GetAwaiter().GetResult());

        Assert.Null(error);
        var paragraphs = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.True(paragraphs[1].Content.PageBreakBefore);
        Assert.Null(paragraphs[1].Content.PageStyleName);
    }, CancellationToken.None);
}
