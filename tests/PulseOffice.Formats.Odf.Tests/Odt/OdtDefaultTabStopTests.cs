using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtDefaultTabStopTests
{
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

    [Fact]
    public void LaDistanzaPredefinitaDelleTabulazioniStaNelloStilePredefinitoESiRilegge()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        Assert.Equal(StyleSheet.StandardTabInterval, document.Styles.DefaultTabInterval, 6);
        document.Styles.DefaultTabInterval = 35.46;

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
        {
            var styles = XDocument.Load(zip.GetEntry("styles.xml")!.Open());
            var defaults = styles.Descendants(Style + "default-style").Single(element => (string?)element.Attribute(Style + "family") == "paragraph");
            Assert.Equal("35.46pt", (string?)defaults.Element(Style + "paragraph-properties")?.Attribute(Style + "tab-stop-distance"));
        }

        Assert.Equal(35.46, OdtRoundTripTests.RoundTrip(document).Styles.DefaultTabInterval, 3);
    }
}
