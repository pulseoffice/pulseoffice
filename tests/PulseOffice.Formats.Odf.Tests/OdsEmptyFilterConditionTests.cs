using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsEmptyFilterConditionTests
{
    [Fact]
    public void LaCondizioneVuotaEsternoIncludeLeCelleVuote()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Lettera"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("a"));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("b"));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(["a", "b"]), IncludeBlanks = false });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            const string close = "</table:filter-condition>";
            var end = content.IndexOf(close, StringComparison.Ordinal) + close.Length;
            content = content[..end] + "<table:filter-condition table:field-number=\"0\" table:value=\"\" table:operator=\"empty\" table:data-type=\"number\"/>" + content[end..];
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var filter = Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].AutoFilter.Filters);

        Assert.Equal(["a", "b"], filter.AllowedValues!.Order());
        Assert.True(filter.IncludeBlanks);
        Assert.Null(filter.Criteria);
    }
}
