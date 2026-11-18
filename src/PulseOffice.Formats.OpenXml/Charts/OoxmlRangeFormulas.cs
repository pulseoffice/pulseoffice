using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlRangeFormulas(IChartSourceReferences references)
{
    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

    public OoxmlRangeFormulas()
        : this(new ChartSourceReferences())
    {
    }

    public string? Formula(string? model) =>
        references.Read(model) is { } reference
            ? reference.Range.IsSingleCell
                ? $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}"
                : $"{_quoting.Quoted(reference.Sheet)}!{reference.Range.Start.ToA1(true, true)}:{reference.Range.End.ToA1(true, true)}"
            : null;

    public string? Model(string? formula) =>
        formula is { } text && text.Contains('!') && references.Read(text) is { } reference ? references.Write(reference.Sheet, reference.Range) : null;
}
