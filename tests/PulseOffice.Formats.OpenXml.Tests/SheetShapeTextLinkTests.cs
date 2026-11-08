using System.IO.Compression;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class SheetShapeTextLinkTests
{
    private const string Target = "https://example.org/";

    [Fact]
    public void I_collegamenti_nel_testo_di_una_cornice_ODS_si_leggono_senza_dichiarare_perdite()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:frame draw:name=\"Cornice\" svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"><draw:text-box>"
            + "<text:p>Vai a <text:a xlink:type=\"simple\" xlink:href=\"" + Target + "\">esempio</text:a> ora</text:p></draw:text-box></draw:frame></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF194");
        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("Vai a esempio ora", shape.Text);
        Assert.Contains(shape.Runs!, run => run.Text == "esempio" && run.Link == Target);
    }

    [Fact]
    public void I_collegamenti_nel_testo_di_una_forma_ODS_si_leggono()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:custom-shape draw:name=\"Forma\" svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\">"
            + "<text:p><text:span><text:a xlink:href=\"" + Target + "\">qui</text:a></text:span></text:p>"
            + "<draw:enhanced-geometry draw:type=\"rectangle\"/></draw:custom-shape></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF194");
        var shape = Assert.IsType<ShapeObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal(Target, Assert.Single(shape.Runs!).Link);
    }

    [Fact]
    public void I_collegamenti_nel_testo_dei_tracciati_ODS_restano_dichiarati()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:path svg:width=\"4cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\" svg:viewBox=\"0 0 10 10\" svg:d=\"M0 0L10 10\">"
            + "<text:p><text:a xlink:href=\"" + Target + "\">qui</text:a></text:p></draw:path></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF194");
    }

    [Fact]
    public void I_collegamenti_nel_testo_delle_forme_attraversano_ODS_e_XLSX()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Dati").Objects.Add(new PlacedObject
        {
            Id = "Forma",
            Anchor = new PulseOffice.Sheets.Addressing.CellAddress(1, 1),
            Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(4), PulseOffice.Foundation.Units.Twip.FromCentimeters(2)),
            Content = new ShapeObject
            {
                Id = "Forma",
                Shape = ShapeKind.Rectangle,
                Text = "Vai a esempio",
                Runs = [new ShapeTextRun("Vai a "), new ShapeTextRun("esempio") { Link = Target }],
            },
        });

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        var fromXlsx = new XlsxReader().Read(new MemoryStream(xlsx.ToArray()));
        Assert.DoesNotContain(fromXlsx.Diagnostics, diagnostic => diagnostic.Code == "XLSX182");
        Linked(fromXlsx.Value!);

        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx.Value!, ods);
        var fromOds = new OdsReader().Read(new MemoryStream(ods.ToArray()));
        Assert.DoesNotContain(fromOds.Diagnostics, diagnostic => diagnostic.Code == "ODF194");
        Linked(fromOds.Value!);
    }

    private void Linked(Workbook workbook)
    {
        var shape = Assert.IsType<ShapeObject>(Assert.Single(workbook.Sheets[0].Objects).Content);
        Assert.Equal("Vai a esempio", shape.Text);
        Assert.Contains(shape.Runs!, run => run.Text == "esempio" && run.Link == Target);
        Assert.Contains(shape.Runs!, run => run.Text == "Vai a " && run.Link is null);
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
                + "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:body><office:spreadsheet><table:table table:name=\"Foglio1\">"
                + rows + "</table:table></office:spreadsheet></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
