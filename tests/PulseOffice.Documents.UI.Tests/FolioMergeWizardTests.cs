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
        var view = new FolioMergeWizardView(scene.Document, People, new FolioMergeLetters(scene.Services.GetRequiredService<IBlockTree>(), new MergeValueFormat(), new PulseOffice.Text.Rich.TextCultures()), new MergeRecipients(), new TextCatalog(UiLanguage.Italian), new PulseOffice.UI.Icons.IconCatalog());
        object? result = null;
        view.Completed += (_, value) => result = value;

        Assert.Equal(["Cara Anna, da Roma", "Saluti"], view.Letter);
        view.ShowRecord(int.MaxValue);
        Assert.Equal(2, view.Record);
        Assert.Equal("Cara Carla, da Torino", view.Letter[0]);

        Assert.Equal(0, view.Step);
        view.Go(2);
        Assert.Equal(5, view.TargetTiles.Count);
        view.ChooseTarget(MergeTarget.PdfFiles);
        var plan = view.Plan();
        Assert.Equal((MergeTarget.PdfFiles, null), (plan.Target, plan.NameColumn));
        Assert.Equal([0, 1, 2], plan.Rows);
        Assert.Null(result);
    }, CancellationToken.None);

    [Fact]
    public Task Recipients_can_be_excluded_and_filtered() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Letter);
        var view = new FolioMergeWizardView(scene.Document, People, new FolioMergeLetters(scene.Services.GetRequiredService<IBlockTree>(), new MergeValueFormat(), new PulseOffice.Text.Rich.TextCultures()), new MergeRecipients(), new TextCatalog(UiLanguage.Italian), null);

        view.Exclude(1, true);
        Assert.Equal([0, 2], view.Plan().Rows);

        view.ApplyFilter(new MergeFilter("Città", MergeFilterOperator.Contains, "o"));
        Assert.Equal([0, 1, 2], view.Listed);
        view.ApplyFilter(new MergeFilter("Città", MergeFilterOperator.Equals, "torino"));
        Assert.Equal([2], view.Listed);
        Assert.Equal([2], view.Plan().Rows);
    }, CancellationToken.None);

    [Fact]
    public Task The_wizard_starts_from_the_saved_selection_and_returns_it() => FolioScene.Session.Dispatch(() =>
    {
        var scene = new FolioScene(Letter);
        scene.Document.MergeSelection = new MergeSelection(
            new MergeFilter([new MergeCondition("Città", MergeFilterOperator.Equals, "Roma"), new MergeCondition("Città", MergeFilterOperator.Equals, "Torino") { Join = MergeJoin.Or }]),
            [new MergeSort("Nome", true)],
            new HashSet<int> { 0 });
        var view = new FolioMergeWizardView(scene.Document, People, new FolioMergeLetters(scene.Services.GetRequiredService<IBlockTree>(), new MergeValueFormat(), new PulseOffice.Text.Rich.TextCultures()), new MergeRecipients(), new TextCatalog(UiLanguage.Italian), null);

        Assert.Equal(2, view.Recipients.ConditionCount);
        Assert.Equal([2, 0], view.Listed);
        var plan = view.Plan();
        Assert.Equal([2], plan.Rows);
        Assert.Equal(scene.Document.MergeSelection, plan.Selection);
    }, CancellationToken.None);
}
