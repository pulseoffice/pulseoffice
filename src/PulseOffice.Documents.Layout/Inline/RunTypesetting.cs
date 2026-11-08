using System.Globalization;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Inline;

public sealed record TypesetSpan(int Start, int End, FontKey Font);

public sealed record TypesetRun(string Text, IReadOnlyList<TypesetSpan> Spans, double[] Advances);

public interface ITextCaseMapping
{
    string Map(string text, TextCase textCase, string? language);
}

public sealed class TextCaseMapping : ITextCaseMapping
{
    public string Map(string text, TextCase textCase, string? language)
    {
        if (textCase == TextCase.None || text.Length == 0) return text;
        var culture = Culture(language);
        var characters = text.ToCharArray();
        for (var i = 0; i < characters.Length; i++)
        {
            var character = characters[i];
            characters[i] = textCase switch
            {
                TextCase.Upper => char.ToUpper(character, culture),
                TextCase.Lower => char.ToLower(character, culture),
                _ => StartsWord(text, i) ? char.ToUpper(character, culture) : character,
            };
        }
        return new string(characters);
    }

    private bool StartsWord(string text, int index) =>
        char.IsLetter(text[index]) && (index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != '\'' && text[index - 1] != '’');

    private CultureInfo Culture(string? language)
    {
        if (string.IsNullOrEmpty(language)) return CultureInfo.InvariantCulture;
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}

public interface IRunTypesetter
{
    TypesetRun Typeset(TextRun run, FontKey font);
}

public sealed class RunTypesetter(ITextMeasurer measurer, ITextCaseMapping cases, LayoutOptions options, IAsianScriptRanges scripts) : IRunTypesetter
{
    public TypesetRun Typeset(TextRun run, FontKey font)
    {
        var text = cases.Map(run.Text, run.Case ?? TextCase.None, run.Language);
        IReadOnlyList<TypesetSpan> spans = run.SmallCaps ? SmallCapitals(run.Text, ref text, font) : [new TypesetSpan(0, text.Length, font)];
        if (run.AsianFont is { Length: > 0 } asian) spans = Asian(spans, text, asian);
        var advances = new double[text.Length];
        foreach (var span in spans)
        {
            var measured = measurer.Advances(span.Font, text[span.Start..span.End]);
            for (var i = 0; i < measured.Count; i++) advances[span.Start + i] = measured[i];
        }
        Adjust(run, text, advances);
        return new TypesetRun(text, spans, advances);
    }

    private IReadOnlyList<TypesetSpan> Asian(IReadOnlyList<TypesetSpan> spans, string text, string asian) =>
        spans.SelectMany(span => scripts.Split(text, span.Start, span.End - span.Start)
            .Select(part => new TypesetSpan(part.Start, part.Start + part.Length, part.Asian ? span.Font with { Family = asian } : span.Font)))
            .ToList();

    private IReadOnlyList<TypesetSpan> SmallCapitals(string source, ref string text, FontKey font)
    {
        var characters = text.ToCharArray();
        var spans = new List<TypesetSpan>();
        var small = font.Scaled(options.SmallCapsScale);
        var start = 0;
        bool? lowered = null;
        for (var i = 0; i < characters.Length; i++)
        {
            var lower = char.IsLower(source[i]) && char.IsLower(characters[i]);
            if (lower) characters[i] = char.ToUpperInvariant(characters[i]);
            if (lowered is { } previous && previous != lower)
            {
                spans.Add(new TypesetSpan(start, i, previous ? small : font));
                start = i;
            }
            lowered = lower;
        }
        spans.Add(new TypesetSpan(start, characters.Length, lowered == true ? small : font));
        text = new string(characters);
        return spans;
    }

    private void Adjust(TextRun run, string text, double[] advances)
    {
        if (run.Hidden == true)
        {
            Array.Clear(advances);
            return;
        }
        var scale = run.ScaleWidth is { } percent && percent > 0 ? percent / 100 : 1;
        var spacing = run.LetterSpacing ?? 0;
        if (scale == 1 && spacing == 0) return;
        for (var i = 0; i < advances.Length; i++)
        {
            if (char.IsLowSurrogate(text[i]) || text[i] is '­' or '​' or '\t') continue;
            advances[i] = Math.Max(0, advances[i] * scale + spacing);
        }
    }
}
