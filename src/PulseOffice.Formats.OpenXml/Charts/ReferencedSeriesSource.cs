using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using PulseOffice.Sheets.Engine.Operations;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class ReferencedSeriesSource(ChartDefinition definition, IChartSourceReferences references) : ISeriesSource
{
    private readonly LiteralSeriesSource _literals = new();
    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();
    private readonly IReadOnlyList<ChartSeriesSource> _sources = definition.SeriesSources ?? [];

    public OpenXmlElement Name(int index, string name) =>
        definition.ExplicitSeriesName(index) is null && Formula(Source(index)?.Name) is { } formula
            ? new C.SeriesText(new C.StringReference
            {
                Formula = new C.Formula(formula),
                StringCache = new C.StringCache(new C.PointCount { Val = 1u }, new C.StringPoint(new C.NumericValue(name)) { Index = 0u }),
            })
            : _literals.Name(index, name);

    public OpenXmlElement Categories(IReadOnlyList<string> categories)
    {
        if (Formula(_sources.Select(source => source.Categories).FirstOrDefault(text => references.Read(text) is not null)) is not { } formula)
            return _literals.Categories(categories);

        var cache = new C.StringCache(new C.PointCount { Val = (uint)categories.Count });
        for (var i = 0; i < categories.Count; i++) cache.AppendChild(new C.StringPoint(new C.NumericValue(categories[i])) { Index = (uint)i });
        return new C.CategoryAxisData(new C.StringReference { Formula = new C.Formula(formula), StringCache = cache });
    }

    public OpenXmlElement Values(int index, IReadOnlyList<double> values)
    {
        if (Formula(Source(index)?.Values) is not { } formula) return _literals.Values(index, values);

        var cache = new C.NumberingCache(new C.PointCount { Val = (uint)values.Count });
        for (var i = 0; i < values.Count; i++)
            if (double.IsFinite(values[i]))
                cache.AppendChild(new C.NumericPoint(new C.NumericValue(values[i].ToString(CultureInfo.InvariantCulture))) { Index = (uint)i });
        return new C.Values(new C.NumberReference { Formula = new C.Formula(formula), NumberingCache = cache });
    }

    private ChartSeriesSource? Source(int index) => index >= 0 && index < _sources.Count ? _sources[index] : null;

    private string? Formula(string? text) =>
        references.Read(text) is { } reference
            ? reference.Range.IsSingleCell
                ? $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}"
                : $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}:{reference.Range.End.ToA1(true, true)}"
            : null;
}
