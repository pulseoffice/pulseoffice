using System.IO.Compression;
using System.Text.RegularExpressions;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class CorePropertiesNanosecondTests
{
    [Fact]
    public void Dates_with_nanosecond_fractions_do_not_make_the_file_unreadable()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Properties.Title = "Accounts";
        workbook.Properties.Created = new DateTimeOffset(2020, 3, 1, 15, 5, 0, TimeSpan.Zero);
        workbook.Properties.Modified = new DateTimeOffset(2020, 7, 27, 10, 37, 2, TimeSpan.Zero);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.Entries.Single(candidate => candidate.FullName.EndsWith(".psmdcp", StringComparison.Ordinal) || candidate.FullName == "docProps/core.xml");
            var name = entry.FullName;
            string content;
            using (var reader = new StreamReader(entry.Open())) content = reader.ReadToEnd();
            content = Regex.Replace(content, "2020-07-27T10:37:02[^<]*", "2020-07-27T10:37:02.406879919");
            entry.Delete();
            using var writer = new StreamWriter(archive.CreateEntry(name).Open());
            writer.Write(content);
        }

        stream.Position = 0;
        var result = new XlsxReader().Read(stream);

        Assert.True(result.Succeeded, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var properties = result.Value!.Properties;
        Assert.Equal("Accounts", properties.Title);
        Assert.Equal(new DateTimeOffset(2020, 3, 1, 15, 5, 0, TimeSpan.Zero), properties.Created);
        Assert.Equal(new DateTimeOffset(2020, 7, 27, 10, 37, 2, TimeSpan.Zero).AddTicks(4068799), properties.Modified);
    }
}
