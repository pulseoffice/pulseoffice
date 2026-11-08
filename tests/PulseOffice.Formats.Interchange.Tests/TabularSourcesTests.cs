using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public sealed class TabularSourcesTests
{
    private readonly ITabularSources _sources = new TabularSources(new DelimitedText());

    [Fact]
    public void LeTabelleHtmlSiElencanoESiLeggono()
    {
        const string html = "<html><body><table><tr><th>Zona</th><th>Importo</th></tr><tr><td>Nord</td><td>1&nbsp;200</td></tr></table>"
            + "<p>x</p><TABLE><TR><TD colspan=2>Unita</TD><TD>z</TD></TR></TABLE></body></html>";
        var source = _sources.For(ExternalDataFormat.Html);

        Assert.Equal(["HTML_all", "HTML_1", "HTML_2"], source.Tables(html));
        var first = source.Read(html, "HTML_1");
        Assert.Equal(2, first.RowCount);
        Assert.Equal("Nord", first.Rows[1][0]);
        Assert.Equal("1 200", first.Rows[1][1]);
        Assert.Equal(["Unita", "", "z"], source.Read(html, "HTML_2").Rows[0]);
        Assert.Equal(3, source.Read(html, "HTML_all").RowCount);
    }

    [Fact]
    public void IRecordXmlDiventanoRighe()
    {
        const string xml = "<ordini><ordine id=\"1\"><cliente>Ada</cliente><totale>10</totale></ordine><ordine id=\"2\"><cliente>Bea</cliente></ordine></ordini>";
        var source = _sources.For(ExternalDataFormat.Xml);

        Assert.Equal(["/ordini/ordine"], source.Tables(xml));
        var data = source.Read(xml, "/ordini/ordine");
        Assert.Equal(["id", "cliente", "totale"], data.Rows[0]);
        Assert.Equal(["2", "Bea", ""], data.Rows[2]);
        Assert.Equal(0, source.Read("<rotto", "/a").RowCount);
    }

    [Fact]
    public void L_XmlConNamespaceSiLegge()
    {
        const string xml = "<o:ordini xmlns:o=\"urn:x\" xmlns=\"urn:y\"><o:ordine o:id=\"7\"><cliente>Ada</cliente></o:ordine><o:ordine o:id=\"8\"><cliente>Bea</cliente></o:ordine></o:ordini>";
        var source = _sources.For(ExternalDataFormat.Xml);

        Assert.Equal(["/ordini/ordine"], source.Tables(xml));
        var data = source.Read(xml, "/ordini/ordine");
        Assert.Equal(["id", "cliente"], data.Rows[0]);
        Assert.Equal(["8", "Bea"], data.Rows[2]);
    }

    [Fact]
    public void IlCsvSiLeggeConIlSeparatoreGiusto()
    {
        var data = _sources.For(ExternalDataFormat.Csv).Read("a;b\n1;\"x;y\"\n", string.Empty);

        Assert.Equal(2, data.RowCount);
        Assert.Equal("x;y", data.Rows[1][1]);
    }

    [Fact]
    public void UnXmlPiattoEnormeSiElencaInFretta()
    {
        var text = "<r>" + string.Concat(Enumerable.Repeat("<i a=\"1\"/>", 200_000)) + "</r>";
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var tables = new XmlTabularSource().Tables(text);

        Assert.Equal(["/r/i"], tables);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UnXPathCheNonDaElementiNonFaCadere() =>
        Assert.Equal(0, new XmlTabularSource().Read("<r><i/><i/></r>", "count(/r/i)").RowCount);

    [Fact]
    public void UnXmlConUnElementoDiversoPerRecordNonEsplodeInMemoria()
    {
        var text = "<r>" + string.Concat(Enumerable.Range(0, 40_000).Select(index => $"<i><c{index}>1</c{index}></i>")) + "</r>";
        var before = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();

        Assert.Throws<InvalidDataException>(() => new XmlTabularSource().Read(text, "/r/i"));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), watch.Elapsed.ToString());
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 2L * 1024 * 1024 * 1024);
    }
}
