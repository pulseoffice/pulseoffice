using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Engine.Operations;

public readonly record struct CycledReference(string Text, int Caret);

public interface IReferenceCycler
{
    string Cycle(string formula, int caret);

    CycledReference CycleAt(string formula, int caret);
}

public sealed partial class ReferenceCycler : IReferenceCycler
{
    [GeneratedRegex(@"(\$?)([A-Za-z]{1,3})(\$?)(\d{1,7})", RegexOptions.CultureInvariant)]
    private partial Regex Reference();

    public string Cycle(string formula, int caret) => CycleAt(formula, caret).Text;

    public CycledReference CycleAt(string formula, int caret)
    {
        ArgumentNullException.ThrowIfNull(formula);

        var candidates = Candidates(formula);
        if (candidates.Count == 0) return new CycledReference(formula, caret);

        var index = candidates.FindIndex(match => caret >= match.Index && caret <= match.Index + match.Length);
        if (index < 0) index = candidates.Count - 1;

        var group = new List<Match> { candidates[index] };
        if (index > 0 && Joined(formula, candidates[index - 1], candidates[index])) group.Insert(0, candidates[index - 1]);
        else if (index + 1 < candidates.Count && Joined(formula, candidates[index], candidates[index + 1])) group.Add(candidates[index + 1]);

        var first = group[0];
        var next = Next(first.Groups[1].Value.Length > 0, first.Groups[3].Value.Length > 0);

        var text = formula;
        var end = 0;
        for (var i = group.Count - 1; i >= 0; i--)
        {
            var match = group[i];
            var replacement =
                (next.Column ? "$" : string.Empty) + match.Groups[2].Value
                + (next.Row ? "$" : string.Empty) + match.Groups[4].Value;
            text = text[..match.Index] + replacement + text[(match.Index + match.Length)..];
            if (i == group.Count - 1) end = match.Index + replacement.Length;
        }

        var shift = 0;
        if (group.Count == 2)
        {
            var firstReplacement = (next.Column ? 1 : 0) + (next.Row ? 1 : 0) + first.Groups[2].Length + first.Groups[4].Length;
            shift = firstReplacement - first.Length;
        }

        return new CycledReference(text, end + shift);
    }

    private List<Match> Candidates(string formula)
    {
        var strings = StringSpans(formula);
        return Reference().Matches(formula)
            .Where(match => !strings.Any(span => match.Index >= span.Start && match.Index < span.End))
            .Where(match => Bounded(formula, match))
            .ToList();
    }

    private bool Bounded(string formula, Match match)
    {
        var before = match.Index - 1;
        var after = match.Index + match.Length;
        if (before >= 0 && (char.IsLetterOrDigit(formula[before]) || formula[before] is '_' or '$')) return false;
        if (after < formula.Length && (char.IsLetterOrDigit(formula[after]) || formula[after] is '_' or '(' or '$' or '.')) return false;
        return true;
    }

    private List<(int Start, int End)> StringSpans(string formula)
    {
        var spans = new List<(int, int)>();
        var start = -1;
        var quote = '\0';
        for (var i = 0; i < formula.Length; i++)
        {
            var current = formula[i];
            if (start < 0)
            {
                if (current is '"' or '\'') { start = i; quote = current; }
            }
            else if (current == quote)
            {
                if (i + 1 < formula.Length && formula[i + 1] == quote) { i++; continue; }
                spans.Add((start, i + 1));
                start = -1;
            }
        }
        if (start >= 0) spans.Add((start, formula.Length));
        return spans;
    }

    private bool Joined(string formula, Match left, Match right) =>
        left.Index + left.Length < formula.Length && formula[left.Index + left.Length] == ':'
        && right.Index == left.Index + left.Length + 1;

    private (bool Column, bool Row) Next(bool column, bool row) => (column, row) switch
    {
        (false, false) => (true, true),
        (true, true) => (false, true),
        (false, true) => (true, false),
        _ => (false, false),
    };
}
