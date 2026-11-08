using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Document.Forms;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Formats.Rasterizing;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using SkiaSharp;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SignatureLineRasterTests
{
    private readonly XNamespace _vml = "urn:schemas-microsoft-com:vml";
    private readonly XNamespace _office = "urn:schemas-microsoft-com:office:office";
    private readonly XNamespace _relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private readonly XNamespace _package = "http://schemas.openxmlformats.org/package/2006/relationships";
    private readonly XNamespace _draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private readonly XNamespace _xlink = "http://www.w3.org/1999/xlink";
    private readonly SignatureLineImage _images = new(new SkiaVectorRasterizer());

    private SignatureLineObject Line() => new() { Id = "firma", SignerName = "Ada Lovelace", SignerTitle = "Contessa" };

    private TextDocument Document()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "Firma: ", DefaultStyleNames.Standard) { Marks = [new ObjectMark(7, "o1")] });
        document.Objects.Add(new AnchoredObject("o1", new AsCharacterAnchor("p1"), new EmbeddedContent(Line()), SignatureLineObject.StandardSize));
        return document;
    }

    private Workbook Workbook()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Contratto").Objects.Add(new PlacedObject
        {
            Id = "firma", Content = Line(), Anchor = new CellAddress(2, 1), Size = SignatureLineObject.StandardSize,
        });
        return workbook;
    }

    private byte[] Written(Action<Stream> write)
    {
        using var stream = new MemoryStream();
        write(stream);
        return stream.ToArray();
    }

    private byte[] Bytes(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        using var stream = archive.GetEntry(name)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private XDocument Xml(byte[] package, string name) => XDocument.Load(new MemoryStream(Bytes(package, name)));

    private IReadOnlyList<string> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private string Resolve(string folder, string target) =>
        target.StartsWith('/') ? target[1..] : Path.GetFullPath(Path.Combine("/" + folder, target))[1..].Replace('\\', '/');

    private void AssertDrawnPng(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        var expectedWidth = (int)Math.Ceiling(SignatureLineObject.StandardSize.Width.ToPoints() * 96 / 72 * 2);
        Assert.Equal(expectedWidth, bitmap.Width);
        var inked = 0;
        for (var y = 0; y < bitmap.Height; y += 2)
            for (var x = 0; x < bitmap.Width; x += 2)
                if (bitmap.GetPixel(x, y) is { Alpha: > 128 } pixel && pixel.Red < 100) inked++;
        Assert.True(inked > 50, $"only {inked} inked pixels");
    }

    private string ImageTargetOf(byte[] package, string part, string relationshipId)
    {
        var folder = Path.GetDirectoryName(part)!.Replace('\\', '/');
        var rels = Xml(package, $"{folder}/_rels/{Path.GetFileName(part)}.rels");
        var target = (string)rels.Descendants(_package + "Relationship").Single(r => (string?)r.Attribute("Id") == relationshipId).Attribute("Target")!;
        return Resolve(folder, target);
    }

    [Fact]
    public void In_DOCX_il_VML_della_riga_di_firma_mostra_un_PNG_disegnato_e_tiene_anche_lo_SVG()
    {
        var composer = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory(), _images);
        var package = Written(stream => composer.Writer(DocxFlavour.Document).Write(Document(), stream));

        var imagedata = Xml(package, "word/document.xml").Descendants(_vml + "imagedata").Single();
        var target = ImageTargetOf(package, "word/document.xml", (string)imagedata.Attribute(_relationships + "id")!);
        Assert.EndsWith(".png", target, StringComparison.Ordinal);
        AssertDrawnPng(Bytes(package, target));
        Assert.Contains(Entries(package), entry => entry.EndsWith(".svg", StringComparison.Ordinal));
    }

    [Fact]
    public void In_XLSX_il_VML_della_riga_di_firma_mostra_un_PNG_disegnato_e_tiene_anche_lo_SVG()
    {
        var package = Written(stream => new XlsxSpreadsheetWriter(_images).Write(Workbook(), stream));

        var drawing = Entries(package).Single(entry => entry.StartsWith("xl/drawings/", StringComparison.Ordinal) && entry.EndsWith(".vml", StringComparison.Ordinal));
        var imagedata = Xml(package, drawing).Descendants(_vml + "imagedata").Single();
        var target = ImageTargetOf(package, drawing, (string)imagedata.Attribute(_office + "relid")!);
        Assert.EndsWith(".png", target, StringComparison.Ordinal);
        AssertDrawnPng(Bytes(package, target));
        Assert.Contains(Entries(package), entry => entry.EndsWith(".svg", StringComparison.Ordinal));
    }

    [Fact]
    public void In_ODT_e_ODS_la_cornice_della_firma_ha_lo_SVG_e_il_PNG_di_ripiego()
    {
        var odt = Written(stream => new OdtWriter(null, _images).Write(Document(), stream));
        var ods = Written(stream => new OdsSpreadsheetWriter(false, _images).Write(Workbook(), stream));

        foreach (var package in new[] { odt, ods })
        {
            var images = Xml(package, "content.xml").Descendants(_draw + "frame")
                .Single(frame => frame.Elements().Any(element => element.Name.LocalName == "signatureline"))
                .Elements(_draw + "image").ToList();
            Assert.Equal(["image/svg+xml", "image/png"], images.Select(image => (string?)image.Attribute(_draw + "mime-type")));
            AssertDrawnPng(Bytes(package, (string)images[1].Attribute(_xlink + "href")!));
        }
    }

    [Fact]
    public void Senza_rasterizzatore_resta_il_solo_SVG()
    {
        var package = Written(stream => new XlsxSpreadsheetWriter().Write(Workbook(), stream));

        Assert.DoesNotContain(Entries(package), entry => entry.EndsWith(".png", StringComparison.Ordinal));
        Assert.Contains(Entries(package), entry => entry.EndsWith(".svg", StringComparison.Ordinal));
    }
}
