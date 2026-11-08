using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using Xunit;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf.Tests;

public class OdfForeignQuirkTests
{
    [Fact]
    public void UnoStileConIlNomeCodificatoSiRisolveDallaFormaInChiaro()
    {
        var document = new XDocument(
            new XElement(Namespaces.Office + "document-styles",
                new XElement(Namespaces.Office + "styles",
                    new XElement(Namespaces.Style + "style",
                        new XAttribute(Namespaces.Style + "name", "ConditionalStyle_5f_1"),
                        new XAttribute(Namespaces.Style + "family", "table-cell"),
                        new XElement(Namespaces.Style + "table-cell-properties",
                            new XAttribute(Namespaces.Fo + "background-color", "#ffc7ce"))))));

        var styles = new OdfCellStyleReader().ReadStyles(document);

        Assert.True(styles.ContainsKey("ConditionalStyle_1"));
        Assert.Equal("#ffc7ce", styles["ConditionalStyle_1"].BackgroundHex);
    }

    [Fact]
    public void UnGraficoSenzaIntervalloComplessivoLoRicavaDalleSerie()
    {
        using var stream = new MemoryStream();

        using (var package = OdfPackage.Create(stream, MimeTypes.Spreadsheet))
        {
            package.WriteXml("content.xml", BuildContentWithChart());
            package.WriteXml("Object 1/content.xml", BuildChartWithoutWholeRange());
            package.WriteManifest(MimeTypes.Spreadsheet,
            [
                ("content.xml", "text/xml"),
                ("Object 1/", MimeTypes.Chart),
                ("Object 1/content.xml", "text/xml"),
            ]);
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var placed = Assert.Single(result.Value!.Sheets[0].Objects);
        var chart = Assert.IsType<ChartDefinition>(placed.Content);

        Assert.Equal("Dati", chart.SourceSheet);
        Assert.Equal("A1:B5", chart.SourceRange);
    }

    private static XDocument BuildContentWithChart() =>
        new(new XElement(Namespaces.Office + "document-content",
            new XAttribute(XNamespace.Xmlns + "office", Namespaces.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "table", Namespaces.Table.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "draw", Namespaces.Draw.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xlink", Namespaces.Xlink.NamespaceName),
            new XElement(Namespaces.Office + "body",
                new XElement(Namespaces.Office + "spreadsheet",
                    new XElement(Namespaces.Table + "table",
                        new XAttribute(Namespaces.Table + "name", "Dati"),
                        new XElement(Namespaces.Table + "table-row",
                            new XElement(Namespaces.Table + "table-cell",
                                new XElement(Namespaces.Draw + "frame",
                                    new XAttribute(Namespaces.Draw + "name", "grafico1"),
                                    new XElement(Namespaces.Draw + "object",
                                        new XAttribute(Namespaces.Xlink + "href", "./Object 1"))))))))));

    private static XDocument BuildChartWithoutWholeRange() =>
        new(new XElement(Namespaces.Office + "document-content",
            new XAttribute(XNamespace.Xmlns + "office", Namespaces.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "chart", Namespaces.Chart.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "table", Namespaces.Table.NamespaceName),
            new XElement(Namespaces.Office + "body",
                new XElement(Namespaces.Office + "chart",
                    new XElement(Namespaces.Chart + "chart",
                        new XAttribute(Namespaces.Chart + "class", "chart:bar"),
                        new XElement(Namespaces.Chart + "plot-area",
                            new XElement(Namespaces.Chart + "series",
                                new XAttribute(Namespaces.Chart + "values-cell-range-address", "Dati.B2:Dati.B5"),
                                new XAttribute(Namespaces.Chart + "label-cell-address", "Dati.B1")),
                            new XElement(Namespaces.Chart + "categories",
                                new XAttribute(Namespaces.Table + "cell-range-address", "Dati.A1:Dati.A5"))))))));
}
