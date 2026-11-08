using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFieldsDialogTests
{
    private static void Book(TextDocument document) =>
        document.Body.Replace(
        [
            new ParagraphBlock("h1", "Primo capitolo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("t1", "Testo del primo capitolo.", DefaultStyleNames.TextBody),
            new ParagraphBlock("h2", "Secondo capitolo", DefaultStyleNames.HeadingLevel(1)),
            new ParagraphBlock("t2", "Qui", DefaultStyleNames.TextBody),
        ]);

    private static (FolioScene Scene, FolioFieldsView View, ITextCatalog Texts) Open(FolioFieldsStart start)
    {
        var scene = new FolioScene(Book);
        scene.Editor.PlaceCaret(new DocumentPosition("t2", 3), false);
        var services = scene.Services.GetRequiredService<FolioFieldsServices>();
        var view = new FolioFieldsView(new FolioFieldsModel(scene.Editor, services, start), new PulseOffice.UI.Icons.IconCatalog());
        return (scene, view, services.Texts);
    }

    private static IReadOnlyList<TextRun> Fields(FolioScene scene, string paragraph) =>
        [.. scene.Document.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Id == paragraph).Content.Runs.Where(run => run.Field != TextFieldKind.None)];

    private static void Choose(FolioFieldsView view, string title)
    {
        for (var category = 0; category < view.Categories.ItemCount; category++)
        {
            view.Categories.SelectedIndex = category;
            for (var index = 0; index < view.FieldList.ItemCount; index++)
            {
                if (((ListBoxItem)view.FieldList.Items[index]!).GetLogicalDescendants().OfType<TextBlock>().First().Text != title) continue;
                view.FieldList.SelectedIndex = index;
                return;
            }
        }
        throw new InvalidOperationException(title);
    }

    [Fact]
    public Task TreColonneCategorieCampiOpzioniConAnteprimaEInserisciSenzaChiudere() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, view, texts) = Open(new FolioFieldsStart(FolioFieldsPage.Fields));
        object? closed = "open";
        view.Completed += (_, value) => closed = value;

        Assert.Equal(5, view.Categories.ItemCount);
        Choose(view, texts["folio.field.kind.date"]);
        var format = view.Options!.GetLogicalDescendants().OfType<ComboBox>().Single();
        Assert.True(format.ItemCount > 4);
        format.SelectedIndex = 6;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.CurrentCulture), view.FieldPreview);

        view.Apply();
        Assert.Equal("open", closed);
        Assert.Equal(texts.Format("folio.fields.inserted", texts["folio.field.kind.date"]), view.Status);
        var date = Assert.Single(Fields(scene, "t2"));
        Assert.Equal(TextFieldKind.Date, date.Field);
        Assert.Equal("yyyy-MM-dd", date.FieldDetail?.Format);

        view.FieldSearch.Text = texts["folio.field.kind.author"];
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, view.FieldList.ItemCount);
        view.Apply();
        Assert.Equal(2, Fields(scene, "t2").Count);
    }, CancellationToken.None);

    [Fact]
    public Task IlCampoUtenteMostraIlValoreScrittoESalvaLaVariabile() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, view, texts) = Open(new FolioFieldsStart(FolioFieldsPage.Fields));
        Choose(view, texts["folio.field.kind.userField"]);
        Assert.False(view.ApplyButton.IsEnabled);
        var boxes = view.Options!.GetLogicalDescendants().OfType<TextBox>().ToList();
        boxes[0].Text = "cliente";
        boxes[1].Text = "ACME";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ACME", view.FieldPreview);
        Assert.True(view.ApplyButton.IsEnabled);
        view.Apply();
        Assert.Equal("ACME", scene.Document.Variables["cliente"]);
        Assert.Equal("cliente", Assert.Single(Fields(scene, "t2")).FieldArgument);
    }, CancellationToken.None);

    [Fact]
    public Task IRimandiMostranoDestinazioniEFormatiConEsempioEPoiSiModificano() => FolioScene.Session.Dispatch(() =>
    {
        var (scene, view, texts) = Open(new FolioFieldsStart(FolioFieldsPage.References));
        Assert.Equal(FolioFieldsPage.References, view.Page);
        Assert.Equal(2, view.TargetList.ItemCount);
        view.TargetSearch.Text = "secondo";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, view.TargetList.ItemCount);
        Assert.Equal("Secondo capitolo", view.ReferencePreview);
        var direction = Enumerable.Range(0, view.FormatList.ItemCount)
            .First(index => ((ListBoxItem)view.FormatList.Items[index]!).GetLogicalDescendants().OfType<TextBlock>().First().Text == texts["folio.reference.format.direction"]);
        view.FormatList.SelectedIndex = direction;
        Assert.Equal(texts["folio.reference.above"], view.ReferencePreview);
        view.Apply();
        var reference = Assert.Single(Fields(scene, "t2"));
        Assert.Equal(TextFieldKind.Bookmark, reference.Field);
        Assert.Equal("__RefHeading__h2", reference.FieldArgument);
        Assert.Equal(ReferenceFormats.Direction, reference.FieldDetail?.Format);

        var hit = scene.Services.GetRequiredService<IFieldEditing>().At(scene.Document, new DocumentPosition("t2", 3))!;
        var services = scene.Services.GetRequiredService<FolioFieldsServices>();
        var editing = new FolioFieldsView(new FolioFieldsModel(scene.Editor, services, new FolioFieldsStart(FolioFieldsPage.References)
        {
            Editing = hit,
            ReferenceKind = new FolioReferenceKind(ReferenceTargetKind.Headings, null, string.Empty, string.Empty),
            Target = new ReferenceTarget(ReferenceTargetKind.Headings, "h2", string.Empty),
            Format = ReferenceFormats.Direction,
        }), null);
        object? result = null;
        editing.Completed += (_, value) => result = value;
        Assert.Equal("Secondo capitolo", ((ListBoxItem)editing.TargetList.SelectedItem!).GetLogicalDescendants().OfType<TextBlock>().First().Text);
        editing.FormatList.SelectedIndex = 0;
        editing.Apply();
        Assert.Equal(true, result);
        Assert.Null(Assert.Single(Fields(scene, "t2")).FieldDetail?.Format);
    }, CancellationToken.None);
}
