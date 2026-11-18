using PulseOffice.Testing;
using PulseOffice.Charts;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class Round192ExchangeTests : IDisposable
{
    private readonly UiThreadSession _session;

    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-round192-exchange-" + Guid.NewGuid().ToString("N"));

    public Round192ExchangeTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static ChartDefinition ErrorBarChart(string sheet, ChartPivotSource? pivot) => new()
    {
        Id = "barre",
        Type = ChartType.Column,
        SourceSheet = sheet,
        SourceRange = "A1:B3",
        PivotSource = pivot,
        SeriesFormats =
        [
            new ChartSeriesFormat
            {
                ErrorBars = new ChartErrorBars(ErrorBarKind.Custom, 0)
                {
                    Plus = new ChartErrorValues($"{sheet}!C2:C3", []),
                    Minus = new ChartErrorValues($"{sheet}!C2:C3", []),
                },
            },
        ],
    };

    private static ShapeGroupObject Group() => new()
    {
        Id = "gruppo",
        Children =
        [
            new GroupChild(new ShapeObject { Id = "g1", Shape = ShapeKind.Rectangle, Text = "Uno" }, 0, 0, 0.5, 1),
            new GroupChild(new ShapeObject { Id = "g2", Shape = ShapeKind.Ellipse, Text = "Due" }, 0.5, 0, 0.5, 1),
        ],
    };

    [Fact]
    public Task Charts_with_error_bars_and_pivot_links_groups_and_drops_cross_every_module() => _session.Dispatch(async () =>
    {
        await using var suite = await new ExchangeSuiteFactory().Open();
        var sheet = suite.Sheet;
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Mese"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Vendite"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Gen"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(10));
        sheet.SetValue(new CellAddress(1, 2), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("Feb"));
        sheet.SetValue(new CellAddress(2, 1), CellValue.Number(20));
        sheet.SetValue(new CellAddress(2, 2), CellValue.Number(2));
        suite.Ledger.Workbook!.Pivots.Add(new PivotDefinition
        {
            Id = "p1",
            Name = "Vendite",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)),
            Destination = new CellAddress(10, 0),
            DestinationSheet = sheet.Name,
        });

        suite.PlaceInLedger(ErrorBarChart(sheet.Name, new ChartPivotSource("Vendite") { FormatId = 0 }));
        Assert.True(await suite.Copy(ModuleKind.Sheets));
        var portable = Assert.IsType<ChartDefinition>(Assert.Single(((ObjectsContent)suite.Exchange.Last!.Content!).Objects).Object);
        Assert.Null(portable.PivotSource);
        Assert.NotNull(portable.InlineData);
        Assert.Equal(new[] { 1d, 2d }, portable.SeriesFormat(0).ErrorBars!.Plus!.Values);

        foreach (var to in new[] { ModuleKind.Documents, ModuleKind.Presentations })
        {
            Assert.True(await suite.Paste(to), $"chart not pasted into {to}");
            var pasted = suite.Objects(to).OfType<ChartDefinition>().Last();
            Assert.Null(pasted.PivotSource);
            Assert.Equal(new[] { 1d, 2d }, pasted.SeriesFormat(0).ErrorBars!.Minus!.Values);
        }

        Assert.True(await suite.Paste(ModuleKind.Sheets));
        var linked = suite.LedgerObjects().OfType<ChartDefinition>().Where(chart => chart.PivotSource is not null).ToList();
        Assert.Equal(2, linked.Count);
        Assert.Equal(2, linked.Select(chart => chart.PivotSource!.FormatId).Distinct().Count());

        suite.Ledger.Workbook!.Pivots.Clear();
        Assert.True(await suite.Paste(ModuleKind.Sheets));
        Assert.Null(suite.LedgerObjects().OfType<ChartDefinition>().Last().PivotSource);

        var drops = suite.Service<IExchangeDragDrop>();
        var transfers = suite.Service<PulseOffice.UI.Controls.Services.IExchangeDataTransfers>();
        suite.PlaceInDeck(Group());
        var group = suite.Exchange.Capture(ModuleKind.Presentations)!;
        foreach (var to in new[] { ModuleKind.Sheets, ModuleKind.Documents })
        {
            suite.Activate(to);
            Assert.True(await drops.DropAsync(transfers.Write(suite.Exchange.Publishable(group))), $"group not dropped into {to}");
            var dropped = suite.Objects(to).OfType<ShapeGroupObject>().Last();
            Assert.Equal(new[] { "Uno", "Due" }, dropped.Children.Select(child => ((ShapeObject)child.Content).Text));
        }

        suite.Document(ModuleKind.Documents).IsReadOnly = true;
        suite.Activate(ModuleKind.Documents);
        var before = suite.FolioObjects().Count;
        Assert.False(await drops.DropAsync(transfers.Write(suite.Exchange.Publishable(group))));
        Assert.Equal(before, suite.FolioObjects().Count);
    }, CancellationToken.None);
}
