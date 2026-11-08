using System.IO.Compression;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtOutOfRangeValueTests
{
    [Fact]
    public void Un_inizio_di_elenco_sproporzionato_viene_limitato()
    {
        var read = new OdtReader().Read(Odt(
            "<text:list-style style:name=\"L1\"><text:list-level-style-number text:level=\"1\" style:num-format=\"a\" text:start-value=\"2000000000\"/></text:list-style>",
            "<text:list text:style-name=\"L1\"><text:list-item><text:p>voce</text:p></text:list-item></text:list>"));
        var level = Assert.IsType<PulseOffice.Documents.Styles.ListStyle>(read.Value!.Styles.List.Find("L1")).Levels[0];
        Assert.InRange(level.StartAt, 0, ushort.MaxValue);
    }

    [Fact]
    public void Un_capolettera_sproporzionato_viene_limitato()
    {
        var read = new OdtReader().Read(Odt(
            "<style:style style:name=\"P1\" style:family=\"paragraph\"><style:paragraph-properties><style:drop-cap style:lines=\"2000000000\" style:length=\"2000000000\"/></style:paragraph-properties></style:style>",
            "<text:p text:style-name=\"P1\">capolettera</text:p>"));
        var drop = read.Value!.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Content.DropCap).OfType<PulseOffice.Text.Rich.DropCap>().Single();
        Assert.InRange(drop.Lines, 1, 255);
        Assert.InRange(drop.Characters, 0, 255);
    }

    [Fact]
    public void Un_inizio_sproporzionato_su_una_voce_di_elenco_viene_limitato()
    {
        var read = new OdtReader().Read(Odt(string.Empty,
            "<text:list><text:list-item text:start-value=\"2000000000\"><text:p>voce</text:p></text:list-item></text:list>"));
        var paragraph = read.Value!.Body.Blocks.OfType<ParagraphBlock>().Single();
        Assert.InRange(paragraph.Content.ListRestart ?? 0, 0, ushort.MaxValue);
    }

    [Fact]
    public void Righe_e_celle_ripetute_hanno_un_tetto_complessivo()
    {
        var row = "<table:table-row table:number-rows-repeated=\"1024\"><table:table-cell table:number-columns-repeated=\"128\"/></table:table-row>";
        var read = new OdtReader().Read(Odt(string.Empty,
            "<table:table xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" table:name=\"T\">" + row + row + "</table:table>"));
        var table = read.Value!.Body.Blocks.OfType<TableBlock>().Single();
        Assert.True(table.Rows.Sum(item => item.Cells.Count) <= 150_000);
    }

    [Fact]
    public void Una_cella_data_con_un_numero_fuori_calendario_non_ferma_il_salvataggio()
    {
        var cell = new TableCell(new BlockContainer([new ParagraphBlock("c1", "99999999")]))
        {
            NumberFormat = new CellNumberFormat(CellValueKind.Date, 0, false),
        };
        var document = new PulseOffice.Documents.TextDocument();
        document.Body.Replace([new TableBlock("t1", [new TableRow([cell])], [PulseOffice.Foundation.Units.Twip.FromCentimeters(5)])]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
    }

    private static MemoryStream Odt(string automatic, string body)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
                + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\"><office:automatic-styles>" + automatic
                + "</office:automatic-styles><office:body><office:text>" + body + "</office:text></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
