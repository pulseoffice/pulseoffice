using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxMergeRoundTripTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void LeUnioniTornanoIndietro()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Text("Rendiconto"));
        source.Sheets[0].Merges.Add(R("A1:C1"));
        source.Sheets[0].Merges.Add(R("A3:A6"));

        var sheet = RoundTrip(source).Sheets[0];

        Assert.Equal(2, sheet.Merges.Count);
        Assert.Contains(R("A1:C1"), sheet.Merges.All);
        Assert.Contains(R("A3:A6"), sheet.Merges.All);
        Assert.Equal("Rendiconto", sheet[A("A1")].Value.ToString());
    }

    [Fact]
    public void SenzaUnioniIlFoglioNonCambia()
    {
        var source = Workbook.CreateDefault();
        source.Sheets[0].SetValue(A("A1"), CellValue.Number(1));

        Assert.Equal(0, RoundTrip(source).Sheets[0].Merges.Count);
    }
}
