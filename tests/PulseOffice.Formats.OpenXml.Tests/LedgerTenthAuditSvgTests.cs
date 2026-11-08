using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Rasterizing;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LedgerTenthAuditSvgTests
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"10\" fill=\"#16A34A\"/></svg>";

    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private readonly VectorFallback _vectors = new(new SkiaVectorRasterizer());

    private Workbook Workbook()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "logo",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3)),
            Content = new VectorGraphicObject
            {
                Id = "logo",
                Asset = new VectorAsset { Id = "logo", Name = "logo", SourceKind = VectorSourceKind.InlineSvg, SvgContent = Svg },
                RotationDegrees = 30,
                FlipHorizontal = true,
            },
        });
        return workbook;
    }

    private byte[] Xlsx()
    {
        using var stream = new MemoryStream();
        new XlsxWriter(new SignatureLineImage(), _vectors).Write(Workbook(), stream);
        return stream.ToArray();
    }

    private byte[] Ods()
    {
        using var stream = new MemoryStream();
        new OdsWriter(false, new SignatureLineImage(), _vectors).Write(Workbook(), stream);
        return stream.ToArray();
    }

    [Fact]
    public void XlsxScriveIlVettoreComeSvgBlipConUnPngDiRiserva()
    {
        using var archive = new ZipArchive(new MemoryStream(Xlsx()));
        var drawing = new StreamReader(archive.Entries.Single(entry => entry.FullName.StartsWith("xl/drawings/drawing", StringComparison.Ordinal)).Open()).ReadToEnd();

        Assert.Contains("svgBlip", drawing);
        Assert.DoesNotContain("custGeom", drawing);
        var media = archive.Entries.Where(entry => entry.FullName.StartsWith("xl/media/", StringComparison.Ordinal)).ToList();
        Assert.Contains(media, entry => entry.FullName.EndsWith(".svg", StringComparison.Ordinal));
        var png = Assert.Single(media, entry => entry.FullName.EndsWith(".png", StringComparison.Ordinal));
        Assert.True(png.Length > 100);
    }

    [Fact]
    public void XlsxRileggePreferendoLSvgConRotazioneERibaltamento()
    {
        var read = new XlsxReader().Read(new MemoryStream(Xlsx()));

        var vector = Assert.IsType<VectorGraphicObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Contains("<circle", vector.Asset.SvgContent);
        Assert.Equal(30, vector.RotationDegrees, 1);
        Assert.True(vector.FlipHorizontal);
    }

    [Fact]
    public void OdsScriveSvgEPngNellaStessaCornice()
    {
        using var archive = new ZipArchive(new MemoryStream(Ods()));
        var content = XDocument.Load(archive.GetEntry("content.xml")!.Open());

        var images = content.Descendants(_draw + "frame").Single().Elements(_draw + "image").ToList();
        Assert.Equal(2, images.Count);
        Assert.Equal("image/svg+xml", (string?)images[0].Attribute(_draw + "mime-type"));
        Assert.Equal("image/png", (string?)images[1].Attribute(_draw + "mime-type"));
        Assert.NotNull(archive.GetEntry((string)images[1].Attribute(_xlink + "href")!));
    }

    [Fact]
    public void OdsRileggeLSvgAncheSeIlPngVienePrima()
    {
        using var edited = new MemoryStream();
        edited.Write(Ods());
        using (var archive = new ZipArchive(edited, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.GetEntry("content.xml")!;
            XDocument content;
            using (var stream = entry.Open()) content = XDocument.Load(stream);
            var images = content.Descendants(_draw + "frame").Single().Elements(_draw + "image").ToList();
            images[0].Remove();
            images[1].AddAfterSelf(images[0]);
            entry.Delete();
            using var target = archive.CreateEntry("content.xml").Open();
            content.Save(target);
        }

        edited.Position = 0;
        var read = new OdsSpreadsheetReader().Read(edited);

        var vector = Assert.IsType<VectorGraphicObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Contains("<circle", vector.Asset.SvgContent);
    }
}
