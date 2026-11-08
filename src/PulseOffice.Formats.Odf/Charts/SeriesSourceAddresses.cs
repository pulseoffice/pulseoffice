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
        if (string.IsNullOrWhiteSpace(address) || _ranges.Read(address) is not { } found || found.Sheet.Length == 0) return null;
        return _references.Write(found.Sheet, found.Range);
    }

    private ChartSeriesSource? Source(ChartDefinition definition, int series) =>
        definition.SeriesSources is { } sources && series >= 0 && series < sources.Count ? sources[series] : null;

    private string? Address(string? text)
    {
        if (_references.Read(text) is not { } reference) return null;
        return reference.Range.IsSingleCell ? _ranges.WriteAddress(reference.Sheet, reference.Range.Start) : _ranges.Write(reference.Sheet, reference.Range);
    }
}
