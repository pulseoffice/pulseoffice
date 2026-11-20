using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round279TableRegionTextStyleTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static readonly BorderLine Rule = new(1, 0xFF336699);

    private static readonly TableProperties Regions = new()
    {
        HeaderBackground = 0xFF101010,
        HeaderBold = true,
        BandBackground = 0xFFEEEEEE,
        HeaderRow = new TableRegionFormat { Italic = true, Font = "Liberation Serif", Borders = new BoxBorders { Bottom = Rule } },
        SecondBand = new TableRegionFormat { FontSize = 9 },
        TotalRow = new TableRegionFormat { Bold = false, Italic = false, FontSize = 11, Borders = new BoxBorders { Top = Rule } },
        FirstColumn = new TableRegionFormat { Font = "Liberation Mono" },
    };

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Table.Add(new TableStyle("Regions") { Table = Regions });
        return document;
    }

    private MemoryStream Written(ITextDocumentWriter writer)
    {
        var stream = new MemoryStream();
        Assert.True(writer.Write(Document(), stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    private void AssertRegions(TableProperties read)
    {
        Assert.Equal(Regions.HeaderRow, read.HeaderRow);
        Assert.Equal(Regions.SecondBand, read.SecondBand);
        Assert.Equal(Regions.TotalRow, read.TotalRow);
        Assert.Equal(Regions.FirstColumn, read.FirstColumn);
        Assert.Equal(Regions.HeaderBackground, read.HeaderBackground);
        Assert.Equal(Regions.BandBackground, read.BandBackground);
    }

    [Fact]
    public void Italics_fonts_sizes_and_lines_of_regions_cross_docx()
    {
        using var stream = Written(_composer.Writer(DocxFlavour.Document));
        AssertRegions(_composer.Reader(DocxFlavour.Document).Read(stream).Value!.Styles.Table.Find("Regions")!.Table);
    }

    [Fact]
    public void Italics_fonts_sizes_and_lines_of_regions_cross_odt()
    {
        using var stream = Written(new OdtWriter());
        AssertRegions(new OdtReader().Read(stream).Value!.Styles.Table.Find("Regions")!.Table);
    }

    [Fact]
    public void Odt_writes_a_native_table_template_that_is_read_without_our_extension()
    {
        using var written = Written(new OdtWriter());
        var copy = new MemoryStream();
        written.CopyTo(copy);
        using (var archive = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            XDocument styles;
            using (var reading = entry.Open()) styles = XDocument.Load(reading);
            var template = styles.Descendants(Table + "table-template").Single(element => (string?)element.Attribute(Table + "name") == "Regions");
            Assert.NotNull(template.Element(Table + "first-row"));
            Assert.NotNull(template.Element(Table + "body"));
            styles.Descendants(Style + "style")
                .Where(element => (string?)element.Attribute(Style + "name") == "Regions" && (string?)element.Attribute(Style + "family") == "table")
                .Remove();
            entry.Delete();
            using var writing = archive.CreateEntry("styles.xml").Open();
            styles.Save(writing);
        }
        copy.Position = 0;

        var read = new OdtReader().Read(copy).Value!.Styles.Table.Find("Regions")!.Table;

        Assert.Equal(Regions.HeaderBackground, read.HeaderBackground);
        Assert.True(read.HeaderBold);
        Assert.Equal(Regions.HeaderRow, read.HeaderRow);
        Assert.Equal(Regions.TotalRow, read.TotalRow);
        Assert.Equal(Regions.BandBackground, read.BandBackground);
    }
}
