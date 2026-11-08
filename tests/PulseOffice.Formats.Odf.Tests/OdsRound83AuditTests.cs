using System.Diagnostics;
using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsRound83AuditTests
{
    private static MemoryStream Rewritten(Workbook workbook, Func<string, string> edit)
    {
        var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(edit(content));
        }
        stream.Position = 0;
        return stream;
    }

    private static string Content(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Duplicate_sheet_names_are_renamed_without_colliding_with_a_later_sheet()
    {
        var workbook = new Workbook();
        workbook.AddSheet("A_3").SetValue(CellAddress.First, CellValue.Text("first"));
        workbook.AddSheet("A").SetValue(CellAddress.First, CellValue.Text("second"));
        workbook.AddSheet("B").SetValue(CellAddress.First, CellValue.Text("third"));

        using var stream = Rewritten(workbook, content => content.Replace("table:name=\"B\"", "table:name=\"A\"", StringComparison.Ordinal));
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded);
        var sheets = result.Value!.Sheets;
        Assert.Equal(3, sheets.Count);
        Assert.Equal(3, sheets.Select(sheet => sheet.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("third", sheets[2].GetValue(CellAddress.First).AsText());
    }

    [Fact]
    public void Workbook_named_expressions_follow_the_tables_as_the_schema_requires()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetValue(CellAddress.First, CellValue.Number(4));
        workbook.DefineName(new DefinedName("Total", "Sheet1!$A$1", null));

        var content = Content(workbook);

        var names = content.IndexOf("<table:named-expressions", StringComparison.Ordinal);
        Assert.True(names > content.LastIndexOf("</table:table>", StringComparison.Ordinal));
        Assert.True(names < content.IndexOf("</office:spreadsheet>", StringComparison.Ordinal));
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        Assert.Contains(new OdsReader().Read(stream).Value!.DefinedNames, name => name.Name == "Total");
    }

    [Fact]
    public void A_wide_merge_is_saved_as_repeated_covered_cells()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("title"));
        sheet.SetValue(new CellAddress(5, CellAddress.MaxColumns - 1), CellValue.Text("inside"));
        var region = new CellRange(CellAddress.First, new CellAddress(1999, CellAddress.MaxColumns - 1));
        sheet.Merges.Add(region);

        var clock = Stopwatch.StartNew();
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"Saving took {clock.Elapsed}.");
        Assert.True(stream.Length < 2_000_000, $"The package is {stream.Length} bytes.");
        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!.Sheets[0];
        Assert.Equal(region, Assert.Single(read.Merges.All));
        Assert.Equal("title", read.GetValue(CellAddress.First).AsText());
        Assert.Equal("inside", read.GetValue(new CellAddress(5, CellAddress.MaxColumns - 1)).AsText());
    }
}
