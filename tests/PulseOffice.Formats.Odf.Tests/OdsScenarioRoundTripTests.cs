using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsScenarioRoundTripTests
{
    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();

        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);

        return result.Value!;
    }

    private static Workbook WithScenario()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];

        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(2));

        sheet.Scenarios.Add(new Scenario
        {
            Name = "Ottimista",
            Comment = "Il caso migliore",
            Range = new CellRange(CellAddress.First, new CellAddress(1, 0)),
            Values = new Dictionary<CellAddress, CellValue>
            {
                [CellAddress.First] = CellValue.Number(10),
                [new CellAddress(1, 0)] = CellValue.Number(20),
            },
        });

        return workbook;
    }

    [Fact]
    public void UnoScenarioSopravviveConIValori()
    {
        var reread = RoundTrip(WithScenario());
        var scenario = Assert.Single(reread.Sheets[0].Scenarios);

        Assert.Equal("Ottimista", scenario.Name);
        Assert.Equal(10, scenario.Values[CellAddress.First].AsNumber());
        Assert.Equal(20, scenario.Values[new CellAddress(1, 0)].AsNumber());
    }

    [Fact]
    public void LoScenarioNonDiventaUnFoglioInPiu()
    {
        var reread = RoundTrip(WithScenario());

        Assert.Single(reread.Sheets);
        Assert.Null(reread.FindSheet("Ottimista"));
    }

    [Fact]
    public void IlCommentoResta()
    {
        Assert.Equal("Il caso migliore", RoundTrip(WithScenario()).Sheets[0].Scenarios[0].Comment);
    }

    [Fact]
    public void LeCelleDelFoglioNonVengonoToccate()
    {
        var reread = RoundTrip(WithScenario());

        Assert.Equal(1, reread.Sheets[0].GetValue(CellAddress.First).AsNumber());
    }

    [Fact]
    public void LaFormulaDiUnoScenarioSopravvive()
    {
        var workbook = WithScenario();
        var sheet = workbook.Sheets[0];
        sheet.Scenarios[0] = sheet.Scenarios[0] with
        {
            Formulas = new Dictionary<CellAddress, string> { [new CellAddress(1, 0)] = "=A1*2" },
        };

        var scenario = Assert.Single(RoundTrip(workbook).Sheets[0].Scenarios);

        Assert.Equal("=A1*2", scenario.Formulas[new CellAddress(1, 0)]);
    }
}
