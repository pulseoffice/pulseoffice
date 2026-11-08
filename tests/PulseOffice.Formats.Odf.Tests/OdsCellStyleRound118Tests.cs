using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsCellStyleRound118Tests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"";

    private static MemoryStream Foreign()
    {
        var styles = $"<office:document-styles {Namespaces} office:version=\"1.3\"><office:styles>" +
            "<style:style style:name=\"Default\" style:family=\"table-cell\"/>" +
            "<style:style style:name=\"Base\" style:family=\"table-cell\" style:parent-style-name=\"Default\"><style:table-cell-properties fo:background-color=\"#ffe08a\"/><style:text-properties fo:font-weight=\"bold\"/></style:style>" +
            "<style:style style:name=\"Derived\" style:family=\"table-cell\" style:parent-style-name=\"Base\"><style:text-properties fo:font-style=\"italic\"/></style:style>" +
            "</office:styles></office:document-styles>";
        var content = $"<office:document-content {Namespaces} office:version=\"1.3\"><office:automatic-styles>" +
            "<style:style style:name=\"ce1\" style:family=\"table-cell\" style:parent-style-name=\"Derived\"><style:text-properties fo:color=\"#c00000\"/></style:style>" +
            "</office:automatic-styles><office:body><office:spreadsheet><table:table table:name=\"S\"><table:table-row>" +
            "<table:table-cell table:style-name=\"ce1\" office:value-type=\"float\" office:value=\"1\"><text:p>1</text:p></table:table-cell>" +
            "</table:table-row></table:table></office:spreadsheet></office:body></office:document-content>";
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in new[] { ("mimetype", "application/vnd.oasis.opendocument.spreadsheet"), ("styles.xml", styles), ("content.xml", content) })
                using (var writer = new StreamWriter(archive.CreateEntry(path).Open())) writer.Write(text);
        buffer.Position = 0;
        return buffer;
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact]
    public void An_automatic_style_from_another_suite_inherits_its_parents_properties()
    {
        var result = new OdsReader().Read(Foreign());
        Assert.True(result.Succeeded);
        var workbook = result.Value!;
        var style = workbook.Styles.Get(workbook.Sheets[0][CellAddress.First].StyleId);

        Assert.Equal("#c00000", style.Font.ColorHex, ignoreCase: true);
        Assert.True(style.Font.Italic);
        Assert.True(style.Font.Bold);
        Assert.Equal("#ffe08a", style.BackgroundHex, ignoreCase: true);
        Assert.Equal("Derived", workbook.Sheets[0][CellAddress.First].StyleName);
    }

    [Fact]
    public void Default_as_parent_and_no_parent_stay_distinct()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Orphan", CellStyle.Default with { BackgroundHex = "#FFE08A" });
        workbook.NamedStyles.Define("Child of default", CellStyle.Default with { BackgroundHex = "#8AE0FF" }, "Default");

        var reread = RoundTrip(workbook);

        Assert.Null(reread.NamedStyles.Find("Orphan")!.Parent);
        Assert.Equal("Default", reread.NamedStyles.Find("Child of default")!.Parent);
    }

    [Fact]
    public void A_cell_that_overrides_its_named_style_back_to_plain_keeps_the_override()
    {
        var workbook = Workbook.CreateDefault();
        workbook.NamedStyles.Define("Loud", CellStyle.Default with { BackgroundHex = "#FFE08A", Font = CellFont.Default with { Bold = true, Italic = true } });
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        sheet.SetStyle(CellAddress.First, workbook.Styles.Intern(CellStyle.Default with { Font = CellFont.Default with { Italic = true } }));
        sheet.SetStyleName(CellAddress.First, "Loud");

        var reread = RoundTrip(workbook);
        var style = reread.Styles.Get(reread.Sheets[0][CellAddress.First].StyleId);

        Assert.False(style.Font.Bold);
        Assert.True(style.Font.Italic);
        Assert.Null(style.BackgroundHex);
        Assert.Equal("Loud", reread.Sheets[0][CellAddress.First].StyleName);
    }
}
