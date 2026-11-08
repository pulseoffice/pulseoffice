using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioMailMergePreviewTests
{
    private static readonly CommandContext Open = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    [Fact]
    public Task ScorrereIRecordNonRiempieLaCronologia() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
            [new ParagraphBlock("p1", new TextParagraph("«nome»") { Runs = [new TextRun("«nome»") { Field = TextFieldKind.Database, FieldArgument = "nome" }] })]));
        var records = scene.Services.GetRequiredService<IMergeRecords>();
        records.Load(scene.Document, new MergeData(["nome"], [["Ada"], ["Grace"]]));
        var commands = new FolioMailMergeCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), null!, records, null!, null!, null!,
                scene.Services.GetRequiredService<IFolioFieldRefresh>(), null!, null!, null!, null!, null!,
                scene.Services.GetRequiredService<PulseOffice.UI.Controls.Services.IStatusBarService>(),
                scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(), null!, null!, new ConsentingQueryGate())
            .GetCommands().ToList();
        var before = scene.History.UndoDescription;

        commands.Single(command => command.Id.Value == "folio.mailMerge.first").Execute(Open, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("Ada", scene.Paragraphs[0]);
        commands.Single(command => command.Id.Value == "folio.mailMerge.next").Execute(Open, CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal("Grace", scene.Paragraphs[0]);

        Assert.Equal(before, scene.History.UndoDescription);
    }, CancellationToken.None);
}
