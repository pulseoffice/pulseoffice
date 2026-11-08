using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

public sealed class InlineChartParts
{
    private readonly ChartSpaceBuilder _builder;
    private readonly ChartSpaceReader _reader;
    private readonly LiteralSeriesSource _literals = new();

    public InlineChartParts()
    {
        var families = new ChartFamilies();
        _builder = new ChartSpaceBuilder(families);
        _reader = new ChartSpaceReader(families);
    }

    public string Write(OpenXmlPart owner, ChartDefinition chart)
    {
        var chartPart = owner.AddNewPart<ChartPart>();
        chartPart.ChartSpace = _builder.Build(chart, chart.InlineData ?? ChartData.Empty, _literals);
        chartPart.ChartSpace.Save();
        return owner.GetIdOfPart(chartPart);
    }

    public ChartDefinition? Read(OpenXmlPart owner, string? relationshipId, string id)
    {
        if (relationshipId is null || !owner.TryGetPartById(relationshipId, out var part) || part is not ChartPart { ChartSpace: { } space }) return null;
        if (_reader.Read(space) is not { } content) return null;
        return content.Decorate(new ChartDefinition
        {
            Id = id,
            Type = content.Type,
            SourceSheet = string.Empty,
            SourceRange = string.Empty,
            InlineData = _reader.Literals(content.Series),
        });
    }
}
