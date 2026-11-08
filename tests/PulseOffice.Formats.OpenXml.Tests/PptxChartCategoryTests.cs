using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxChartCategoryTests
{
    private static (ChartDefinition Read, SlidePart Part) RoundTrip(Action<C.ChartSpace> alter)
    {
        var stream = new MemoryStream();
        using var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation);
        var slide = document.AddPresentationPart().AddNewPart<SlidePart>();
        var mapper = new PptxDeckChartMapper();
        var chart = new ChartDefinition
        {
            Id = "c1", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
            InlineData = new ChartData(["2024", "2025"], [new ChartSeries("Vendite", [1, 2])]),
        };
        var element = new ObjectElement { Id = "e1", Bounds = new DocumentRect(new(0), new(0), new(4000), new(3000)), Content = chart };
        var frame = mapper.Build(element, chart, 2, slide);
        var space = slide.ChartParts.Single().ChartSpace!;
        alter(space);
        space.Save();
        var read = (ChartDefinition)mapper.Read(frame, slide, "e1")!.Content;
        return (read, slide);
    }

    [Fact]
    public void LeCategorieNumericheSiLeggono()
    {
        var (read, _) = RoundTrip(space =>
        {
            foreach (var axis in space.Descendants<C.CategoryAxisData>())
            {
                axis.RemoveAllChildren();
                axis.Append(new C.NumberLiteral(new C.PointCount { Val = 2 },
                    new C.NumericPoint(new C.NumericValue("2024")) { Index = 0 },
                    new C.NumericPoint(new C.NumericValue("2025")) { Index = 1 }));
            }
        });

        Assert.Equal(["2024", "2025"], read.InlineData!.Categories);
    }

    [Fact]
    public void UnIndiceEnormeNonEsaurisceLaMemoria()
    {
        var (read, _) = RoundTrip(space =>
        {
            foreach (var point in space.Descendants<C.Values>().SelectMany(values => values.Descendants<C.NumericPoint>()))
                point.Index = 4_000_000_000;
        });

        Assert.True(read.InlineData!.Series[0].Values.Count < 1000);
    }

    [Fact]
    public void UnGraficoASuperficieSiLegge()
    {
        var (read, _) = RoundTrip(space =>
        {
            var bar = space.Descendants<C.BarChart>().Single();
            var series = bar.Elements<C.BarChartSeries>().Select(old => new C.SurfaceChartSeries(old.ChildElements
                .Where(child => child is C.Index or C.Order or C.SeriesText or C.CategoryAxisData or C.Values).Select(child => child.CloneNode(true)))).ToList();
            var surface = new C.Surface3DChart(new C.Wireframe { Val = false });
            surface.Append(series);
            surface.Append(bar.Elements<C.AxisId>().Select(axis => axis.CloneNode(true)));
            bar.InsertBeforeSelf(surface);
            bar.Remove();
        });

        Assert.Equal(ChartType.Surface3D, read.Type);
        Assert.Equal([1d, 2d], read.InlineData!.Series[0].Values);
    }

    [Fact]
    public void UnaSerieSenzaNomeHaUnNomeInglese()
    {
        var (read, _) = RoundTrip(space =>
        {
            foreach (var text in space.Descendants<C.SeriesText>().ToList()) text.Remove();
        });

        Assert.Equal("Series 1", read.InlineData!.Series[0].Name);
    }
}

public sealed class ChartBlanksTests
{
    [Fact]
    public void IValoriMancantiSonoBuchiNonZeri()
    {
        var stream = new MemoryStream();
        using var document = PresentationDocument.Create(stream, PresentationDocumentType.Presentation);
        var slide = document.AddPresentationPart().AddNewPart<SlidePart>();
        var chart = new ChartDefinition
        {
            Id = "c1", Type = ChartType.Line, SourceSheet = string.Empty, SourceRange = string.Empty,
            InlineData = new ChartData(["a", "b"], [new ChartSeries("S", [1, double.NaN])]),
        };
        new PptxDeckChartMapper().Build(new ObjectElement { Id = "e1", Content = chart }, chart, 2, slide);

        var blanks = slide.ChartParts.Single().ChartSpace!.Descendants<C.DisplayBlanksAs>().Single();
        Assert.Equal(C.DisplayBlanksAsValues.Gap, blanks.Val!.Value);
    }
}
