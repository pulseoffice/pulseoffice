using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtHeaderFooterHeightTests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    [Fact]
    public void The_minimum_height_of_a_header_or_footer_includes_its_spacing()
    {
        var page = Read(dynamic: false);

        Near(Twip.FromCentimeters(1.191), page.Header.Spacing);
        Near(Twip.FromCentimeters(0.1), page.Header.MinimumHeight);
        Near(Twip.FromCentimeters(0.1), page.Footer.MinimumHeight);
    }

    [Fact]
    public void Dynamic_spacing_keeps_the_body_where_the_minimum_height_ends()
    {
        var page = Read(dynamic: true);

        Assert.Equal(Twip.Zero, page.Header.Spacing);
        Near(Twip.FromCentimeters(1.291), page.Header.MinimumHeight);
        Assert.Equal(Twip.Zero, page.Footer.Spacing);
        Near(Twip.FromCentimeters(1.291), page.Footer.MinimumHeight);
    }

    [Fact]
    public void The_header_height_survives_a_round_trip()
    {
        var page = Read(dynamic: false);
        using var again = new MemoryStream();
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var standard = document.Styles.Page.All.First();
        standard.Header.Spacing = page.Header.Spacing;
        standard.Header.MinimumHeight = page.Header.MinimumHeight;
        Assert.True(new OdtWriter().Write(document, again).Succeeded);
        again.Position = 0;
        var round = new OdtReader().Read(again).Value!.Styles.Page.All.First();
        Near(page.Header.MinimumHeight, round.Header.MinimumHeight);
        Near(page.Header.Spacing, round.Header.Spacing);
    }

    private PageStyle Read(bool dynamic)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(new TextDocument(new DefaultStyleSheetFactory().Create()), stream).Succeeded);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("styles.xml")!;
            XDocument styles;
            using (var input = entry.Open()) styles = XDocument.Load(input);
            foreach (var region in styles.Descendants(Style + "header-style").Concat(styles.Descendants(Style + "footer-style")))
                region.ReplaceNodes(new XElement(Style + "header-footer-properties",
                    new XAttribute(Fo + "min-height", "1.291cm"), new XAttribute(Fo + "margin-bottom", "1.191cm"), new XAttribute(Fo + "margin-top", "1.191cm"), new XAttribute(Style + "dynamic-spacing", dynamic ? "true" : "false")));
            entry.Delete();
            using var output = zip.CreateEntry("styles.xml").Open();
            styles.Save(output);
        }
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!.Styles.Page.All.First();
    }

    private void Near(Twip expected, Twip actual) => Assert.InRange(actual.Value, expected.Value - 1, expected.Value + 1);
}
