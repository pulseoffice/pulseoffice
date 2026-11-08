using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Interchange.Text;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class TextRoundTripTests
{
    private readonly TextInterchange _formats = new();
    private readonly ModelSnapshot _snapshot = new();

    [Fact]
    public void UnDocumentoRiccoTornaUgualeDalGiroInRtf()
    {
        var back = TextInterchange.Read(TextInterchange.Bytes(TextInterchange.Rich(), _formats.RtfWriter), _formats.RtfReader);
        Assert.Equal(_snapshot.Content(TextInterchange.Rich()), _snapshot.Content(back));
        Assert.Equal("Relazione", back.Properties.Title);
        Assert.Equal("Ada", back.Properties.Author);
    }

    [Fact]
    public void UnDocumentoRiccoTornaUgualeDalGiroInHtml()
    {
        var back = TextInterchange.Read(TextInterchange.Bytes(TextInterchange.Rich(), _formats.HtmlWriter), _formats.HtmlReader);
        Assert.Equal(_snapshot.Content(TextInterchange.Rich()), _snapshot.Content(back));
        Assert.Equal("Relazione", back.Properties.Title);
    }

    [Fact]
    public void IlTestoSempliceConservaLeParole()
    {
        var bytes = TextInterchange.Bytes(TextInterchange.Rich(), _formats.PlainWriter);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Equal("Titolo\nCiao bel mondo è\n1. Primo\n1. Secondo\n• Punto\nPagina 1 di 3\nA\nB\nC\nUnita\nD\nFine\tqui  doppio\na capo\n", text);
        var back = TextInterchange.Read(bytes, _formats.PlainReader);
        Assert.Equal(13, back.Body.Count);
        Assert.Equal("Fine\tqui  doppio", ((ParagraphBlock)back.Body.Blocks[^2]).Text);
    }

    [Theory]
    [InlineData("uno\r\ndue\rtre\nquattro", 4)]
    [InlineData("uno\n\ndue\n", 3)]
    [InlineData("", 1)]
    public void IlTestoSempliceRiconosceOgniFineRiga(string text, int paragraphs)
    {
        var document = TextInterchange.Read(Encoding.UTF8.GetBytes(text), _formats.PlainReader);
        Assert.Equal(paragraphs, document.Body.Count);
    }

    [Fact]
    public void IlTestoSempliceRiconosceLaCodifica()
    {
        const string text = "Perché €";
        foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode, new UTF32Encoding(false, true) })
        {
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            Assert.Equal(text, ((ParagraphBlock)TextInterchange.Read(bytes, _formats.PlainReader).Body[0]).Text);
        }
        Assert.Equal(text, ((ParagraphBlock)TextInterchange.Read(Encoding.UTF8.GetBytes(text), _formats.PlainReader).Body[0]).Text);
        Assert.Equal(text, ((ParagraphBlock)TextInterchange.Read(Encoding.Unicode.GetBytes(text), _formats.PlainReader).Body[0]).Text);
        Assert.Equal(text, ((ParagraphBlock)TextInterchange.Read([0x50, 0x65, 0x72, 0x63, 0x68, 0xE9, 0x20, 0x80], _formats.PlainReader).Body[0]).Text);
    }

    [Fact]
    public void LHtmlNonEsegueNullaEScartaICollegamentiPericolosi()
    {
        const string html = "<html><head><script>alert('x')</script><style>p{color:red}</style></head><body>"
            + "<p onclick=\"evil()\">Ciao <a href=\"javascript:evil()\">qui</a> <a href=\"https://ok.example\">là</a></p>"
            + "<img src=\"https://tracker.example/x.png\"><iframe src=\"x\">dentro</iframe><p>fine &amp; &lt;b&gt;</p></body></html>";
        var read = _formats.HtmlReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(html)));
        Assert.True(read.Succeeded);
        var paragraphs = read.Value!.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.Equal("Ciao qui là", paragraphs[0].Text);
        Assert.Null(paragraphs[0].Content.Runs.Single(run => run.Text.Contains("qui")).Hyperlink);
        Assert.Equal("https://ok.example", paragraphs[0].Content.Runs.Single(run => run.Text.Contains('à')).Hyperlink);
        Assert.Equal("fine & <b>", paragraphs[^1].Text);
        Assert.DoesNotContain(paragraphs, paragraph => paragraph.Text.Contains("alert") || paragraph.Text.Contains("dentro") || paragraph.Text.Contains("color"));
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "HTML211");
        Assert.Empty(read.Value.Objects.All);
    }

    [Fact]
    public void LHtmlMalformatoONidificatoNonFaCadereNulla()
    {
        var inputs = new[]
        {
            string.Concat(Enumerable.Repeat("<div><b><i>", 20000)) + "testo",
            "<table><tr><td>a<td>b<tr><td colspan=999999 rowspan=-3>c</table></td></tr>",
            "<p>non chiuso <b>grassetto <i>corsivo</p> fuori</b>",
            "<<<>>><a href='x' href=\"y\"><img src=\"data:image/png;base64,!!!\"><ul><li>uno<li>due</ol>",
            "<html><body>" + new string('x', 100000),
        };
        foreach (var html in inputs)
        {
            var read = _formats.HtmlReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(html)));
            Assert.True(read.Succeeded);
        }
    }

    [Fact]
    public void LRtfDanneggiatoNonFaCadereNulla()
    {
        var rtf = TextInterchange.Bytes(TextInterchange.Rich(), _formats.RtfWriter);
        var random = new Random(7);
        for (var cut = 6; cut < rtf.Length; cut += Math.Max(1, rtf.Length / 60)) Assert.True(_formats.RtfReader.Read(new MemoryStream(rtf[..cut])).Succeeded);
        for (var round = 0; round < 40; round++)
        {
            var damaged = (byte[])rtf.Clone();
            for (var i = 0; i < 30; i++) damaged[random.Next(6, damaged.Length)] = (byte)random.Next(256);
            Assert.True(_formats.RtfReader.Read(new MemoryStream(damaged)).Succeeded);
        }
        var nested = Encoding.ASCII.GetBytes("{\\rtf1 " + new string('{', 200000) + "testo" + "\\bin999999999 " + "\\u-99999999999 \\'zz\\fs99999999999");
        Assert.True(_formats.RtfReader.Read(new MemoryStream(nested)).Succeeded);
        Assert.False(_formats.RtfReader.Read(new MemoryStream("non rtf"u8.ToArray())).Succeeded);
    }

    [Fact]
    public void LRtfLeggeCodificheUnicodeESimboli()
    {
        var rtf = @"{\rtf1\ansi\ansicpg1252{\fonttbl{\f0\fswiss Arial;}{\f1\fcharset204 Times;}}\uc1 caf\'e9 " + "\\u8364?" + @" {\f1 \'cf\'f0\'e8} a\~b\emdash c\par}";
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        Assert.Equal("café € При a b—c", ((ParagraphBlock)document.Body[0]).Text);
    }

    [Fact]
    public void IlCarattereDefinitoComePredefinitoNellRtfDiTextEditNonSiPerde()
    {
        var rtf = @"{\rtf1\ansi\ansicpg1252\cocoartf2907{\fonttbl\f0\fswiss\fcharset0 Helvetica;\f1\fswiss\fcharset0 Helvetica-Bold;}\pard\f1\b uno\f0\b0  due\par}";
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        var runs = ((ParagraphBlock)document.Body[0]).Content.Runs;
        Assert.All(runs, run => Assert.StartsWith("Helvetica", run.Font));
    }

    [Fact]
    public void IlCaratterePredefinitoDelDocumentoNonDiventaFormattazioneDiretta()
    {
        var rtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Liberation Serif;}}\pard testo\par}";
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        Assert.All(((ParagraphBlock)document.Body[0]).Content.Runs, run => Assert.Null(run.Font));
    }

    [Fact]
    public void LeCelleUniteInVerticaleSiLeggonoDaRtf()
    {
        var rtf = @"{\rtf1\ansi\trowd\clvmgf\cellx1000\cellx2000\pard\intbl A\cell B\cell\row\trowd\clvmrg\cellx1000\cellx2000\pard\intbl\cell C\cell\row\pard fine\par}";
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader);
        var table = Assert.IsType<TableBlock>(document.Body[0]);
        Assert.Equal(2, table.Cell(0, 0).RowSpan);
        Assert.True(table.Cell(1, 0).Covered);
        Assert.Equal("C", ((ParagraphBlock)table.Cell(1, 1).Content[0]).Text);
        Assert.Equal("fine", ((ParagraphBlock)document.Body[1]).Text);
    }

    [Fact]
    public void LePerditeSonoDichiarate()
    {
        var document = TextInterchange.Rich();
        document.Comments.Add(new Comment("c1", "Bea", DateTimeOffset.UnixEpoch, new BlockContainer()));
        Assert.Contains(_formats.RtfWriter.CheckFidelity(document).Losses, loss => loss.Code == "RTF004");
        Assert.Contains(_formats.HtmlWriter.CheckFidelity(document).Losses, loss => loss.Code == "HTML203");
        Assert.Contains(_formats.PlainWriter.CheckFidelity(document).Losses, loss => loss.Code == "TXT001");
        Assert.DoesNotContain(_formats.RtfWriter.CheckFidelity(TextInterchange.Rich()).Losses, loss => loss.Code is "RTF002" or "RTF004");
    }

    [Fact]
    public void IlRegistroRiconosceRtfEHtmlDalContenutoEIlTestoDallEstensione()
    {
        var registry = new TextDocumentFormatRegistry([_formats.RtfReader, _formats.HtmlReader, _formats.PlainReader], [_formats.RtfWriter, _formats.HtmlWriter, _formats.PlainWriter], new PulseOffice.Formats.Intake.DocumentIntake());
        Assert.Equal("rtf", registry.ReaderForContent(new MemoryStream(TextInterchange.Bytes(TextInterchange.Rich(), _formats.RtfWriter)))!.Format.Id);
        Assert.Equal("html", registry.ReaderForContent(new MemoryStream(TextInterchange.Bytes(TextInterchange.Rich(), _formats.HtmlWriter)))!.Format.Id);
        Assert.Null(registry.ReaderForContent(new MemoryStream("ciao"u8.ToArray())));
        Assert.True(registry.Read("nota.txt", new MemoryStream("ciao"u8.ToArray())).Succeeded);
        Assert.Equal(TextInterchangeFormats.Rtf, registry.WriterFor("x.rtf")!.Format);
    }
}
