using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFieldUndoTests
{
    [Fact]
    public Task UnRiferimentoAUnTitoloNellaSezioneProtettaNonLoModifica() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace(
        [
            new SectionBlock("s1", "Chiusa", new BlockContainer([new ParagraphBlock("h1", "Titolo", PulseOffice.Documents.Styles.DefaultStyleNames.HeadingLevel(1))])) { Protected = true },
            new ParagraphBlock("p1", "Vedi "),
        ]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 5), false);
        var services = scene.Services.GetRequiredService<FolioFieldsServices>();
        var model = new FolioFieldsModel(scene.Editor, services, new FolioFieldsStart(FolioFieldsPage.References));
        var heading = (SectionBlock)scene.Document.Body.Blocks[0];
        var target = model.Targets(model.ReferenceKinds()[0], string.Empty).Single();

        Assert.False(model.ApplyReference(target, PulseOffice.Documents.Fields.ReferenceFormats.Text));

        Assert.Empty(heading.Content.Blocks.OfType<ParagraphBlock>().Single().Marks);
    }, CancellationToken.None);

    [Fact]
    public Task UnCampoUtenteSiInserisceESiAnnullaInUnSoloPasso() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("p1", "Ciao ")]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 5), false);
        var services = scene.Services.GetRequiredService<FolioFieldsServices>();
        var model = new FolioFieldsModel(scene.Editor, services, new FolioFieldsStart(FolioFieldsPage.Fields));
        var choice = services.Catalog.Choices.Single(candidate => candidate.Key == "userField");

        Assert.True(model.Apply(choice, new Dictionary<string, object> { ["name"] = "cliente", ["value"] = "Ada" }));
        Assert.Equal("Ada", scene.Document.Variables["cliente"]);
        Assert.Contains(scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field == TextFieldKind.Variable);

        scene.History.Undo();

        Assert.False(scene.Document.Variables.ContainsKey("cliente"));
        Assert.DoesNotContain(scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field == TextFieldKind.Variable);
    }, CancellationToken.None);

    [Fact]
    public Task Changing_the_format_of_a_fixed_date_reformats_its_stored_date() => FolioScene.Session.Dispatch(() =>
    {
        var field = new TextRun("2020-03-01") { Field = TextFieldKind.Date, FieldDetail = new FieldDetail { Fixed = true, Value = "2020-03-01", Format = "yyyy-MM-dd" } };
        var scene = new FolioScene(document => document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("2020-03-01") { Runs = [field] })]));
        scene.Editor.PlaceCaret(new DocumentPosition("p1", 1), false);
        var services = scene.Services.GetRequiredService<FolioFieldsServices>();
        var hit = scene.Services.GetRequiredService<PulseOffice.Documents.Fields.IFieldEditing>().At(scene.Document, new DocumentPosition("p1", 1))!;
        var model = new FolioFieldsModel(scene.Editor, services, new FolioFieldsStart(FolioFieldsPage.Fields) { Editing = hit, Choice = services.Catalog.Of(hit.Run) });
        var choice = services.Catalog.Choices.Single(candidate => candidate.Key == "date");

        Assert.True(model.Apply(choice, new Dictionary<string, object> { ["format"] = 7, ["custom"] = "yyyy/MM/dd", ["fixed"] = true }));

        var run = scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single(candidate => candidate.Field == TextFieldKind.Date);
        Assert.Equal("2020/03/01", run.Text);
        Assert.Equal("2020-03-01", run.FieldDetail!.Value);
    }, CancellationToken.None);
}
