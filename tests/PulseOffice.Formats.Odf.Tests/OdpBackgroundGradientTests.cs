using System.IO.Compression;
using System.Text;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpBackgroundGradientTests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\"";

    private const string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles " + Namespaces + "><office:styles>" +
        "<draw:gradient draw:name=\"Master\" draw:style=\"linear\" draw:start-color=\"#000080\" draw:end-color=\"#ffffff\" draw:angle=\"0\" draw:border=\"0%\"/>" +
        "<draw:gradient draw:name=\"Page\" draw:style=\"axial\" draw:start-color=\"#ff0000\" draw:end-color=\"#00ff00\" draw:angle=\"90deg\" draw:border=\"10%\"/>" +
        "</office:styles><office:automatic-styles>" +
        "<style:style style:name=\"Mdp1\" style:family=\"drawing-page\"><style:drawing-page-properties draw:fill=\"gradient\" draw:fill-gradient-name=\"Master\"/></style:style>" +
        "</office:automatic-styles><office:master-styles><style:master-page style:name=\"Default\" draw:style-name=\"Mdp1\"/></office:master-styles></office:document-styles>";

    private const string Content =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content " + Namespaces + "><office:automatic-styles>" +
        "<style:style style:name=\"dp1\" style:family=\"drawing-page\"><style:drawing-page-properties draw:fill=\"gradient\" draw:fill-gradient-name=\"Page\"/></style:style>" +
        "</office:automatic-styles><office:body><office:presentation>" +
        "<draw:page draw:name=\"p1\" draw:master-page-name=\"Default\" draw:style-name=\"dp1\"/>" +
        "<draw:page draw:name=\"p2\" draw:master-page-name=\"Default\"/>" +
        "</office:presentation></office:body></office:document-content>";

    private Presentation Read(Stream stream)
    {
        var result = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation Read()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.presentation");
            Add(archive, "styles.xml", Styles);
            Add(archive, "content.xml", Content);
        }
        buffer.Position = 0;
        return Read(buffer);
    }

    private void Add(ZipArchive archive, string name, string text)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void LoSfondoAGradienteDellaDiapositivaSiLegge()
    {
        var fill = Read().Slides[0].BackgroundFill;

        Assert.NotNull(fill);
        Assert.Equal(ShapeFillKind.Gradient, fill.Kind);
        Assert.Equal(GradientKind.Axial, fill.Gradient.Kind);
        Assert.Equal("#FF0000", fill.Gradient.StartHex, ignoreCase: true);
        Assert.Equal(0, fill.Gradient.AngleDegrees, 1);
        Assert.Equal(0.1, fill.Gradient.Border, 3);
    }

    [Fact]
    public void LoSfondoAGradienteDelloSchemaSiLeggeConLAngoloDiODF()
    {
        var deck = Read();

        Assert.Null(deck.Slides[1].BackgroundFill);
        Assert.NotNull(deck.Master.BackgroundFill);
        Assert.Equal("#000080", deck.Master.BackgroundFill.Gradient.StartHex, ignoreCase: true);
        Assert.Equal(90, deck.Master.BackgroundFill.Gradient.AngleDegrees, 1);
    }

    [Fact]
    public void GliSfondiAGradienteAttraversanoODP()
    {
        var deck = Read();
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;

        var again = Read(buffer);

        Assert.Equal(deck.Slides[0].BackgroundFill!.Gradient, again.Slides[0].BackgroundFill!.Gradient, new GradientComparer());
        Assert.Equal(deck.Master.BackgroundFill!.Gradient, again.Master.BackgroundFill!.Gradient, new GradientComparer());
        Assert.Null(again.Slides[1].BackgroundFill);
    }

    private sealed class GradientComparer : IEqualityComparer<ShapeGradient>
    {
        public bool Equals(ShapeGradient? x, ShapeGradient? y) =>
            x is not null && y is not null && x.Kind == y.Kind && Math.Abs(x.AngleDegrees - y.AngleDegrees) < 0.1
            && Math.Abs(x.Border - y.Border) < 0.001
            && string.Equals(x.StartHex, y.StartHex, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.EndHex, y.EndHex, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ShapeGradient obj) => obj.Kind.GetHashCode();
    }

    [Fact]
    public void LoSchemaSenzaRiempimentoHaSfondoBiancoAncheConUnColoreDichiarato()
    {
        var styles = Styles.Replace("draw:fill=\"gradient\" draw:fill-gradient-name=\"Master\"", "draw:fill=\"none\" draw:fill-color=\"#127622\"");
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.presentation");
            Add(archive, "styles.xml", styles);
            Add(archive, "content.xml", Content);
        }
        buffer.Position = 0;
        var deck = Read(buffer);

        Assert.Equal(0xFFFFFFu, deck.Master.BackgroundOf(deck.Theme, deck.Slides[1].Layout) & 0xFFFFFFu);
    }

    [Fact]
    public void L_immagine_di_sfondo_dello_schema_senza_ripetizione_dichiarata_si_affianca_e_resta_affiancata()
    {
        var styles = Styles
            .Replace("draw:fill=\"gradient\" draw:fill-gradient-name=\"Master\"", "draw:fill=\"bitmap\" draw:fill-image-name=\"Tile\" draw:fill-image-width=\"0cm\" draw:fill-image-height=\"0cm\"")
            .Replace("</office:styles>", "<draw:fill-image draw:name=\"Tile\" xlink:href=\"Pictures/tile.png\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/></office:styles>");
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.presentation");
            Add(archive, "styles.xml", styles);
            Add(archive, "content.xml", Content);
            using var picture = archive.CreateEntry("Pictures/tile.png").Open();
            picture.Write(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="));
        }
        buffer.Position = 0;
        var deck = Read(buffer);
        Assert.NotNull(deck.Master.BackgroundImage);
        Assert.Equal(BackgroundImageMode.Tile, deck.Master.BackgroundLayout.Mode);

        using var again = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, again).Succeeded);
        again.Position = 0;
        Assert.Equal(BackgroundImageMode.Tile, Read(again).Master.BackgroundLayout.Mode);
    }
}
