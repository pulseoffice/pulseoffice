using Avalonia.Controls;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class Round206TextToColumnsTests
{
    private sealed class AnsweringDialogs(object answer) : IDialogService
    {
        public bool IsDialogOpen => false;
        public Task<TResult?> ShowAsync<TResult>(Control content, DialogOptions options, CancellationToken ct = default) => Task.FromResult((TResult?)answer);
        public Task ShowAsync(Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;
        public void Close(object? result) { }
    }

    private sealed class FixedSplit(IReadOnlyDictionary<CellAddress, CellValue> values) : Engine.Operations.ITextToColumns
    {
        public Engine.Operations.TextToColumnsPlan Plan(Worksheet sheet, Engine.Operations.TextToColumnsRequest request) => new(values, 2);

        public IReadOnlyList<string> Split(string text, Engine.Operations.TextToColumnsRequest request) => [];
    }

    private static async Task Split(Round206Scene scene, Round206FixedOverwritePrompt overwrite)
    {
        scene.Sheet.SetValue(Round206Scene.A("A1"), CellValue.Text("x;y"));
        var split = new FixedSplit(new Dictionary<CellAddress, CellValue>
        {
            [Round206Scene.A("A1")] = CellValue.Text("x"),
            [Round206Scene.A("B1")] = CellValue.Text("y"),
        });
        var choice = new Dialogs.TextToColumnsChoice(false, true, false, false, string.Empty, false, Dialogs.TextQualifier.Double, false);
        var commands = new LedgerToolCommands(scene.Context, new Engine.Operations.SheetSnapshots(),
            Round206Scene.Fake<Engine.Operations.ISheetSubtotals>(), split, new AnsweringDialogs(new Dictionary<string, object> { ["options"] = choice }),
            new Round206Status(), new SheetEditGuard(), Round206Scene.Fake<ISheetSortGate>(), Round206Scene.Fake<Dialogs.IValidationRules>(),
            new Engine.Operations.ValidationPlacement(), Round206Scene.Fake<Dialogs.ISheetReferenceSources>(),
            Round206Scene.Fake<Dialogs.ISheetReferenceReader>(), Round206Scene.Texts, null!, null!,
            Round206Scene.Fake<DataDialogs.ILedgerDialogProbes>(), Round206Scene.Fake<ISheetRangeFilters>(), overwrite);

        await commands.GetCommands().Single(command => command.Id.Value == "ledger.data.textToColumns").Execute(null!, CancellationToken.None);
    }

    [Fact]
    public async Task Declining_to_replace_the_cells_on_the_right_keeps_them()
    {
        var scene = new Round206Scene();
        scene.Sheet.SetValue(Round206Scene.A("B1"), CellValue.Text("kept"));
        var overwrite = new Round206FixedOverwritePrompt(false);

        await Split(scene, overwrite);

        Assert.Equal(1, overwrite.Calls);
        Assert.Equal("kept", scene.Text("B1"));
        Assert.Equal("x;y", scene.Text("A1"));
        Assert.False(scene.History.CanUndo);
    }

    [Fact]
    public async Task Empty_cells_on_the_right_are_written_without_asking()
    {
        var scene = new Round206Scene();
        var overwrite = new Round206FixedOverwritePrompt(false);

        await Split(scene, overwrite);

        Assert.Equal(0, overwrite.Calls);
        Assert.Equal("y", scene.Text("B1"));
    }
}
