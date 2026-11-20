using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Charts;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round319ChartPlacementTests
{
    [Fact]
    public void A_chart_moved_past_the_first_cell_stops_at_the_sheet_edge()
    {
        var workbook = Workbook.CreateDefault();
        var engine = new ServiceCollection().AddSheetsEngine().BuildServiceProvider().GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        var viewport = new PulseOffice.Sheets.UI.GridParts.GridViewport(engine,
            PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact), 0, () => new Avalonia.Size(800, 600), new PulseOffice.Sheets.UI.Outline.OutlineGutter());
        var placed = new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 0),
            Content = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = "Sheet1", SourceRange = "A1:B3" },
        };
        var bounds = viewport.BoundsOf(placed);

        var change = viewport.ChangeOf(placed, new ObjectBox(bounds.X - 40, bounds.Y - 25, bounds.Width, bounds.Height), null);

        Assert.Equal(new CellAddress(0, 0), change.Anchor);
        Assert.Equal(Twip.Zero, change.OffsetX);
        Assert.Equal(Twip.Zero, change.OffsetY);
        Assert.Equal(placed.Size, change.Size);
    }
}
