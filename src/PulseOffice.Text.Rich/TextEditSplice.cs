namespace PulseOffice.Text.Rich;

public interface ITextEditSplice
{
    RichText Apply(RichText text, string edited);
}

public sealed class TextEditSplice(IParagraphBreaks? breaks = null) : ITextEditSplice
{
    private readonly IParagraphBreaks _breaks = breaks ?? new ParagraphBreaks();

    public RichText Apply(RichText text, string edited)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edited);

        var paragraphs = text.Paragraphs.Count > 0 ? text.Paragraphs : [new TextParagraph(string.Empty)];
        var tokens = Tokens(paragraphs);
        var target = _breaks.Normalize(edited);

        var prefix = 0;
        while (prefix < tokens.Count && prefix < target.Length && tokens[prefix].Char == target[prefix]) prefix++;

        var suffix = 0;
        while (suffix < tokens.Count - prefix && suffix < target.Length - prefix
            && tokens[tokens.Count - 1 - suffix].Char == target[target.Length - 1 - suffix]) suffix++;

        if (prefix == tokens.Count && prefix == target.Length) return text;

        var inserted = Inserted(tokens, prefix, suffix, target);
        var merged = new List<Token>(prefix + inserted.Count + suffix);
        merged.AddRange(tokens.Take(prefix));
        merged.AddRange(inserted);
        merged.AddRange(tokens.Skip(tokens.Count - suffix));

        return text with { Paragraphs = Rebuild(merged, paragraphs, Broken(tokens, prefix, suffix)) };
    }

    private List<Token> Tokens(IReadOnlyList<TextParagraph> paragraphs)
    {
        var tokens = new List<Token>();
        var group = 0;
        for (var index = 0; index < paragraphs.Count; index++)
        {
            foreach (var run in paragraphs[index].EffectiveRuns())
            {
                var style = run with { Text = string.Empty };
                var id = run.Field == TextFieldKind.None ? -1 : group++;
                foreach (var c in run.Text) tokens.Add(new Token(c, style, index, id));
            }

            if (index < paragraphs.Count - 1) tokens.Add(new Token('\n', null, index, -1));
        }

        return tokens;
    }

    private List<Token> Inserted(List<Token> tokens, int prefix, int suffix, string target)
    {
        var before = prefix > 0 ? tokens[prefix - 1] : null;
        var after = suffix > 0 ? tokens[tokens.Count - suffix] : null;
        var style = before is { Style: { } left } ? left : after?.Style;
        var paragraph = before?.Paragraph ?? after?.Paragraph ?? 0;
        if (style is not null) style = style with { Field = TextFieldKind.None };

        var inserted = new List<Token>(target.Length - prefix - suffix);
        foreach (var c in target.AsSpan(prefix, target.Length - prefix - suffix))
            inserted.Add(new Token(c, c == '\n' ? null : style, paragraph, -1) { Fresh = true });

        return inserted;
    }

    private HashSet<int> Broken(List<Token> tokens, int prefix, int suffix)
    {
        var broken = new HashSet<int>();
        for (var index = prefix; index < tokens.Count - suffix; index++)
            if (tokens[index].Field >= 0) broken.Add(tokens[index].Field);

        return broken;
    }

    private List<TextParagraph> Rebuild(List<Token> tokens, IReadOnlyList<TextParagraph> source, HashSet<int> broken)
    {
        var result = new List<TextParagraph>();
        var shape = source[0];
        var origin = 0;
        var pending = new List<Token>();
        var fallback = shape.Runs.Count > 0 ? shape.Runs[0] with { Text = string.Empty } : shape.SingleRun() with { Text = string.Empty };

        foreach (var token in tokens)
        {
            if (token.Char != '\n')
            {
                pending.Add(token);
                continue;
            }

            result.Add(Paragraph(shape, origin, source, pending, broken, fallback));
            fallback = pending.Count > 0 ? pending[^1].Style ?? fallback : fallback;
            pending = [];
            if (!token.Fresh && token.Paragraph + 1 < source.Count)
            {
                origin = token.Paragraph + 1;
                shape = source[origin];
            }
        }

        result.Add(Paragraph(shape, origin, source, pending, broken, fallback));
        return result;
    }

    private TextParagraph Paragraph(
        TextParagraph shape, int origin, IReadOnlyList<TextParagraph> source, List<Token> tokens, HashSet<int> broken, TextRun fallback)
    {
        var pieces = new List<(TextRun Style, int Field, System.Text.StringBuilder Text)>();
        foreach (var token in tokens)
        {
            var style = token.Style ?? fallback;
            var field = token.Field >= 0 && !broken.Contains(token.Field) ? token.Field : -1;
            if (field < 0 && style.Field != TextFieldKind.None) style = style with { Field = TextFieldKind.None };
            style = style with { Text = string.Empty };

            if (pieces.Count > 0 && pieces[^1].Field == field && (field >= 0 || style == pieces[^1].Style))
                pieces[^1].Text.Append(token.Char);
            else
                pieces.Add((style, field, new System.Text.StringBuilder().Append(token.Char)));
        }

        var runs = pieces.Select(piece => piece.Style with { Text = piece.Text.ToString() }).ToList();

        if (runs.Count == 0)
            return shape with { Text = string.Empty, Runs = source[origin].Runs.Count > 0 ? [fallback with { Text = string.Empty, Field = TextFieldKind.None }] : [] };

        var plain = shape.SingleRun() with { Text = string.Empty };
        if (shape.Runs.Count == 0 && runs.All(run => run with { Text = string.Empty } == plain))
            return shape with { Text = string.Concat(runs.Select(run => run.Text)), Runs = [] };

        return shape.WithRuns(runs);
    }

    private sealed record Token(char Char, TextRun? Style, int Paragraph, int Field)
    {
        public bool Fresh { get; init; }
    }
}
