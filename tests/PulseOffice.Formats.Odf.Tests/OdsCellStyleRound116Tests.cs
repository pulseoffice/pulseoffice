using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsCellStyleRound116Tests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static Workbook WithStyle(string name, string? parent = null)
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define(name, CellStyle.Default with { BackgroundHex = "#FFE08A" }, parent);
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(workbook.NamedStyles.StyleOf(workbook.NamedStyles.Find(name)!)));
        sheet.SetStyleName(CellAddress.First, name);
        return workbook;
    }

    [Fact]
    public void The_parent_of_a_cell_style_survives_the_round_trip()
    {
        var reread = RoundTrip(WithStyle("Sottotitolo", "Heading"));

        Assert.Equal("Heading", reread.NamedStyles.Find("Sottotitolo")!.Parent);
    }

    [Theory]
    [InlineData("cerise")]
    [InlineData("ConditionalStyle 1")]
    public void A_style_whose_name_starts_like_an_automatic_one_is_kept(string name)
    {
        var reread = RoundTrip(WithStyle(name));

        Assert.NotNull(reread.NamedStyles.Find(name));
        Assert.Equal(name, reread.Sheets[0][CellAddress.First].StyleName);
    }

    [Theory]
    [InlineData("Évidence 1")]
    [InlineData("Totale €/mese")]
    [InlineData("税 合計")]
    public void A_style_name_with_spaces_or_unicode_is_written_as_a_valid_name_and_read_back_once(string name)
    {
        var workbook = WithStyle(name);
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var entry = zip.GetEntry("styles.xml")!.Open();
            var styles = XDocument.Load(entry);
            var declared = styles.Descendants(Style + "style").Single(element => (string?)element.Attribute(Style + "display-name") == name);
            System.Xml.XmlConvert.VerifyNCName((string)declared.Attribute(Style + "name")!);
        }
        stream.Position = 0;
        var reread = new OdsReader().Read(stream).Value!;

        Assert.Equal(workbook.NamedStyles.All.Count, reread.NamedStyles.All.Count);
        Assert.NotNull(reread.NamedStyles.Find(name));
        Assert.Equal(name, reread.Sheets[0][CellAddress.First].StyleName);
    }
}
