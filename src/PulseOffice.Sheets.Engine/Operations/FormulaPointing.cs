namespace PulseOffice.Sheets.Engine.Operations;

public readonly record struct PointedReference(string Text, int Start, int Length)
{
    public int Caret => Start + Length;
}

public interface IFormulaPointing
{
    bool AcceptsReference(string text, int caret);

    PointedReference Place(string text, int start, int length, string reference);

    string Qualify(string sheetName, string reference);
}

public sealed class FormulaPointing : IFormulaPointing
{
    private const string Openers = "=(,;+-*/^&<>:{|";
    private const string Closers = "),;+-*/^&<>:}|";

    public bool AcceptsReference(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0 || text[0] != '=' || caret < 1 || caret > text.Length) return false;
        if (InsideQuotes(text, caret)) return false;

        var before = caret - 1;
        while (before > 0 && text[before] == ' ') before--;
        if (!Openers.Contains(text[before], StringComparison.Ordinal)) return false;

        var after = caret;
        while (after < text.Length && text[after] == ' ') after++;
        return after == text.Length || Closers.Contains(text[after], StringComparison.Ordinal);
    }

    public string Qualify(string sheetName, string reference)
    {
        ArgumentNullException.ThrowIfNull(sheetName);
        ArgumentNullException.ThrowIfNull(reference);
        var plain = sheetName.Length > 0 && (char.IsLetter(sheetName[0]) || sheetName[0] == '_')
            && sheetName.All(letter => char.IsLetterOrDigit(letter) || letter is '_' or '.')
            && !LooksLikeCell(sheetName);
        var sheet = plain ? sheetName : "'" + sheetName.Replace("'", "''", StringComparison.Ordinal) + "'";
        return sheet + "!" + reference;
    }

    private bool LooksLikeCell(string name) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, "^([A-Za-z]{1,3}[0-9]+|[Rr][0-9]*[Cc]?[0-9]*)$");

    public PointedReference Place(string text, int start, int length, string reference)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(reference);
        start = Math.Clamp(start, 0, text.Length);
        length = Math.Clamp(length, 0, text.Length - start);
        return new PointedReference(text[..start] + reference + text[(start + length)..], start, reference.Length);
    }

    private bool InsideQuotes(string text, int caret)
    {
        var quote = '\0';
        for (var index = 0; index < caret; index++)
        {
            var current = text[index];
            if (quote == '\0')
            {
                if (current is '"' or '\'') quote = current;
            }
            else if (current == quote) quote = '\0';
        }
        return quote != '\0';
    }
}
