using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Shell;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioMergeWizardTests
{
    private static readonly MergeData People = new(["Nome", "Città"], [["Anna", "Roma"], ["Bruno", "Milano"], ["Carla", "Torino"]]);

    private static void Letter(TextDocument document)
    {
        var greeting = new ParagraphBlock("p1", string.Empty);
        greeting.State = greeting.State with
        {
            Content = greeting.Content with
            {
                Runs =
                [
                    new TextRun("Cara "),
                    new TextRun("«Nome»") { Field = TextFieldKind.Database, FieldArgument = "Nome" },
                    new TextRun(", da "),
                    new TextRun("«Città»") { Field = TextFieldKind.Database, FieldArgument = "Città" },
                ],
            },
        };
        document.Body.Replace([greeting, new ParagraphBlock("p2", "Saluti")]);
    }

    [Fact]
    public Task SiScorronoIRecordConLaLetteraUnitaESiSceglieIlRisultato() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Letter);
        var view = new FolioMergeWizardView(scene.Document, People, new FolioMergeLetters(scene.Services.GetRequiredService<IBlockTree>()), new TextCatalog(UiLanguage.Italian), new PulseOffice.UI.Icons.IconCatalog());
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(["Cara Anna, da Roma", "Saluti"], view.Letter);
        view.ShowRecord(int.MaxValue);
        Assert.Equal(2, view.Record);
        Assert.Equal("Cara Carla, da Torino", view.Letter[0]);

        Assert.Equal(0, view.Step);
        view.Go(1);
        Assert.Equal(5, view.TargetTiles.Count);
        view.ChooseTarget(MergeTarget.PdfFiles);
        Assert.Equal(new FolioMergePlan(MergeTarget.PdfFiles, 0, 2, null), view.Plan());
        Assert.Null(result);
    }, CancellationToken.None);
}
