using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtFieldLanguageTests
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

    [Theory]
    [InlineData("en-US", "it-IT", null, "1.234,5")]
    [InlineData("it-IT", "en-US", null, "1,234.5")]
    [InlineData("en-US", "en-US", "it-IT", "1.234,5")]
    [InlineData("it-IT", "it-IT", "en-US", "1,234.5")]
    public void ATableCellValueIsReadInTheLanguageOfItsText(string system, string document, string? run, string text)
    {
        var cell = new TableCell(new BlockContainer([new ParagraphBlock("c1", new ParagraphBlock("c1", text).Content.WithRuns([new TextRun(text) { Language = run }]))]))
        {
            NumberFormat = new CellNumberFormat(CellValueKind.Number, 1, true),
        };
        var model = new PulseOffice.Documents.TextDocument { Language = document };
        model.Body.Replace([new TableBlock("t1", [new TableRow([cell])], [PulseOffice.Foundation.Units.Twip.FromCentimeters(5)])]);

        var value = WithCulture(system, () => Value(model));

        Assert.Equal("1234.5", value);
    }

    private string? Value(PulseOffice.Documents.TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var content = archive.GetEntry("content.xml")!.Open();
        return (string?)XDocument.Load(content).Descendants(Table + "table-cell").Single().Attribute(Office + "value");
    }

    private T WithCulture<T>(string name, Func<T> action)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
