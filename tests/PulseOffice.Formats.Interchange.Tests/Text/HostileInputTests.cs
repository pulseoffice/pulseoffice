using System.Globalization;
using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public class HostileInputTests
{
    private readonly TextInterchange _formats = new();

    [Fact]
    public void UnRtfConMilioniDiGruppiAnnidatiNonEsaurisceLaMemoria()
    {
        const int depth = 4_000_000;
        var rtf = new StringBuilder("{\\rtf1 prima").Append('{', depth).Append("dentro").Append('}', depth).Append(" dopo\\par}").ToString();
        var before = GC.GetAllocatedBytesForCurrentThread();

        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 400L * 1024 * 1024);
        Assert.Contains("prima", document.Body.Blocks.OfType<ParagraphBlock>().First().Text, StringComparison.Ordinal);
        Assert.Contains("dopo", document.Body.Blocks.OfType<ParagraphBlock>().First().Text, StringComparison.Ordinal);
    }

    [Fact]
    public void IlSaltoDopoUnCarattereUnicodeFinisceConIlGruppo()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes("{\\rtf1 {\\u8364}ab\\par}"), _formats.RtfReader);

        Assert.Equal("€ab", document.Body.Blocks.OfType<ParagraphBlock>().First().Text);
    }

    [Fact]
    public void UnaTabellaHtmlConCelleEnormementeEsteseVieneTroncataEDichiarata()
    {
        var html = new StringBuilder("<html><body><table>");
        for (var row = 0; row < 1000; row++) html.Append("<tr><td colspan=\"1000\" rowspan=\"1000\">x</td></tr>");
        html.Append("</table></body></html>");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html.ToString()));
        var read = _formats.HtmlReader.Read(stream);

        Assert.True(read.Succeeded);
        var table = Assert.Single(read.Value!.Body.Blocks.OfType<TableBlock>());
        Assert.True(table.Columns.Count <= 1024);
        Assert.True((long)table.Rows.Count * table.Columns.Count <= 1_000_000);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF258");
    }

    [Fact]
    public void MolteTabelleHtmlLargheRestanoEntroIlTettoDiCelleDelDocumento()
    {
        var html = new StringBuilder("<html><body>");
        for (var table = 0; table < 3; table++)
        {
            html.Append("<table><tr><td colspan=\"1024\">x</td></tr>");
            for (var row = 0; row < 700; row++) html.Append("<tr><td>y</td></tr>");
            html.Append("</table>");
        }
        html.Append("</body></html>");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html.ToString()));
        var read = _formats.HtmlReader.Read(stream);

        Assert.True(read.Succeeded);
        var cells = read.Value!.Body.Blocks.OfType<TableBlock>().Sum(table => (long)table.Rows.Count * table.Columns.Count);
        Assert.InRange(cells, 1, ImportedTableLimits.DocumentTableCells);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF258");
    }

    [Fact]
    public void UnaLunghezzaCssSmisurataNonDiventaInfinita()
    {
        var css = new CssDeclarations();

        Assert.Null(css.Points("1e308in"));
        Assert.Null(css.Points("1e306"));
        Assert.Equal(72, css.Points("1in"));
    }

    [Fact]
    public void IlNomeDiUnCarattereNonPuoIniettareMarcatoNellHtml()
    {
        var document = _formats.Factory.Create();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("x") { Runs = [new TextRun("x") { Font = "Arial</style><script>alert(1)</script>" }] })]);

        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IlNomeDiUnCarattereNonEsceDallAttributoStyleNelFrammentoHtmlDiLedger()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Foglio1");
        var address = new CellAddress(0, 0);
        sheet.SetValue(address, CellValue.Text("dato"));
        sheet.SetStyle(address, workbook.Styles.Intern(CellStyle.Default with { Font = CellStyle.Default.Font with { Family = "Arial\" onmouseover=\"alert(1)" } }));
        var writer = new HtmlSpreadsheetWriter(new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.FromOADate(serial))));

        var fragment = writer.RenderFragment(sheet, new CellRange(address, address), workbook.Styles);

        Assert.DoesNotContain("\" onmouseover=", fragment, StringComparison.Ordinal);
    }

    [Fact]
    public void IlCampioneCsvTroncatoInMezzoAUnCarattereRestaUtf8()
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes("a;b\n"));
        while (bytes.Count < 8192 * 4 - 1) bytes.AddRange(Encoding.ASCII.GetBytes("1;2\n").Take(8192 * 4 - 1 - bytes.Count));
        bytes.AddRange(Encoding.UTF8.GetBytes("è;ò\n"));
        var delimited = new PulseOffice.Foundation.Text.DelimitedText();
        var reader = new CsvSpreadsheetReader(delimited, new CsvValueReader(), new CsvSniffer(delimited));

        var options = reader.Detect(new MemoryStream([.. bytes]));

        Assert.Equal("utf-8", options.Encoding.WebName);
    }
    [Fact]
    public void MoltiFrammentiDiTestoConLaStessaFormaNonCostanoUnTempoQuadratico()
    {
        var html = new StringBuilder("<html><body><p>");
        for (var i = 0; i < 400_000; i++) html.Append("<i></i>a");
        html.Append("</p></body></html>");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var document = TextInterchange.Read(Encoding.UTF8.GetBytes(html.ToString()), _formats.HtmlReader);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
        Assert.Equal(400_000, document.Body.Blocks.OfType<ParagraphBlock>().First().Text.Length);
    }

    [Fact]
    public void MoltiGruppiRtfVuotiFraLeLettereNonCostanoUnTempoQuadratico()
    {
        var rtf = new StringBuilder("{\\rtf1 ");
        for (var i = 0; i < 600_000; i++) rtf.Append("{}a");
        rtf.Append("\\par}");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf.ToString()), _formats.RtfReader);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), watch.Elapsed.ToString());
        Assert.Equal(600_000, document.Body.Blocks.OfType<ParagraphBlock>().First().Text.Length);
    }

    [Fact]
    public void MoltiElementiColConSpanEnormeNonEsaurisconoLaMemoria()
    {
        var html = new StringBuilder("<html><body><table>");
        for (var i = 0; i < 200_000; i++) html.Append("<col span=1000 width=10>");
        html.Append("<tr><td>x</td></tr></table></body></html>");
        var before = GC.GetAllocatedBytesForCurrentThread();

        var document = TextInterchange.Read(Encoding.UTF8.GetBytes(html.ToString()), _formats.HtmlReader);

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 400L * 1024 * 1024);
        Assert.Single(document.Body.Blocks.OfType<TableBlock>());
    }

    [Fact]
    public void RientriEScaleRtfSmisuratiVengonoLimitati()
    {
        var rtf = "{\\rtf1 \\li2000000000\\ri-2000000000\\fi2000000000\\sb2000000000\\sa2000000000\\charscalex2000000000\\expndtw2000000000 x\\par}";

        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);

        var paragraph = document.Body.Blocks.OfType<ParagraphBlock>().First().Content;
        var properties = new DirectFormatting().Paragraph(paragraph);
        Assert.True(properties.MarginLeft is <= 1584);
        Assert.True(properties.MarginRight is >= -1584);
        Assert.True(properties.Indent is <= 1584);
        Assert.True(properties.SpaceBefore is <= 1584);
        Assert.True(properties.SpaceAfter is <= 1584);
        var character = new DirectFormatting().Run(paragraph.Runs[0]);
        Assert.True(character.ScaleWidth is <= 600);
        Assert.True(character.LetterSpacing is <= 1584);
    }

    [Fact]
    public void LeUnitaRemSiLeggonoComeLeEm()
    {
        var css = new CssDeclarations();

        Assert.Equal(24, css.Points("2rem"));
    }

    [Fact]
    public void MilioniDiRigheHtmlVuoteNonCresconoSenzaLimite()
    {
        var html = new StringBuilder("<table>");
        for (var i = 0; i < 3_000_000; i++) html.Append("<tr>");
        html.Append("<tr><td>x</td></tr></table>");
        var before = GC.GetAllocatedBytesForCurrentThread();

        new HtmlTableReader(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations())).FirstTable(html.ToString());

        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 300L * 1024 * 1024, (GC.GetAllocatedBytesForCurrentThread() - before).ToString());
    }

    [Fact]
    public void LArgomentoDiUnCampoConParentesiNonRompeLRtf()
    {
        var document = _formats.Factory.Create();
        var run = new TextRun("valore") { Field = TextFieldKind.Variable, FieldArgument = "nome}{\\par città" };
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("prima valore dopo") { Runs = [new TextRun("prima "), run, new TextRun(" dopo")] })]);

        var read = TextInterchange.Read(TextInterchange.Bytes(document, _formats.RtfWriter), _formats.RtfReader);

        var paragraphs = read.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Single(paragraphs);
        Assert.Equal("prima valore dopo", paragraphs[0].Text);
        Assert.Equal("nome}{\\par città", paragraphs[0].Content.Runs.Single(r => r.Field == TextFieldKind.Variable).FieldArgument);
    }

    [Fact]
    public void UnCollegamentoConVirgoletteSopravviveAllRtf()
    {
        var document = _formats.Factory.Create();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("x") { Runs = [new TextRun("x") { Hyperlink = "https://example.com/a\"b" }] })]);

        var read = TextInterchange.Read(TextInterchange.Bytes(document, _formats.RtfWriter), _formats.RtfReader);

        var link = read.Body.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single().Hyperlink;
        Assert.Equal("https://example.com/a%22b", link);
    }
}
