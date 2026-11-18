using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round210DuplicateTests
{
    private static async Task<Round206Scene> Remove(bool caseSensitive)
    {
        var scene = new Round206Scene();
        string[] names = ["Ann", "ann", "Ann"];
        for (var row = 0; row < names.Length; row++)
            scene.Sheet.SetValue(new CellAddress(row, 0), CellValue.Text(names[row]));
        scene.Selection = new CellRange(Round206Scene.A("A1"), Round206Scene.A("A3"));
        var commands = new LedgerDataCommands(scene.Context, Round206Scene.Fake<Engine.Operations.ISheetOutline>(),
            new Engine.Operations.DuplicateRemover(new SelectionWorkArea()),
            new Engine.Operations.SheetCellShift(new Engine.Operations.SheetStructure()), new Round206Status(),
            new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()), Round206Scene.Texts,
            new Round206FixedDuplicatePrompt(new DataDialogs.DuplicateRemovalChoice(false, [0], caseSensitive)));
        await commands.GetCommands().Single(command => command.Id.Value == "ledger.data.removeDuplicates")
            .Execute(null!, CancellationToken.None);
        return scene;
    }

    [Fact]
    public async Task Match_case_keeps_rows_that_differ_only_in_case()
    {
        var scene = await Remove(caseSensitive: true);

        Assert.Equal(["Ann", "ann", ""], new[] { scene.Text("A1"), scene.Text("A2"), scene.Text("A3") });
    }

    [Fact]
    public async Task Without_match_case_rows_differing_only_in_case_are_duplicates()
    {
        var scene = await Remove(caseSensitive: false);

        Assert.Equal(["Ann", "", ""], new[] { scene.Text("A1"), scene.Text("A2"), scene.Text("A3") });
    }
}
