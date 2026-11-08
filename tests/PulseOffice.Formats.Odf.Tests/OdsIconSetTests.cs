using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsIconSetTests
{
    private static Workbook Workbook(bool reverse)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0)), Visual = ConditionalVisual.IconSet, IconSetName = "3Stars", ReverseIcons = reverse,
            Thresholds = [new(ConditionalThresholdKind.Percent, 0), new(ConditionalThresholdKind.Number, 3, null, false), new(ConditionalThresholdKind.Number, 8)],
        });
        return workbook;
    }

    [Fact]
    public void LaSogliaStrettaFaIlGiroComeNelRiferimento()
    {
        var workbook = Workbook(false);
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var rule = new OdsReader().Read(stream).Value!.Sheets[0].ConditionalRules.Single();

        Assert.Equal("3Stars", rule.IconSetName);
        Assert.Equal(workbook.Sheets[0].ConditionalRules[0].Thresholds, rule.Thresholds);
    }

    [Fact]
    public void LInversioneDelleIconeEDichiarata() =>
        Assert.Contains("ODF040", new OdsSpreadsheetWriter().CheckFidelity(Workbook(true)).Losses.Select(loss => loss.Code));

    [Fact]
    public void LeIconePersonalizzateSiScrivonoComeNelRiferimento()
    {
        var workbook = Workbook(false);
        var original = workbook.Sheets[0].ConditionalRules[0];
        workbook.Sheets[0].ConditionalRules[0] = original with { CustomIcons = [new("3Flags", 2), new(ConditionalIconChoice.NoIcons, -1), new("5Boxes", 4)] };
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var rule = new OdsReader().Read(stream).Value!.Sheets[0].ConditionalRules.Single();

        Assert.Equal(workbook.Sheets[0].ConditionalRules[0].CustomIcons, rule.CustomIcons);
    }
}
