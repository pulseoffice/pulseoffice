using System.IO.Compression;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OpenXmlWorkbookPartsAuditTests
{
    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static string Entry(MemoryStream stream, string name)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void A_sheet_whose_relationship_id_names_nothing_does_not_reject_the_workbook()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Primo").SetValue(new CellAddress(0, 0), CellValue.Number(1));
        workbook.AddSheet("Secondo").SetValue(new CellAddress(0, 0), CellValue.Number(2));
        using var stream = Written(workbook);
        var xml = Entry(stream, "xl/workbook.xml");
        var second = System.Text.RegularExpressions.Regex.Match(xml, "<(?:x:)?sheet name=\"Secondo\"[^>]*r:id=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(second);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            archive.GetEntry("xl/workbook.xml")!.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("xl/workbook.xml").Open());
            writer.Write(xml.Replace($"r:id=\"{second}\"", "r:id=\"rId999\"", StringComparison.Ordinal));
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        Assert.Equal(CellValue.Number(1), result.Value!.Sheets[0].GetValue(new CellAddress(0, 0)));
    }

    [Fact]
    public void A_cell_colour_that_is_not_six_or_eight_hex_digits_is_not_written_as_an_invalid_argb()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var style = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#ABC", Font = CellFont.Default with { ColorHex = "rosso" } });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetStyle(new CellAddress(0, 0), style);
        using var stream = Written(workbook);

        var styles = Entry(stream, "xl/styles.xml");

        Assert.All(System.Text.RegularExpressions.Regex.Matches(styles, "rgb=\"([^\"]*)\"").Select(match => match.Groups[1].Value),
            value => Assert.Matches("^[0-9A-F]{8}$", value));
    }

    [Fact]
    public void A_comment_author_index_beyond_the_signed_range_does_not_reject_the_workbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "Nota", Author = "Anna" };
        using var stream = Written(workbook);
        var name = "xl/comments1.xml";
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.Entries.Single(found => found.FullName.Contains("comments", StringComparison.Ordinal) && found.FullName.EndsWith(".xml", StringComparison.Ordinal));
            name = entry.FullName;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(System.Text.RegularExpressions.Regex.Replace(xml, "authorId=\"\\d+\"", "authorId=\"4294967295\""));
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var comment = Assert.Single(result.Value!.Sheets[0].Comments).Value;
        Assert.Equal("Nota", comment.Text);
        Assert.Null(comment.Author);
    }
}
