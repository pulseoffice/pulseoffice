using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxFormulaRoundTripTests
{
    private static Workbook WithFormula(string expression)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.Objects.Add(new PlacedObject
        {
            Id = "f1",
            Content = new FormulaObject { Id = "f1", Expression = expression },
            Anchor = new CellAddress(3, 2),
            Size = new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)),
        });

        return workbook;
    }

    private static FormulaObject RoundTrip(string expression)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(WithFormula(expression), stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var placed = Assert.Single(result.Value!.Sheets[0].Objects);
        Assert.Equal(new CellAddress(3, 2), placed.Anchor);

        return Assert.IsType<FormulaObject>(placed.Content);
    }

    [Theory]
    [InlineData("frac {a + b} {c}")]
    [InlineData("sqrt {x^2 + y^2}")]
    [InlineData("sum_{i=1}^{n} x_i")]
    [InlineData("matrix {a & b # c & d}")]
    public void UnaFormulaSopravviveAlGiroInXlsx(string expression)
    {
        Assert.Equal(expression, RoundTrip(expression).Expression);
    }

    [Fact]
    public void LaPerditaEDichiarata()
    {
        var report = new XlsxSpreadsheetWriter().CheckFidelity(WithFormula("frac {1} {2}"));

        Assert.Contains(report.Losses, loss => loss.Code == "XLSX021");
    }
}
