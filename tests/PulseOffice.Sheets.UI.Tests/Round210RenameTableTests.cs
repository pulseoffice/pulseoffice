using Avalonia.Controls;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round210RenameTableTests
{
    private sealed class AnsweringDialogs(object answer) : IDialogService
    {
        public bool IsDialogOpen => false;
        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) => Task.FromResult((TResult?)answer);
        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;
        public void Close(object? result) { }
    }

    private static async Task<(Round206Scene Scene, Round206Status Status)> Rename(string newName)
    {
        var scene = new Round206Scene();
        scene.Engine.SetValue(new SheetCell(0, Round206Scene.A("A1")), CellValue.Text("Amount"));
        scene.Engine.SetValue(new SheetCell(0, Round206Scene.A("A2")), CellValue.Number(5));
        scene.Workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(Round206Scene.A("A1"), Round206Scene.A("A2")) });
        scene.Workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Other", SheetIndex = 0, Range = new CellRange(Round206Scene.A("D1"), Round206Scene.A("D2")) });
        scene.Engine.SetFormula(new SheetCell(0, Round206Scene.A("C1")), "=SUM(Sales[Amount])");
        var status = new Round206Status();
        var reader = new Dialogs.SheetReferenceReader(new Engine.Operations.SheetRangeReferences());
        var commands = new LedgerDatabaseRangeCommands(scene.Context, Round206Scene.Fake<Engine.Operations.ISheetRegions>(), Round206Scene.Fake<IQueryRefreshRunner>(),
            new AnsweringDialogs(new Dictionary<string, object> { ["area"] = 0L, ["name"] = newName }), status,
            new Dialogs.SheetReferenceSources(scene.Context, reader, new Engine.Operations.SheetRangeReferences(), Round206Scene.Fake<PulseOffice.UI.Controls.References.IReferencePickers>()),
            reader, Round206Scene.Texts, new Engine.Operations.DefinedNameRules(), new Engine.Operations.TableRenaming());
        await commands.GetCommands().Single(command => command.Id.Value == "ledger.database.rename").Execute(null!, CancellationToken.None);
        return (scene, status);
    }

    [Fact]
    public async Task Renaming_a_table_rewrites_its_formulas_in_one_undo_step()
    {
        var (scene, _) = await Rename("Orders");

        Assert.Equal("=SUM(Orders[Amount])", scene.Sheet[Round206Scene.A("C1")].Formula);
        Assert.NotNull(scene.Workbook.DatabaseRanges.Find("Orders"));
        scene.History.Undo();
        Assert.False(scene.History.CanUndo);
        Assert.Equal("=SUM(Sales[Amount])", scene.Sheet[Round206Scene.A("C1")].Formula);
        Assert.NotNull(scene.Workbook.DatabaseRanges.Find("Sales"));
    }

    [Theory]
    [InlineData("B2")]
    [InlineData("Other")]
    public async Task An_invalid_or_taken_name_is_refused(string newName)
    {
        var (scene, status) = await Rename(newName);

        Assert.NotNull(scene.Workbook.DatabaseRanges.Find("Sales"));
        Assert.False(scene.History.CanUndo);
        Assert.Single(status.Messages);
    }
}
