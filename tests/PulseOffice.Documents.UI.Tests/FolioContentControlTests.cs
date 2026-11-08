using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioContentControlTests
{
    private static readonly CommandContext Context = new(ModuleKind.Documents, SelectionKind.None, DocumentState.DocumentOpen);

    private FolioContentControls Controls(FolioScene scene, Dictionary<string, object> answer) => new(
        new AnsweringForms(answer),
        scene.Services.GetRequiredService<PulseOffice.Localization.ITextCatalog>(),
        null!,
        scene.Services.GetRequiredService<IFolioImages>(),
        scene.Services.GetRequiredService<FolioControlServices>(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<FolioContentControls>.Instance);

    private ContentControlSpan Only(FolioScene scene) => Assert.Single(scene.Services.GetRequiredService<IContentControlCatalog>().All(scene.Document));

    private string Inside(FolioScene scene, ContentControlSpan span) => scene.Paragraphs[0][span.Start.Offset..span.End.Offset];

    [Fact]
    public Task IlSegnapostoSiSostituisceScrivendoESiAnnullaInUnPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("Nome: ");
        var controls = Controls(scene, new() { ["title"] = "Nome", ["tag"] = "nome", ["placeholder"] = "Scrivi qui" });
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.PlainText, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("Nome: Scrivi qui", scene.Paragraphs[0]);
        Assert.Equal(("Nome", "nome", true), (Only(scene).Control.Title, Only(scene).Control.Tag, Only(scene).Control.ShowingPlaceholder));

        scene.Editor.PlaceCaret(new DocumentPosition(scene.Editor.Caret.ParagraphId, 8), false);
        scene.Type("Ada");
        Assert.Equal("Nome: Ada", scene.Paragraphs[0]);
        Assert.Equal("Ada", Inside(scene, Only(scene)));
        Assert.False(Only(scene).Control.ShowingPlaceholder);

        scene.History.Undo();
        Assert.Equal("Nome: Scrivi qui", scene.Paragraphs[0]);
        Assert.True(Only(scene).Control.ShowingPlaceholder);
    }, CancellationToken.None);

    [Fact]
    public Task IlClicCommutaLaCasellaMaNonInSolaLettura() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var controls = Controls(scene, new() { ["title"] = "Accetto" });
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.CheckBox, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("☐", scene.Paragraphs[0]);
        var inside = new DocumentPosition(scene.Editor.Caret.ParagraphId, 0);
        Assert.True(controls.Activate(scene.Editor, inside));
        Assert.Equal("☒", scene.Paragraphs[0]);
        Assert.True(Only(scene).Control.Checked);

        scene.ReadOnly = true;
        Assert.False(controls.Activate(scene.Editor, inside));
        Assert.Equal("☒", scene.Paragraphs[0]);
        var commands = new FolioContentControlCommands(scene.Services.GetRequiredService<IActiveFolioContext>(), controls).GetCommands().ToList();
        Assert.All(commands, command => Assert.False(command.EnabledWhen?.Invoke(Context) ?? true));
    }, CancellationToken.None);

    [Fact]
    public Task IlClicNonCommutaLaCasellaDiUnaSezioneProtettaConIlCursoreAltrove() => FolioScene.Session.Dispatch(() =>
    {
        var section = new PulseOffice.Documents.Blocks.SectionBlock("s1", "Chiusa", new PulseOffice.Documents.Blocks.BlockContainer([new PulseOffice.Documents.Blocks.ParagraphBlock("chiuso", string.Empty)]));
        var scene = new FolioScene(document => document.Body.Replace([new PulseOffice.Documents.Blocks.ParagraphBlock("libero", "libero"), section]));
        scene.Editor.PlaceCaret(new DocumentPosition("chiuso", 0), false);
        var controls = Controls(scene, new() { ["title"] = "Accetto" });
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.CheckBox, CancellationToken.None).GetAwaiter().GetResult());
        section.Protected = true;
        scene.Editor.PlaceCaret(new DocumentPosition("libero", 2), false);

        Assert.False(controls.Activate(scene.Editor, new DocumentPosition("chiuso", 0)));
        Assert.False(Only(scene).Control.Checked);
    }, CancellationToken.None);

    [Fact]
    public Task IlContenutoBloccatoNonSiScriveNeSiCancella() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        scene.Type("fisso");
        scene.Editor.Select(new DocumentPosition(scene.Editor.Caret.ParagraphId, 0), new DocumentPosition(scene.Editor.Caret.ParagraphId, 5));
        var controls = Controls(scene, new() { ["contentLocked"] = true, ["controlLocked"] = true });
        Assert.True(controls.InsertAsync(scene.Editor, ContentControlKind.RichText, CancellationToken.None).GetAwaiter().GetResult());
        Assert.True(Only(scene).Control is { ContentLocked: true, ControlLocked: true });

        scene.Editor.PlaceCaret(new DocumentPosition(scene.Editor.Caret.ParagraphId, 2), false);
        scene.Type("X");
        scene.Press(Avalonia.Input.Key.Back);
        Assert.Equal("fisso", scene.Paragraphs[0]);
        Assert.False(controls.Remove(scene.Editor));
        Assert.Single(scene.Services.GetRequiredService<IContentControlCatalog>().All(scene.Document));
    }, CancellationToken.None);

    [Fact]
    public Task LElencoSiSceglieDalSelettoreELaCancellazioneRiportaIlSegnaposto() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene();
        var items = new List<PulseOffice.UI.Controls.Forms.ChoiceItem> { new("Uno", "1"), new("Due", "2") };
        var insert = Controls(scene, new() { ["items"] = items, ["placeholder"] = "Scegli" });
        Assert.True(insert.InsertAsync(scene.Editor, ContentControlKind.DropDownList, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal([new ContentControlItem("Uno", "1"), new ContentControlItem("Due", "2")], Only(scene).Control.Items);

        scene.Editor.PlaceCaret(new DocumentPosition(scene.Editor.Caret.ParagraphId, 1), false);
        Assert.True(Controls(scene, new() { ["value"] = 1 }).ChooseAsync(scene.Editor, CancellationToken.None).GetAwaiter().GetResult());
        Assert.Equal("Due", scene.Paragraphs[0]);

        var plain = Controls(scene, new() { ["placeholder"] = "Vuoto" });
        scene.Editor.PlaceCaret(new DocumentPosition(scene.Editor.Caret.ParagraphId, 3), false);
        scene.Type(" ");
        Assert.True(plain.InsertAsync(scene.Editor, ContentControlKind.RichText, CancellationToken.None).GetAwaiter().GetResult());
        scene.Editor.PlaceCaret(new DocumentPosition(scene.Editor.Caret.ParagraphId, 5), false);
        scene.Type("ok");
        Assert.Equal("Due ok", scene.Paragraphs[0]);
        scene.Press(Avalonia.Input.Key.Back);
        scene.Press(Avalonia.Input.Key.Back);
        Assert.Equal("Due Vuoto", scene.Paragraphs[0]);
    }, CancellationToken.None);
}
