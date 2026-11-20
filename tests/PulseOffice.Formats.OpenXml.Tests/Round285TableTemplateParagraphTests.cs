using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round285TableTemplateParagraphTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    private static readonly TableProperties Regions = new()
    {
        HeaderBackground = 0xFF101010,
        TotalRow = new TableRegionFormat { Italic = true, Font = "Liberation Serif" },
    };

    private static MemoryStream Written()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Table.Add(new TableStyle("Regions") { Table = Regions });
        var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Template_parts_name_paragraph_styles_that_do_not_come_back_as_user_styles()
    {
        using var stream = Written();
        XDocument styles;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var reading = archive.GetEntry("styles.xml")!.Open())
            styles = XDocument.Load(reading);
        var part = styles.Descendants(Table + "table-template").Single(template => ((string?)template.Attribute(Table + "name"))?.Contains("Regions", StringComparison.Ordinal) == true).Element(Table + "last-row")!;
        var paragraphName = (string?)part.Attribute(Table + "paragraph-style-name");
        Assert.NotNull(paragraphName);
        Assert.Contains(styles.Descendants(Style + "style"),
            style => (string?)style.Attribute(Style + "name") == paragraphName && (string?)style.Attribute(Style + "family") == "paragraph");
        stream.Position = 0;

        var read = new OdtReader().Read(stream).Value!;

        Assert.Null(read.Styles.Paragraph.Find(paragraphName));
        Assert.Equal(Regions.TotalRow, read.Styles.Table.Find("Regions")!.Table.TotalRow);
    }

    [Fact]
    public void A_template_whose_text_lives_only_in_its_paragraph_style_is_read()
    {
        using var written = Written();
        var copy = new MemoryStream();
        written.CopyTo(copy);
        using (var archive = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("styles.xml")!;
            XDocument styles;
            using (var reading = entry.Open()) styles = XDocument.Load(reading);
            styles.Descendants(Style + "style")
                .Where(element => (string?)element.Attribute(Style + "family") == "table-cell")
                .Elements(Style + "text-properties").Remove();
            styles.Descendants(Style + "style")
                .Where(element => (string?)element.Attribute(Style + "name") == "Regions" && (string?)element.Attribute(Style + "family") == "table")
                .Remove();
            entry.Delete();
            using var writing = archive.CreateEntry("styles.xml").Open();
            styles.Save(writing);
        }
        copy.Position = 0;

        var read = new OdtReader().Read(copy).Value!.Styles.Table.Find("Regions")!.Table;

        Assert.Equal(Regions.TotalRow, read.TotalRow);
    }
}
