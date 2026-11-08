using System.Text;

namespace PulseOffice.Formats;

public abstract class FormulaDialect
{
    protected virtual string Prefix => string.Empty;

    protected virtual bool ImportsOutsideQuotes => true;

    protected virtual FunctionNameTable? Functions => null;

    private readonly LambdaParameterQualifier _parameters = new();

    public string FromInternal(string internalFormula)
    {
        ArgumentNullException.ThrowIfNull(internalFormula);
        return Prefix + OutsideQuotes(Prepare(_parameters.Qualify(StripEquals(internalFormula.TrimStart()))), segment => Functions is { } names ? names.Export(Export(segment)) : Export(segment));
    }

    public string? ToInternal(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula)) return null;

        var text = Prefix.Length > 0 && formula.StartsWith(Prefix, StringComparison.Ordinal)
            ? formula[Prefix.Length..]
            : StripEquals(formula.TrimStart());

        var imported = ImportsOutsideQuotes ? OutsideQuotes(text, Import) : Import(text);
        return "=" + (Functions is { } names ? OutsideQuotes(imported, names.Import) : imported);
    }

    protected virtual string Prepare(string formula) => formula;

    protected abstract string Export(string segment);

    protected abstract string Import(string segment);

    private string StripEquals(string text) => text.StartsWith('=') ? text[1..] : text;

    private string OutsideQuotes(string text, Func<string, string> transform)
    {
        var result = new StringBuilder(text.Length);
        var segment = new StringBuilder();
        var inQuotes = false;

        foreach (var c in text)
        {
            if (c == '"')
            {
                if (!inQuotes)
                {
                    result.Append(transform(segment.ToString()));
                    segment.Clear();
                }

                result.Append(c);
                inQuotes = !inQuotes;
                continue;
            }

            (inQuotes ? result : segment).Append(c);
        }

        if (segment.Length > 0) result.Append(transform(segment.ToString()));
        return result.ToString();
    }
}
