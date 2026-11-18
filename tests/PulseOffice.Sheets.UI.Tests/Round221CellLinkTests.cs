using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.UI.Controls.Links;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round221CellLinkTests
{
    private static readonly Engine.Recalculation.ICalculationEngineFactory Engines =
        new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<Engine.Recalculation.ICalculationEngineFactory>();

    private sealed class RecordingFollower : ILinkFollower
    {
        public List<string> Opened { get; } = [];

        public bool Follow(string? link, Func<string, bool> place)
        {
            if (link is ['#', ..]) return place(link);
            Opened.Add(link!);
            return true;
        }
    }

    [Fact]
    public async Task The_hyperlink_of_the_current_cell_opens_and_internal_places_select_their_range()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("Totals");
        var sheet = workbook.Sheets[0];
        sheet[new CellAddress(0, 0)] = sheet[new CellAddress(0, 0)] with { Hyperlink = "#Totals.C3" };
        sheet[new CellAddress(1, 0)] = sheet[new CellAddress(1, 0)] with { Hyperlink = "https://example.test/" };
        var active = new CellAddress(2, 0);
        var selected = new List<CellRange>();
        var sheets = new List<int>();
        var context = new ActiveSheetContext();
        context.Attach(new SheetTarget
        {
            Workbook = workbook,
            SheetIndex = 0,
            History = new UndoStack(),
            Engine = Engines.Create(workbook),
            Selection = () => CellRange.Single(active),
            WriteSelection = range => selected.Add(range),
            ReadViewOption = _ => false,
            WriteViewOption = (_, _) => { },
            Refresh = () => { },
            FreezePanes = (_, _) => { },
            SplitPanes = (_, _) => { },
            ReadPanesSplit = () => false,
            ApplyRowFilter = (_, _) => { },
            ReadZoom = () => 1,
            WriteZoom = _ => { },
            ReadSelectedObject = () => null,
            ReadSelectedObjects = () => [],
            WriteSelectedObjects = _ => { },
            SelectSheet = index => sheets.Add(index),
            WriteSelectedObject = _ => { },
        });
        var follower = new RecordingFollower();
        var open = new LedgerLinkCommands(context, follower, new LedgerPlaceNavigation(new NameBoxResolver())).GetCommands().Single();

        Assert.False(open.EnabledWhen!(null!));

        active = new CellAddress(1, 0);
        await open.Execute(null!, CancellationToken.None);
        Assert.Equal(new[] { "https://example.test/" }, follower.Opened);

        active = new CellAddress(0, 0);
        await open.Execute(null!, CancellationToken.None);
        Assert.Equal(new[] { 1 }, sheets);
        Assert.Equal(new CellRange(new CellAddress(2, 2), new CellAddress(2, 2)), selected[^1]);
    }
}
