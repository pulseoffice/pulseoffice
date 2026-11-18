using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round206DuplicateTests
{
    private static LedgerDataCommands Commands(Round206Scene scene, Round206Status status, Round206FixedDuplicatePrompt prompt) =>
        new(scene.Context, Round206Scene.Fake<Engine.Operations.ISheetOutline>(),
            new Engine.Operations.DuplicateRemover(new SelectionWorkArea()),
            new Engine.Operations.SheetCellShift(new Engine.Operations.SheetStructure()), status,
            new Engine.Operations.WorkbookCheckpoints(new Engine.Operations.SheetSnapshots()), Round206Scene.Texts, prompt);

    private static Task Run(LedgerDataCommands commands) =>
        commands.GetCommands().Single(command => command.Id.Value == "ledger.data.removeDuplicates").Execute(null!, CancellationToken.None);

    private static Round206Scene Table()
    {
        var scene = new Round206Scene();
        string[][] rows = [["Name", "City"], ["Ann", "Rome"], ["Ann", "Milan"], ["Bob", "Rome"], ["Ann", "Rome"]];
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < 2; column++)
                scene.Sheet.SetValue(new CellAddress(row, column), CellValue.Text(rows[row][column]));
        scene.Selection = new CellRange(Round206Scene.A("A1"), Round206Scene.A("B5"));
        return scene;
    }

    [Fact]
    public async Task The_chosen_columns_and_header_row_decide_which_rows_are_duplicates()
    {
        var scene = Table();
        var prompt = new Round206FixedDuplicatePrompt(new DataDialogs.DuplicateRemovalChoice(true, [0]));

        await Run(Commands(scene, new Round206Status(), prompt));

        Assert.Equal(1, prompt.Calls);
        Assert.Equal(["Name", "Ann", "Bob", ""], new[] { scene.Text("A1"), scene.Text("A2"), scene.Text("A3"), scene.Text("A4") });
        Assert.Equal("Rome", scene.Text("B3"));
        Assert.True(scene.History.CanUndo);
        scene.History.Undo();
        Assert.False(scene.History.CanUndo);
        Assert.Equal("Ann", scene.Text("A5"));
    }

    [Fact]
    public async Task Without_a_header_the_first_row_is_compared_too()
    {
        var scene = Table();
        scene.Sheet.SetValue(Round206Scene.A("A2"), CellValue.Text("Name"));
        scene.Sheet.SetValue(Round206Scene.A("B2"), CellValue.Text("City"));

        await Run(Commands(scene, new Round206Status(), new Round206FixedDuplicatePrompt(new DataDialogs.DuplicateRemovalChoice(false, [0, 1]))));

        Assert.Equal(["Name", "Ann", "Bob", "Ann", ""], new[] { scene.Text("A1"), scene.Text("A2"), scene.Text("A3"), scene.Text("A4"), scene.Text("A5") });
        Assert.Equal("Milan", scene.Text("B2"));
    }

    [Fact]
    public async Task Cancelling_or_choosing_no_column_changes_nothing()
    {
        var scene = Table();
        var status = new Round206Status();

        await Run(Commands(scene, status, new Round206FixedDuplicatePrompt(new DataDialogs.DuplicateRemovalChoice(true, []))));

        Assert.Equal([Round206Scene.Texts["status.duplicates.noColumns"]], status.Messages);
        Assert.Equal("Ann", scene.Text("A5"));
        Assert.False(scene.History.CanUndo);
    }

    [Fact]
    public async Task A_protected_sheet_that_forbids_deleting_rows_says_so()
    {
        var scene = Table();
        scene.Sheet.Protection = new SheetProtection { IsEnabled = true };
        var status = new Round206Status();
        var prompt = new Round206FixedDuplicatePrompt();

        await Run(Commands(scene, status, prompt));

        Assert.Equal([Round206Scene.Texts["status.protection.deleteRows"]], status.Messages);
        Assert.Equal(0, prompt.Calls);
    }
}
