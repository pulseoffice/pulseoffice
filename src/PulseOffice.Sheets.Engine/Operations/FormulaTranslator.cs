namespace PulseOffice.Sheets.Engine.Operations;

public sealed class FormulaTranslator
{
    public string? Translate(string formula, int rowDelta, int columnDelta)
    {
        ArgumentNullException.ThrowIfNull(formula);

        return rowDelta == 0 && columnDelta == 0
            ? formula
            : new OffsetRewriter(rowDelta, columnDelta).Rewrite(formula);
    }
}
