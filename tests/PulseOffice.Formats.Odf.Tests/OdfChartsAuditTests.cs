using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfChartsAuditTests
{
    private readonly XNamespace _table = OdfPackage.Namespaces.Table;
    private readonly XNamespace _office = OdfPackage.Namespaces.Office;
    private readonly XNamespace _draw = OdfPackage.Namespaces.Draw;
    private readonly XNamespace _xlink = OdfPackage.Namespaces.Xlink;
    private readonly OdfDeckChartMapper _mapper = new();

    [Fact]
    public void Una_riga_corta_non_sposta_i_valori_delle_righe_successive()
    {
        var data = Read(document =>
        {
            var rows = document.Descendants(_table + "table-rows").Single().Elements(_table + "table-row").ToList();
            rows[0].Elements(_table + "table-cell").Last().Remove();
        });

        Assert.Equal(3, data.Series[1].Values.Count);
        Assert.True(double.IsNaN(data.Series[1].Values[0]));
        Assert.Equal(20, data.Series[1].Values[1]);
        Assert.Equal(30, data.Series[1].Values[2]);
    }

    [Fact]
    public void Una_serie_che_compare_tardi_resta_allineata_alle_categorie()
    {
        var data = Read(document =>
        {
            var rows = document.Descendants(_table + "table-rows").Single().Elements(_table + "table-row").ToList();
            rows[0].Elements(_table + "table-cell").Skip(1).Remove();
        });

        Assert.Equal(3, data.Series[0].Values.Count);
        Assert.True(double.IsNaN(data.Series[0].Values[0]));
        Assert.Equal(2, data.Series[0].Values[1]);
    }

    [Fact]
    public void Un_valore_infinito_si_legge_come_mancante()
    {
        var data = Read(document =>
        {
            var cell = document.Descendants(_table + "table-rows").Single().Elements(_table + "table-row").First().Elements(_table + "table-cell").ElementAt(1);
            cell.SetAttributeValue(_office + "value", "1e999");
        });

        Assert.True(double.IsNaN(data.Series[0].Values[0]));
    }

    private ChartData Read(Action<XDocument> change)
    {
        var definition = new ChartDefinition { Id = "c1", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty };
        var source = new ChartData(["A", "B", "C"], [new ChartSeries("Uno", [1, 2, 3]), new ChartSeries("Due", [10, 20, 30])]);
        var document = _mapper.BuildDocument(definition, source, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)), System.Globalization.CultureInfo.InvariantCulture);
        change(document);
        var frame = new XElement(_draw + "frame", new XElement(_draw + "object", new XAttribute(_xlink + "href", "./Object 1")));

        var element = _mapper.Read(frame, "c1", _ => document);

        return Assert.IsType<ChartDefinition>(element!.Content).InlineData!;
    }
}
