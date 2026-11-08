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
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SignatureLineOoxmlTests
{
    private readonly XNamespace _vml = "urn:schemas-microsoft-com:vml";
    private readonly XNamespace _office = "urn:schemas-microsoft-com:office:office";
    private readonly XNamespace _excel = "urn:schemas-microsoft-com:office:excel";
    private readonly XNamespace _relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private SignatureLineObject Line(string id, bool signed = false) => new()
    {
        Id = id, SignerName = "Ada Lovelace", SignerTitle = "Contessa", SignerEmail = "ada@example.org",
        Instructions = "Firmare qui", ShowDate = false, AllowComments = true,
        SignedBy = signed ? "Ada" : null,
    };

    private TextDocument Document(ObjectAnchor anchor, SignatureLineObject line)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Replace(new PulseOffice.Documents.Styles.PageStyle(DefaultStyleNames.Standard));
        var paragraph = new ParagraphBlock("p1", "Firma: ", DefaultStyleNames.Standard) { Marks = [new ObjectMark(7, "o1")] };
        document.Body.Add(paragraph);
        var size = new DocumentSize(Twip.FromCentimeters(6), Twip.FromCentimeters(2));
        document.Objects.Add(new AnchoredObject("o1", anchor, new EmbeddedContent(line), size)
        {
            Placement = new ObjectPlacement(Twip.FromCentimeters(2), Twip.FromCentimeters(1)) { Horizontal = HorizontalRelation.PageArea, Vertical = VerticalRelation.Paragraph },
        });
        return document;
    }

    private byte[] Docx(TextDocument document)
    {
        using var stream = new MemoryStream();
        var written = _composer.Writer(DocxFlavour.Document).Write(document, stream);
        Assert.True(written.Succeeded, string.Join("; ", written.Diagnostics.Select(d => d.Message)));
        return stream.ToArray();
    }

    private TextDocument ReadDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private XDocument Entry(byte[] package, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        using var stream = archive.GetEntry(name)!.Open();
        return XDocument.Load(stream);
    }

    private IReadOnlyList<string> Entries(byte[] package)
    {
        using var archive = new ZipArchive(new MemoryStream(package));
        return [.. archive.Entries.Select(entry => entry.FullName)];
    }

    private void AssertLine(SignatureLineObject line)
    {
        Assert.Equal("Ada Lovelace", line.SignerName);
        Assert.Equal("Contessa", line.SignerTitle);
        Assert.Equal("ada@example.org", line.SignerEmail);
        Assert.Equal("Firmare qui", line.Instructions);
        Assert.False(line.ShowDate);
        Assert.True(line.AllowComments);
    }

    [Fact]
    public void InDocxLaRigaDellaFirmaEUnVmlConSignaturelineEImmagine()
    {
        var bytes = Docx(Document(new AsCharacterAnchor("p1"), Line("o1")));

        var body = Entry(bytes, "word/document.xml");
        var shape = Assert.Single(body.Descendants(_vml + "shape"));
        var signature = shape.Element(_office + "signatureline")!;
        Assert.Equal("t", (string?)signature.Attribute("issignatureline"));
        Assert.Equal("Ada Lovelace", (string?)signature.Attribute(_office + "suggestedsigner"));
        Assert.Equal("Contessa", (string?)signature.Attribute(_office + "suggestedsigner2"));
        Assert.Equal("ada@example.org", (string?)signature.Attribute(_office + "suggestedsigneremail"));
        Assert.Equal("Firmare qui", (string?)signature.Attribute("signinginstructions"));
        Assert.Equal("f", (string?)signature.Attribute("showsigndate"));
        Assert.Equal("t", (string?)signature.Attribute("allowcomments"));
        Assert.True(Guid.TryParse((string?)signature.Attribute("id"), out _));
        Assert.NotNull(signature.Attribute("provid"));
        Assert.NotNull(shape.Element(_vml + "imagedata")!.Attribute(_relationships + "id"));
        Assert.Contains(Entries(bytes), name => name.EndsWith(".svg", StringComparison.Ordinal));
    }

    [Fact]
    public void LaRigaDellaFirmaInLineaTornaDalDocx()
    {
        var document = Document(new AsCharacterAnchor("p1"), Line("o1"));
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code is "DOCX002" or "DOCX011" or "DOCX042");

        var read = ReadDocx(Docx(document));

        var placed = Assert.Single(read.Objects.All);
        Assert.IsType<AsCharacterAnchor>(placed.Anchor);
        AssertLine(Assert.IsType<SignatureLineObject>(Assert.IsType<EmbeddedContent>(placed.Content).Value));
        Assert.InRange(placed.Size.Width.Value - Twip.FromCentimeters(6).Value, -2, 2);
        Assert.InRange(placed.Size.Height.Value - Twip.FromCentimeters(2).Value, -2, 2);
        Assert.Contains(((ParagraphBlock)read.Body[0]).Marks, mark => mark is ObjectMark { ObjectId: var id } && id == placed.Id);
    }

    [Fact]
    public void LaRigaDellaFirmaFluttuanteTieneLaPosizioneNelDocx()
    {
        var document = Document(new CharacterAnchor("p1"), Line("o1"));
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code is "DOCX002" or "DOCX011");

        var read = ReadDocx(Docx(document));

        var placed = Assert.Single(read.Objects.All);
        Assert.IsType<CharacterAnchor>(placed.Anchor);
        Assert.InRange(placed.Placement.X.Value - Twip.FromCentimeters(2).Value, -2, 2);
        Assert.InRange(placed.Placement.Y.Value - Twip.FromCentimeters(1).Value, -2, 2);
        Assert.Equal(HorizontalRelation.PageArea, placed.Placement.Horizontal);
        AssertLine(Assert.IsType<SignatureLineObject>(Assert.IsType<EmbeddedContent>(placed.Content).Value));
    }

    [Fact]
    public void LaRigaGiaFirmataDichiaraCheIlFirmatarioNonPassaInDocx()
    {
        var document = Document(new AsCharacterAnchor("p1"), Line("o1", signed: true));
        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX042");
    }

    [Fact]
    public void LaRigaDellaFirmaVaDaOdtADocxEIndietro()
    {
        var document = Document(new CharacterAnchor("p1"), Line("o1"));
        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, odt).Succeeded);
        odt.Position = 0;
        var fromOdt = new OdtReader().Read(odt).Value!;

        var throughDocx = ReadDocx(Docx(fromOdt));
        using var back = new MemoryStream();
        Assert.True(new OdtWriter().Write(throughDocx, back).Succeeded);
        back.Position = 0;
        var final = new OdtReader().Read(back).Value!;

        var placed = Assert.Single(final.Objects.All);
        AssertLine(Assert.IsType<SignatureLineObject>(Assert.IsType<EmbeddedContent>(placed.Content).Value));
        Assert.InRange(placed.Size.Width.Value - Twip.FromCentimeters(6).Value, -2, 2);
    }

    [Fact]
    public void LaRigaDellaFirmaDiUnDocxScrittoDaUnAltraSuiteRientraNelModello()
    {
        var read = ReadDocx(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Objects", "signature-line.lo.docx")));

        var placed = Assert.Single(read.Objects.All, item => item.Content is EmbeddedContent { Value: SignatureLineObject });
        AssertLine((SignatureLineObject)((EmbeddedContent)placed.Content).Value);
        Assert.IsType<CharacterAnchor>(placed.Anchor);
        Assert.InRange(placed.Size.Width.Value - Twip.FromCentimeters(6).Value, -20, 20);
        Assert.Equal(HorizontalRelation.PageArea, placed.Placement.Horizontal);
    }

    private Workbook Workbook(bool signed = false, bool comment = false)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Contratto");
        sheet.Objects.Add(new PlacedObject
        {
            Id = "firma",
            Content = Line("firma", signed),
            Anchor = new CellAddress(4, 2),
            OffsetX = Twip.FromPoints(6),
            OffsetY = Twip.FromPoints(3),
            Size = SignatureLineObject.StandardSize,
        });
        if (comment) sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "Nota", Author = "Ada" };
        return workbook;
    }

    private byte[] Xlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        return stream.ToArray();
    }

    private Workbook ReadXlsx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void InXlsxLaRigaDellaFirmaStaNelDisegnoVmlDelFoglio()
    {
        var bytes = Xlsx(Workbook());

        var vmlName = Assert.Single(Entries(bytes), name => name.EndsWith(".vml", StringComparison.Ordinal));
        var vml = Entry(bytes, vmlName);
        var shape = Assert.Single(vml.Descendants(_vml + "shape"));
        Assert.Equal("t", (string?)shape.Element(_office + "signatureline")!.Attribute("issignatureline"));
        Assert.NotNull(shape.Element(_vml + "imagedata")!.Attribute(_office + "relid"));
        var data = shape.Element(_excel + "ClientData")!;
        Assert.Equal("Pict", (string?)data.Attribute("ObjectType"));
        Assert.StartsWith("2, 8, 4, 4, ", (string?)data.Element(_excel + "Anchor"), StringComparison.Ordinal);
        Assert.Contains(Entries(bytes), name => name.EndsWith(".svg", StringComparison.Ordinal));
        Assert.Contains("legacyDrawing", Entry(bytes, "xl/worksheets/sheet1.xml").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void LaRigaDellaFirmaTornaDallXlsx()
    {
        var workbook = Workbook();
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX016");

        var read = ReadXlsx(Xlsx(workbook));

        var placed = Assert.Single(read.Sheets[0].Objects);
        AssertLine(Assert.IsType<SignatureLineObject>(placed.Content));
        Assert.Equal(new CellAddress(4, 2), placed.Anchor);
        Assert.InRange(placed.OffsetX.Value - Twip.FromPoints(6).Value, -2, 2);
        Assert.InRange(placed.OffsetY.Value - Twip.FromPoints(3).Value, -2, 2);
        Assert.Equal(7, placed.Size.Width.ToCentimeters(), 1);
        Assert.Equal(2.4, placed.Size.Height.ToCentimeters(), 1);
    }

    [Fact]
    public void LaRigaDellaFirmaEINoteDividonoLoStessoDisegnoVml()
    {
        var bytes = Xlsx(Workbook(comment: true));

        Assert.Single(Entries(bytes), name => name.EndsWith(".vml", StringComparison.Ordinal));
        var read = ReadXlsx(bytes);
        Assert.Single(read.Sheets[0].Objects, placed => placed.Content is SignatureLineObject);
        Assert.Equal("Nota", read.Sheets[0].Comments[new CellAddress(0, 0)].Text);
    }

    [Fact]
    public void LaRigaGiaFirmataDichiaraCheIlFirmatarioNonPassaInXlsx() =>
        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(Workbook(signed: true)).Losses, loss => loss.Code == "XLSX016");

    [Fact]
    public void LaRigaDellaFirmaVaDaOdsAXlsxEIndietro()
    {
        using var ods = new MemoryStream();
        new OdsWriter().Write(Workbook(), ods);
        ods.Position = 0;
        var fromOds = new OdsReader().Read(ods).Value!;

        var throughXlsx = ReadXlsx(Xlsx(fromOds));
        using var back = new MemoryStream();
        new OdsWriter().Write(throughXlsx, back);
        back.Position = 0;
        var final = new OdsReader().Read(back).Value!;

        var placed = Assert.Single(final.Sheets[0].Objects);
        AssertLine(Assert.IsType<SignatureLineObject>(placed.Content));
        Assert.Equal(new CellAddress(4, 2), placed.Anchor);
    }
}
