using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlRangeFormulas(IChartSourceReferences references)
{
    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

    public OoxmlRangeFormulas()
        : this(new ChartSourceReferences())
    {
    }

    public string? Formula(string? model)
    {
        var areas = references.Areas(model);
        if (areas.Count == 0) return null;
        var written = areas.Select(Area).ToList();
        return written.Count == 1 ? written[0] : "(" + string.Join(',', written) + ")";
    }

    public string? Model(string? formula)
    {
        if (formula is not { } text || !text.Contains('!')) return null;
        var trimmed = text.Trim();
        if (trimmed is ['(', .., ')']) trimmed = trimmed[1..^1];
        var areas = references.Areas(trimmed);
        return areas.Count switch
        {
            0 => null,
            1 => references.Write(areas[0].Sheet, areas[0].Range),
            _ => references.Write(areas),
        };
    }

    public IReadOnlyList<string> Parts(string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var trimmed = formula.Trim();
        if (trimmed is not ['(', .., ')']) return [formula];
        var parts = new List<string>();
        var quoted = false;
        var start = 1;
        for (var index = 1; index < trimmed.Length - 1; index++)
        {
            if (trimmed[index] == '\'') quoted = !quoted;
            else if (trimmed[index] == ',' && !quoted)
            {
                parts.Add(trimmed[start..index]);
                start = index + 1;
            }
        }
        parts.Add(trimmed[start..^1]);
        return parts;
    }

    private string Area(ChartSourceReference reference) =>
        reference.Range.IsSingleCell
            ? $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}"
            : $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}:{reference.Range.End.ToA1(true, true)}";
}
