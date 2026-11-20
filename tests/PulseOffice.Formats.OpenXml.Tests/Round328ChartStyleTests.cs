using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round328ChartStyleTests
{
    private static CellAddress At(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Book()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[At("A1")] = new Cell { Value = CellValue.Text("Voce") };
        sheet[At("B1")] = new Cell { Value = CellValue.Text("Uno") };
        sheet[At("C1")] = new Cell { Value = CellValue.Text("Due") };
        for (var row = 0; row < 3; row++)
        {
            sheet[At($"A{row + 2}")] = new Cell { Value = CellValue.Text($"V{row + 1}") };
            sheet[At($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
            sheet[At($"C{row + 2}")] = new Cell { Value = CellValue.Number(20 + row) };
        }
        var chart = new ChartDefinition { Id = "grafico1", Type = ChartType.Column, SourceSheet = "Dati", SourceRange = "A1:C4" };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = At("E2") });
        return workbook;
    }

    private static PlacedObject Read(Action<SpreadsheetDocument> alter)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Book(), stream);
        using (var package = SpreadsheetDocument.Open(stream, true)) alter(package);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.Single(result.Value!.Sheets[0].Objects);
    }

    private static IEnumerable<C.BarChartSeries> Series(SpreadsheetDocument package) =>
        package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.ChartParts.Single().ChartSpace!.Descendants<C.BarChartSeries>().ToList();

    private static void Fill(C.BarChartSeries series, OpenXmlElement colour)
    {
        series.RemoveAllChildren<C.ChartShapeProperties>();
        series.GetFirstChild<C.SeriesText>()!.InsertAfterSelf(new C.ChartShapeProperties(new A.SolidFill(colour)));
    }

    [Fact]
    public void Scheme_colours_with_luminance_modifiers_resolve_against_the_theme()
    {
        var placed = Read(package =>
        {
            var series = Series(package).ToList();
            Fill(series[0], new A.SchemeColor { Val = A.SchemeColorValues.Accent2 });
            Fill(series[1], new A.SchemeColor(new A.LuminanceModulation { Val = 50000 }) { Val = A.SchemeColorValues.Background1 });
        });

        var chart = Assert.IsType<ChartDefinition>(placed.Content);
        Assert.Equal("#ED7D31", chart.SeriesColor(0));
        Assert.Equal("#808080", chart.SeriesColor(1));
    }

    [Fact]
    public void Series_without_a_fill_take_the_chart_style_accents()
    {
        var placed = Read(package =>
        {
            foreach (var series in Series(package)) series.RemoveAllChildren<C.ChartShapeProperties>();
        });

        var chart = Assert.IsType<ChartDefinition>(placed.Content);
        Assert.Equal("#4472C4", chart.SeriesColor(0));
        Assert.Equal("#ED7D31", chart.SeriesColor(1));
    }

    [Fact]
    public void A_monochrome_chart_style_shades_one_accent()
    {
        var placed = Read(package =>
        {
            var space = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.ChartParts.Single().ChartSpace!;
            foreach (var series in Series(package)) series.RemoveAllChildren<C.ChartShapeProperties>();
            space.RemoveAllChildren<C.Style>();
            space.InsertBefore(new C.Style { Val = 3 }, space.GetFirstChild<C.Chart>());
        });

        var chart = Assert.IsType<ChartDefinition>(placed.Content);
        Assert.NotEqual(chart.SeriesColor(0), chart.SeriesColor(1));
        Assert.DoesNotContain("#ED7D31", new[] { chart.SeriesColor(0), chart.SeriesColor(1) });
    }

    [Fact]
    public void An_excel_treemap_in_a_chartex_part_is_read_with_its_type_and_ranges()
    {
        const string Chartex = """
            <cx:chartSpace xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:cx="http://schemas.microsoft.com/office/drawing/2014/chartex">
              <cx:chartData>
                <cx:data id="0">
                  <cx:strDim type="cat"><cx:f>Dati!$A$2:$A$4</cx:f><cx:lvl ptCount="3"><cx:pt idx="0">V1</cx:pt><cx:pt idx="1">V2</cx:pt><cx:pt idx="2">V3</cx:pt></cx:lvl></cx:strDim>
                  <cx:numDim type="size"><cx:f>Dati!$B$2:$B$4</cx:f><cx:lvl ptCount="3" formatCode="General"><cx:pt idx="0">10</cx:pt><cx:pt idx="1">11</cx:pt><cx:pt idx="2">12</cx:pt></cx:lvl></cx:numDim>
                </cx:data>
              </cx:chartData>
              <cx:chart>
                <cx:title pos="t"><cx:tx><cx:txData><cx:v>Spese</cx:v></cx:txData></cx:tx></cx:title>
                <cx:plotArea><cx:plotAreaRegion>
                  <cx:series layoutId="treemap" uniqueId="{00000001-0000-0000-0000-000000000000}"><cx:tx><cx:txData><cx:f>Dati!$B$1</cx:f><cx:v>Uno</cx:v></cx:txData></cx:tx><cx:dataId val="0"/></cx:series>
                </cx:plotAreaRegion></cx:plotArea>
                <cx:legend pos="b" align="ctr" overlay="0"/>
              </cx:chart>
            </cx:chartSpace>
            """;

        var placed = Read(package =>
        {
            var drawings = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!;
            var part = drawings.AddNewPart<ExtendedChartPart>("rIdEx1");
            using (var data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Chartex))) part.FeedData(data);
            var anchor = drawings.WorksheetDrawing!.ChildElements.OfType<OpenXmlCompositeElement>().Single();
            var xml = anchor.OuterXml;
            var start = xml.IndexOf("<a:graphicData", StringComparison.Ordinal);
            var end = xml.IndexOf("</a:graphicData>", StringComparison.Ordinal) + "</a:graphicData>".Length;
            var extended = xml[..start]
                + """<a:graphicData uri="http://schemas.microsoft.com/office/drawing/2014/chartex"><cx:chart xmlns:cx="http://schemas.microsoft.com/office/drawing/2014/chartex" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" r:id="rIdEx1"/></a:graphicData>"""
                + xml[end..];
            anchor.Remove();
            drawings.WorksheetDrawing.AppendChild(new AlternateContent(
                $"""<mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"><mc:Choice xmlns:cx1="http://schemas.microsoft.com/office/drawing/2015/9/8/chartex" Requires="cx1">{extended}</mc:Choice></mc:AlternateContent>"""));
            drawings.WorksheetDrawing.Save();
        });

        var chart = Assert.IsType<ChartDefinition>(placed.Content);
        Assert.Equal(ChartType.Treemap, chart.Type);
        Assert.Equal("Dati", chart.SourceSheet);
        Assert.Equal("Spese", chart.ChartTitle);
        Assert.Equal(LegendPosition.Bottom, chart.Legend);
        Assert.Equal("A1:B4", chart.SourceRange);
    }
}
