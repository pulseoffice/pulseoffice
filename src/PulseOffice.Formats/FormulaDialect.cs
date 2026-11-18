using System.Text;

namespace PulseOffice.Formats;

public abstract class FormulaDialect
{
    protected virtual string Prefix => string.Empty;

    protected virtual bool ImportsOutsideQuotes => true;

    protected virtual FunctionNameTable? Functions => null;

    protected virtual bool KeepsTableReferences => false;

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

        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (!inQuotes && c == '[' && KeepsTableReferences && index > 0 && IsTableNamePart(text[index - 1]))
            {
                var end = TableReferenceEnd(text, index);
                result.Append(transform(segment.ToString()));
                segment.Clear();
                result.Append(text, index, end - index);
                index = end - 1;
                continue;
            }

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

    private bool IsTableNamePart(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '\\';

    private int TableReferenceEnd(string text, int open)
    {
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '\'') { index++; continue; }
            if (text[index] == '[') depth++;
            else if (text[index] == ']' && --depth == 0) return index + 1;
        }
        return text.Length;
    }
}
