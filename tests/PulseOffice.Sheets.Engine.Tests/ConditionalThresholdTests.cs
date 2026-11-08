using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ConditionalThresholdTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellRange Column = new(new CellAddress(0, 0), new CellAddress(9, 0));

    private Worksheet Sheet(ConditionalRule rule)
    {
        var sheet = new Worksheet("Foglio1");
        for (var i = 0; i < 10; i++) sheet.SetValue(new CellAddress(i, 0), CellValue.Number(i + 1));
        sheet.ConditionalRules.Add(rule);
        return sheet;
    }

    private ConditionalAppearance At(Worksheet sheet, int row) => new ConditionalFormattingEvaluator(fixture.Evaluator).Evaluate(sheet, new CellAddress(row, 0));

    [Fact]
    public void LaScalaUsaLeSoglieNumeriche()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.ColorScale, ScaleColors = ["#000000", "#FFFFFF"],
            Thresholds = [new(ConditionalThresholdKind.Number, 3), new(ConditionalThresholdKind.Number, 5)],
        });

        Assert.Equal("#000000", At(sheet, 0).ScaleColorHex);
        Assert.Equal("#808080", At(sheet, 3).ScaleColorHex);
        Assert.Equal("#FFFFFF", At(sheet, 9).ScaleColorHex);
    }

    [Fact]
    public void LaBarraNegativaPrendeIlSuoColore()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.DataBar, NegativeBarColorHex = "#FF0000",
            Thresholds = [new(ConditionalThresholdKind.Number, -10), new(ConditionalThresholdKind.Number, 10)],
        });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(-5));

        Assert.Equal("#FF0000", At(sheet, 0).BarColorHex);
        Assert.Equal(0.25, At(sheet, 0).BarFraction!.Value, 6);
        Assert.Equal(0.75, At(sheet, 4).BarFraction!.Value, 6);
    }

    [Fact]
    public void IlMinimoAutomaticoDellaBarraParteDaZero()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.DataBar,
            Thresholds = [new(ConditionalThresholdKind.AutomaticMinimum), new(ConditionalThresholdKind.AutomaticMaximum)],
        });
        var plain = Sheet(new ConditionalRule { Range = Column, Visual = ConditionalVisual.DataBar,
            Thresholds = [new(ConditionalThresholdKind.Minimum), new(ConditionalThresholdKind.Maximum)] });

        Assert.Equal(0.1, At(sheet, 0).BarFraction!.Value, 6);
        Assert.Equal(0, At(plain, 0).BarFraction!.Value, 6);
    }

    [Fact]
    public void LIconaDipendeDalleSoglie()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.IconSet, IconSetName = "3TrafficLights1", ShowValue = false,
            Thresholds = [new(ConditionalThresholdKind.Percent, 0), new(ConditionalThresholdKind.Number, 4), new(ConditionalThresholdKind.Percentile, 80)],
        });

        Assert.Equal(0, At(sheet, 0).IconIndex);
        Assert.Equal(1, At(sheet, 3).IconIndex);
        Assert.Equal(2, At(sheet, 9).IconIndex);
        Assert.True(At(sheet, 9).HideValue);
        Assert.Equal("3TrafficLights1", At(sheet, 9).IconSetName);
    }

    [Fact]
    public void UnaSogliaStrettaEsclusaIlValoreUgualeELInversioneGiraLeIcone()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.IconSet, IconSetName = "3Arrows", ReverseIcons = true,
            Thresholds = [new(ConditionalThresholdKind.Number, 0), new(ConditionalThresholdKind.Number, 4, null, false), new(ConditionalThresholdKind.Number, 8)],
        });

        Assert.Equal(2, At(sheet, 3).IconIndex);
        Assert.Equal(1, At(sheet, 4).IconIndex);
        Assert.Equal(0, At(sheet, 7).IconIndex);
    }

    [Fact]
    public void LeIconePersonalizzateSostituisconoQuelleDellInsieme()
    {
        var sheet = Sheet(new ConditionalRule
        {
            Range = Column, Visual = ConditionalVisual.IconSet, IconSetName = "3Arrows",
            Thresholds = [new(ConditionalThresholdKind.Number, 0), new(ConditionalThresholdKind.Number, 4), new(ConditionalThresholdKind.Number, 8)],
            CustomIcons = [new("3Flags", 0), new(ConditionalIconChoice.NoIcons, -1), new("3Stars", 2)],
        });

        Assert.Equal(("3Flags", 0), (At(sheet, 0).IconSetName, At(sheet, 0).IconIndex!.Value));
        Assert.Null(At(sheet, 4).IconIndex);
        Assert.Equal(("3Stars", 2), (At(sheet, 9).IconSetName, At(sheet, 9).IconIndex!.Value));
    }
}
