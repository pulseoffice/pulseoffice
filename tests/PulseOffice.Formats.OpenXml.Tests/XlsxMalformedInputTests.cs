using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxMalformedInputTests
{
    private static readonly string[] Garbage = ["", "abc", "-99999999999999999999", "1e309", "-5"];

    private CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private byte[] RichPackage()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(A("A1"), CellValue.Text("Testo"));
        sheet.SetValue(A("B2"), CellValue.Number(42));
        sheet.Merges.Add(new CellRange(A("C3"), A("D4")));
        sheet.Comments[A("A1")] = new CellComment { Text = "Nota", Author = "Ada" };

        using var buffer = new MemoryStream();
        new XlsxWriter().Write(workbook, buffer);
        return buffer.ToArray();
    }

    private byte[] Replace(byte[] package, string entryName, XDocument document)
    {
        using var source = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var entry in source.Entries)
            {
                using var to = target.CreateEntry(entry.FullName, CompressionLevel.NoCompression).Open();
                if (entry.FullName == entryName) document.Save(to);
                else
                {
                    using var from = entry.Open();
                    from.CopyTo(to);
                }
            }

        return output.ToArray();
    }

    private XDocument Entry(byte[] package, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        using var stream = (archive.GetEntry(entryName) ?? throw new InvalidOperationException(string.Join(",", archive.Entries.Select(e => e.FullName)))).Open();
        return XDocument.Load(stream);
    }

    private string? Failure(byte[] package)
    {
        using var stream = new MemoryStream(package);
        try
        {
            var result = new XlsxReader().Read(stream);
            return result.Succeeded ? null : string.Join("; ", result.Diagnostics.Select(d => d.Message));
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name} {ex.Message}";
        }
    }

    [Theory]
    [InlineData("xl/worksheets/sheet1.xml")]
    [InlineData("xl/workbook.xml")]
    [InlineData("xl/styles.xml")]
    public void GarbageAttributesNeverRejectTheWorkbook(string entryName)
    {
        var package = RichPackage();
        var original = Entry(package, entryName);
        var count = original.Descendants().SelectMany(e => e.Attributes()).Count(a => !a.IsNamespaceDeclaration);
        var failures = new List<string>();

        for (var index = 0; index < count; index++)
            foreach (var value in Garbage)
            {
                var document = new XDocument(original);
                var attribute = document.Descendants().SelectMany(e => e.Attributes()).Where(a => !a.IsNamespaceDeclaration).ElementAt(index);
                if (attribute.Name.LocalName == "id" && attribute.Name.Namespace != XNamespace.None) continue;
                attribute.Value = value;
                if (Failure(Replace(package, entryName, document)) is { } failure) failures.Add($"{attribute.Name}={value}: {failure}");
            }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Distinct()));
    }

    [Theory]
    [InlineData("xl/worksheets/sheet1.xml")]
    [InlineData("xl/styles.xml")]
    public void MissingElementsNeverRejectTheWorkbook(string entryName)
    {
        var package = RichPackage();
        var original = Entry(package, entryName);
        var count = original.Descendants().Count();
        var failures = new List<string>();

        for (var index = 1; index < count; index++)
        {
            var document = new XDocument(original);
            var element = document.Descendants().ElementAt(index);
            var name = element.Name;
            element.Remove();
            if (Failure(Replace(package, entryName, document)) is { } failure) failures.Add($"{name}: {failure}");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Distinct()));
    }
}
