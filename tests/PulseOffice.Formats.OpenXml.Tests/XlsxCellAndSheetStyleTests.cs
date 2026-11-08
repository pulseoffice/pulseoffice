using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxCellAndSheetStyleTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static CellStyle StyleRoundTrip(CellStyle style)
    {
        var source = Workbook.CreateDefault();
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        source.Sheets[0].SetValue(a1, CellValue.Number(1));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(style));
        var read = RoundTrip(source);
        return read.Styles.Get(read.Sheets[0][a1].StyleId);
    }

    [Fact]
    public void LeDiagonaliSopravvivono()
    {
        var pen = new BorderEdge(BorderStyle.Thin, "#FF0000", PulseOffice.Foundation.Units.Twip.FromPoints(1));
        var result = StyleRoundTrip(new CellStyle { Borders = CellBorders.None with { DiagonalUp = pen, DiagonalDown = pen } });

        Assert.True(result.Borders.DiagonalUp.IsVisible);
        Assert.True(result.Borders.DiagonalDown.IsVisible);
        Assert.Equal("#FF0000", result.Borders.DiagonalUp.ColorHex.ToUpperInvariant());
    }

    [Fact]
    public void AllineamentoVerticaleRotazioneEProtezioneSopravvivono()
    {
        var result = StyleRoundTrip(new CellStyle { Vertical = CellVerticalAlignment.Top, RotationDegrees = -30, Locked = false });
        Assert.Equal(CellVerticalAlignment.Top, result.Vertical);
        Assert.Equal(-30, result.RotationDegrees);
        Assert.False(result.Locked);
    }

    [Fact]
    public void FoglioNascostoEColoreDellaSchedaSopravvivono()
    {
        var source = Workbook.CreateDefault();
        var second = source.AddSheet("Nascosto");
        second.IsHidden = true;
        second.TabColorHex = "#00FF00";

        var read = RoundTrip(source);
        Assert.True(read.Sheets[1].IsHidden);
        Assert.Equal("#00FF00", read.Sheets[1].TabColorHex);
        Assert.False(read.Sheets[0].IsHidden);
    }

    [Fact]
    public void LaDataDiCreazioneTornaInUtcSenzaSpostamenti()
    {
        var source = Workbook.CreateDefault();
        var moment = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(2));
        source.Properties.Created = moment;
        var read = RoundTrip(source);
        Assert.Equal(moment.UtcDateTime, read.Properties.Created!.Value.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, read.Properties.Created!.Value.Offset);
    }

    [Fact]
    public void IFormatiNumericiPersonalizzatiTornanoNelCatalogo()
    {
        var source = Workbook.CreateDefault();
        source.NumberFormats.Register("0.0");
        var id = source.NumberFormats.Register("[$€-410] #,##0.000");
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        source.Sheets[0].SetValue(a1, CellValue.Number(3));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(new CellStyle { NumberFormatId = id }));

        var read = RoundTrip(source);
        var readId = read.Styles.Get(read.Sheets[0][a1].StyleId).NumberFormatId;
        Assert.Equal("[$€-410] #,##0.000", read.NumberFormats.CodeOf(readId));
    }

    [Theory]
    [InlineData("https://example.org/a?b=1")]
    [InlineData("#Dati 2024.B3")]
    [InlineData("#Sheet1.A1")]
    [InlineData("altri/bilancio.ods")]
    public void ICollegamentiSuCelleNumericheInterniERelativiSopravvivono(string link)
    {
        var source = Workbook.CreateDefault();
        source.AddSheet("Dati 2024");
        Assert.True(CellAddress.TryParseA1("B2", out var b2));
        source.Sheets[0][b2] = new Cell { Value = CellValue.Number(42), Hyperlink = link };

        var read = RoundTrip(source).Sheets[0][b2];
        Assert.Equal(link, read.Hyperlink);
        Assert.Equal(42, read.Value.AsNumber());
    }
}
