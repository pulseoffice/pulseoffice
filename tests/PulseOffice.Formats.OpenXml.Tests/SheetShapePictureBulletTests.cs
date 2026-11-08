using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SheetShapePictureBulletTests
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Un_elenco_a_immagine_in_una_forma_XLSX_si_legge_e_attraversa_ODS(bool resolvable)
    {
        var read = new XlsxReader().Read(new MemoryStream(Bulleted(resolvable)));

        Assert.True(read.Succeeded);
        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Uno\nDue", shape.Text);
        Assert.Equal(2, shape.Lists!.Count);
        Assert.All(shape.Lists, item => Assert.False(item!.Numbered));
        if (resolvable) Assert.Equal(Png, shape.Lists[0]!.Symbol!.Picture!.Data.ToArray());
        else Assert.Null(shape.Lists[0]!.Symbol?.Picture);

        using var ods = new MemoryStream();
        new OdsWriter().Write(read.Value!, ods);
        var fromOds = new OdsReader().Read(new MemoryStream(ods.ToArray()));
        var odsShape = Assert.IsType<ShapeObject>(Assert.Single(fromOds.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Uno\nDue", odsShape.Text);
        Assert.Equal(2, odsShape.Lists!.Count);
        if (resolvable) Assert.Equal(Png, odsShape.Lists[1]!.Symbol!.Picture!.Data.ToArray());

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(fromOds.Value!, xlsx);
        var again = new XlsxReader().Read(new MemoryStream(xlsx.ToArray()));
        var back = Assert.IsType<ShapeObject>(Assert.Single(again.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Uno\nDue", back.Text);
        Assert.Equal(2, back.Lists!.Count);
        if (resolvable) Assert.Equal(Png, back.Lists[0]!.Symbol!.Picture!.Data.ToArray());
    }

    [Fact]
    public void Punti_numeri_e_livelli_nel_testo_di_una_forma_attraversano_XLSX_e_ODS()
    {
        var workbook = Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Titolo\nPrimo\nSecondo\nDentro\nFine",
            Lists =
            [
                null,
                new ShapeListItem { Numbered = true },
                new ShapeListItem { Numbered = true },
                new ShapeListItem { Level = 1, Symbol = new PulseOffice.Text.Rich.BulletSymbol("–") { Font = "Arial" } },
            ],
        });

        var xlsx = RoundTripXlsx(workbook);
        var ods = RoundTripOds(workbook);
        var crossed = RoundTripXlsx(Workbook(RoundTripOds(Workbook(RoundTripXlsx(workbook)))));

        foreach (var shape in new[] { xlsx, ods, crossed })
        {
            Assert.Equal("Titolo\nPrimo\nSecondo\nDentro\nFine", shape.Text);
            Assert.Equal(4, shape.Lists!.Count);
            Assert.Null(shape.Lists[0]);
            Assert.True(shape.Lists[1]!.Numbered);
            Assert.True(shape.Lists[2]!.Numbered);
            Assert.False(shape.Lists[3]!.Numbered);
            Assert.Equal(1, shape.Lists[3]!.Level);
            Assert.Equal("–", shape.Lists[3]!.Symbol!.Character);
        }
    }

    [Fact]
    public void Il_testo_XLSX_di_una_forma_con_elenco_scrive_i_marcatori_DrawingML()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue",
            Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Level = 2 }],
        }), stream);

        using var document = SpreadsheetDocument.Open(new MemoryStream(stream.ToArray()), false);
        var paragraphs = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!.Descendants<A.Paragraph>().ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.NotNull(paragraphs[0].ParagraphProperties!.GetFirstChild<A.AutoNumberedBullet>());
        Assert.Equal(2, paragraphs[1].ParagraphProperties!.Level!.Value);
        Assert.Equal("•", paragraphs[1].ParagraphProperties!.GetFirstChild<A.CharacterBullet>()!.Char!.Value);
    }

    [Fact]
    public void Una_forma_ODS_con_elenco_a_immagine_mancante_tiene_il_punto()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:custom-shape draw:name=\"Forma\" svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\">"
            + "<text:list text:style-name=\"L1\"><text:list-item><text:p>Uno</text:p></text:list-item><text:list-item><text:p>Due</text:p></text:list-item></text:list>"
            + "<draw:enhanced-geometry draw:type=\"rectangle\"/></draw:custom-shape></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.True(read.Succeeded);
        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Uno\nDue", shape.Text);
        Assert.Equal(2, shape.Lists!.Count);
        Assert.All(shape.Lists, item => Assert.Null(item!.Symbol?.Picture));
    }

    [Fact]
    public void Margini_e_rientri_dei_punti_attraversano_XLSX_e_ODS()
    {
        var workbook = Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue\nTre",
            Lists =
            [
                new ShapeListItem { Numbered = true, Margin = 30, Indent = -12 },
                new ShapeListItem { Numbered = true, Level = 1, Margin = 60, Indent = -20 },
                new ShapeListItem { Numbered = true, Margin = 30, Indent = -12 },
            ],
        });

        var crossed = RoundTripXlsx(Workbook(RoundTripOds(Workbook(RoundTripXlsx(workbook)))));

        foreach (var shape in new[] { RoundTripXlsx(workbook), RoundTripOds(workbook), crossed })
        {
            Assert.Equal(30, shape.Lists![0]!.Margin!.Value, 1);
            Assert.Equal(-12, shape.Lists[0]!.Indent!.Value, 1);
            Assert.Equal(60, shape.Lists[1]!.Margin!.Value, 1);
            Assert.Equal(-20, shape.Lists[1]!.Indent!.Value, 1);
            Assert.Equal(1, shape.Lists[1]!.Level);
        }
    }

    [Fact]
    public void Un_a_capo_in_un_punto_XLSX_resta_nel_punto_e_un_livello_invalido_non_rompe()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(new ShapeObject
        {
            Id = "Forma",
            Shape = ShapeKind.Rectangle,
            Text = "Uno\nDue",
            Lists = [new ShapeListItem { Numbered = true }, new ShapeListItem { Level = 2 }],
        }), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var drawing = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!;
            var paragraphs = drawing.Descendants<A.Paragraph>().ToList();
            var run = paragraphs[0].Elements<A.Run>().Last();
            run.InsertAfterSelf(new A.Run(new A.Text("dopo")));
            run.InsertAfterSelf(new A.Break());
            paragraphs[1].ParagraphProperties!.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("lvl", string.Empty, "molto"));
            drawing.Save();
        }

        var read = new XlsxReader().Read(new MemoryStream(stream.ToArray()));

        Assert.True(read.Succeeded);
        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Uno\u2028dopo\nDue", shape.Text);
        Assert.True(shape.Lists![0]!.Numbered);
        Assert.False(shape.Lists[1]!.Numbered);
        Assert.Equal(0, shape.Lists[1]!.Level);
    }

    [Fact]
    public void Un_a_capo_in_un_punto_ODS_resta_nel_punto()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:custom-shape draw:name=\"Forma\" svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\">"
            + "<text:p>Testa</text:p><text:list text:style-name=\"L1\"><text:list-item><text:p>Uno<text:line-break/>dopo</text:p></text:list-item><text:list-item><text:p>Due</text:p></text:list-item></text:list>"
            + "<draw:enhanced-geometry draw:type=\"rectangle\"/></draw:custom-shape></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Testa\nUno\u2028dopo\nDue", shape.Text);
        Assert.Null(shape.Lists![0]);
        Assert.NotNull(shape.Lists[1]);
        Assert.NotNull(shape.Lists[2]);
    }

    private ShapeObject RoundTripXlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        var read = new XlsxReader().Read(new MemoryStream(stream.ToArray()));
        return Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
    }

    private ShapeObject RoundTripOds(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        var read = new OdsReader().Read(new MemoryStream(stream.ToArray()));
        return Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
    }

    private byte[] Bulleted(bool resolvable)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Workbook(), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var drawings = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!;
            var id = "rIdMissing";
            if (resolvable)
            {
                var image = drawings.AddImagePart(ImagePartType.Png);
                using (var data = new MemoryStream(Png)) image.FeedData(data);
                id = drawings.GetIdOfPart(image);
            }
            foreach (var paragraph in drawings.WorksheetDrawing!.Descendants<A.Paragraph>())
            {
                var properties = paragraph.ParagraphProperties ?? paragraph.PrependChild(new A.ParagraphProperties());
                properties.Append(new A.BulletSizePoints { Val = 400000 }, new A.PictureBullet(new A.Blip { Embed = id }));
            }
            drawings.WorksheetDrawing.Save();
        }
        return stream.ToArray();
    }

    private Workbook Workbook() => Workbook(new ShapeObject
    {
        Id = "Forma",
        Shape = ShapeKind.Rectangle,
        Text = "Uno\nDue",
        Runs = [new ShapeTextRun("Uno\nDue") { Bold = true }],
    });

    private Workbook Workbook(ShapeObject content)
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "Forma",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = content,
        });
        return workbook;
    }

    private MemoryStream Ods(string rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.spreadsheet");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" "
                + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
                + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" "
                + "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\">"
                + "<office:automatic-styles><text:list-style style:name=\"L1\"><text:list-level-style-image text:level=\"1\" xlink:href=\"Pictures/missing.png\" xlink:type=\"simple\"><style:list-level-properties fo:width=\"999in\" fo:height=\"999in\"/></text:list-level-style-image></text:list-style></office:automatic-styles>"
                + "<office:body><office:spreadsheet><table:table table:name=\"Foglio1\">"
                + rows + "</table:table></office:spreadsheet></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
