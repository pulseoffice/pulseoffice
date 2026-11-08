using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsSystemDateTests
{
    [Fact]
    public void IlFormatoDataDiSistemaInOdsDiventaFissoEDichiaraLaPerdita()
    {
        var source = Workbook.CreateDefault();
        var a1 = new CellAddress(0, 0);
        source.Sheets[0].SetValue(a1, CellValue.Number(46313));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(new CellStyle { NumberFormatId = NumberFormatCatalog.SystemShortDateId }));

        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(source).Losses, loss => loss.Code == "ODF170");

        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded);
        var back = result.Value!;
        var code = back.NumberFormats.CodeOf(back.Styles.Get(back.Sheets[0].StyleIdAt(a1)).NumberFormatId);
        Assert.Equal(source.NumberFormats.CodeOf(NumberFormatCatalog.SystemShortDateId), code);
    }

    [Fact]
    public void UnaDataConFormatoEsplicitoNonDichiaraLaPerdita()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetStyle(new CellAddress(0, 0), source.Styles.Intern(new CellStyle { NumberFormatId = source.NumberFormats.Register("yyyy-mm-dd") }));

        Assert.DoesNotContain(new OdsSpreadsheetWriter().CheckFidelity(source).Losses, loss => loss.Code == "ODF170");
    }
}
