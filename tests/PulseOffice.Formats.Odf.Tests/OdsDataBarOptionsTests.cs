using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsDataBarOptionsTests
{
    private static Workbook With(ConditionalRule rule)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").ConditionalRules.Add(rule);
        return workbook;
    }

    private static ConditionalRule Bar(DataBarOptions options) => new()
    {
        Range = new CellRange(new CellAddress(0, 0), new CellAddress(4, 0)),
        Visual = ConditionalVisual.DataBar,
        NegativeBarColorHex = "#CC0000",
        ShowValue = false,
        Thresholds = [new ConditionalThreshold(ConditionalThresholdKind.AutomaticMinimum), new ConditionalThreshold(ConditionalThresholdKind.Number, 50)],
        Bar = options,
    };

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void LeOpzioniDellaBarraAttraversanoOds()
    {
        var options = new DataBarOptions { Gradient = false, Axis = DataBarAxis.Middle, AxisColorHex = "#00AA00", MinimumLength = 10, MaximumLength = 90 };

        var reread = Assert.Single(RoundTrip(With(Bar(options))).Sheets[0].ConditionalRules);

        Assert.Equal(options, reread.Bar);
        Assert.Equal("#CC0000", reread.NegativeBarColorHex);
        Assert.False(reread.ShowValue);
        Assert.Equal(ConditionalThresholdKind.AutomaticMinimum, reread.Thresholds[0].Kind);
        Assert.Equal(50, reread.Thresholds[1].Value);
    }

    [Fact]
    public void BordoEDirezioneSonoPerditeDichiarate()
    {
        var plain = new OdsSpreadsheetWriter().CheckFidelity(With(Bar(new DataBarOptions()))).Losses.Select(loss => loss.Code).ToList();
        var lossy = new OdsSpreadsheetWriter().CheckFidelity(With(Bar(new DataBarOptions
        {
            BorderColorHex = "#000000",
            Direction = DataBarDirection.RightToLeft,
        }))).Losses.Select(loss => loss.Code).ToList();

        Assert.DoesNotContain("ODF086", plain);
        Assert.DoesNotContain("ODF087", plain);
        Assert.Contains("ODF086", lossy);
        Assert.Contains("ODF087", lossy);
    }
}
