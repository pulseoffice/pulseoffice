using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Metadata;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round174KeywordTests
{
    private static List<string> RoundTrip(params string[] keywords)
    {
        using var stream = new MemoryStream();
        using var package = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook);
        var written = new DocumentProperties();
        written.Keywords.AddRange(keywords);
        var mapper = new OoxmlCorePropertiesMapper();
        mapper.Write(package, written);
        var read = new DocumentProperties();
        mapper.Read(package, read);
        return read.Keywords;
    }

    [Fact]
    public void A_keyword_holding_a_comma_comes_back_whole() =>
        Assert.Equal(["Rossi, Mario", "budget"], RoundTrip("Rossi, Mario", "budget"));

    [Fact]
    public void Plain_keywords_keep_the_comma_list() =>
        Assert.Equal(["sales", "2026"], RoundTrip("sales", "2026"));
}
