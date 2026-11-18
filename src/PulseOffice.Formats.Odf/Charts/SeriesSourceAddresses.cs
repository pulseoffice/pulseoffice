using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.Odf;

internal sealed class SeriesSourceAddresses
{
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly ChartSourceReferences _references = new();

    public string Whole(ChartDefinition definition) =>
        string.Join(' ', definition.SeriesSources!
            .SelectMany(source => new[] { source.Name, source.Values, source.Categories })
            .Select(Address)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal));

    public string? Values(ChartDefinition definition, int series) => Address(Source(definition, series)?.Values);

    public string? Label(ChartDefinition definition, int series) => Address(Source(definition, series)?.Name);

    public string? Categories(ChartDefinition definition) =>
        definition.SeriesSources!.Select(source => Address(source.Categories)).FirstOrDefault(address => address is not null);

    public string? Model(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var areas = new List<ChartSourceReference>();
        foreach (var part in Parts(address))
        {
            if (_ranges.Read(part) is not { } found || found.Sheet.Length == 0) return null;
            areas.Add(new ChartSourceReference(found.Sheet, found.Range));
        }
        return areas.Count switch
        {
            0 => null,
            1 => _references.Write(areas[0].Sheet, areas[0].Range),
            _ => _references.Write(areas),
        };
    }

    private List<string> Parts(string address)
    {
        var parts = new List<string>();
        var quoted = false;
        var start = 0;
        for (var index = 0; index < address.Length; index++)
        {
            if (address[index] == '\'') quoted = !quoted;
            else if (address[index] == ' ' && !quoted)
            {
                if (index > start) parts.Add(address[start..index]);
                start = index + 1;
            }
        }
        if (address.Length > start) parts.Add(address[start..]);
        return parts;
    }

    private ChartSeriesSource? Source(ChartDefinition definition, int series) =>
        definition.SeriesSources is { } sources && series >= 0 && series < sources.Count ? sources[series] : null;

    public string? Address(string? text)
    {
        var areas = _references.Areas(text);
        if (areas.Count == 0) return null;
        return string.Join(' ', areas.Select(reference =>
            reference.Range.IsSingleCell ? _ranges.WriteAddress(reference.Sheet, reference.Range.Start) : _ranges.Write(reference.Sheet, reference.Range)));
    }
}
