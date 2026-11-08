namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class DelimitedTextSplitter
{
    public IReadOnlyList<string> Split(string text, IReadOnlyList<string> delimiters, bool ignoreEmpty, StringComparison comparison)
    {
        var parts = new List<string>();
        if (delimiters.Count == 0)
        {
            parts.Add(text);
            return parts;
        }

        var start = 0;
        while (true)
        {
            var (position, length) = NextDelimiter(text, start, delimiters, comparison);
            if (position < 0) break;
            parts.Add(text[start..position]);
            start = position + length;
        }
        parts.Add(text[start..]);

        if (ignoreEmpty) parts.RemoveAll(part => part.Length == 0);
        return parts;
    }

    private static (int Position, int Length) NextDelimiter(string text, int start, IReadOnlyList<string> delimiters, StringComparison comparison)
    {
        var best = -1;
        var bestLength = 0;
        foreach (var delimiter in delimiters)
        {
            var found = text.IndexOf(delimiter, start, comparison);
            if (found < 0) continue;
            if (best < 0 || found < best || (found == best && delimiter.Length > bestLength))
            {
                best = found;
                bestLength = delimiter.Length;
            }
        }
        return (best, bestLength);
    }
}
