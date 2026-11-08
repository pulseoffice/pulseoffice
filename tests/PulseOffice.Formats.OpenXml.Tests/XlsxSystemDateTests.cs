using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxSystemDateTests
{
    [Theory]
    [InlineData(NumberFormatCatalog.SystemShortDateId)]
    [InlineData(NumberFormatCatalog.SystemDateTimeId)]
    public void IlFormatoDataDiSistemaAttraversaXlsxSenzaDiventareFisso(int id)
    {
        var source = Workbook.CreateDefault();
        var a1 = new CellAddress(0, 0);
        source.Sheets[0].SetValue(a1, CellValue.Number(46313));
        source.Sheets[0].SetStyle(a1, source.Styles.Intern(new CellStyle { NumberFormatId = id }));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded);
        var back = result.Value!;
        Assert.Equal(id, back.Styles.Get(back.Sheets[0].StyleIdAt(a1)).NumberFormatId);
    }
}
