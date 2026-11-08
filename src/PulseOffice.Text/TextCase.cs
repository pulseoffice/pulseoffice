using System.Globalization;
using System.Text;

namespace PulseOffice.Text;

public enum TextCaseKind { Upper, Lower, Sentence, Title, Toggle, HalfWidth, FullWidth, Hiragana, Katakana }

public interface ITextCaseTransform
{
    TextCaseKind Kind { get; }

    string Apply(string text);

    bool Describes(string sample);
}

public abstract class TextCaseTransform : ITextCaseTransform
{
    public abstract TextCaseKind Kind { get; }

    protected CultureInfo Culture => CultureInfo.CurrentCulture;

    public string Apply(string text) => string.IsNullOrEmpty(text) ? text ?? string.Empty : Convert(text);

    public virtual bool Describes(string sample) => sample.Any(char.IsLetter) && Apply(sample) == sample;

    protected abstract string Convert(string text);
}

public sealed class UpperCase : TextCaseTransform
{
    public override TextCaseKind Kind => TextCaseKind.Upper;

    protected override string Convert(string text) => text.ToUpper(Culture);
}

public sealed class LowerCase : TextCaseTransform
{
    public override TextCaseKind Kind => TextCaseKind.Lower;

    protected override string Convert(string text) => text.ToLower(Culture);
}

public sealed class SentenceCase : TextCaseTransform
{
    public override TextCaseKind Kind => TextCaseKind.Sentence;

    protected override string Convert(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var start = true;
        var afterStop = false;

        foreach (var rune in text.ToLower(Culture).EnumerateRunes())
        {
            var written = rune;

            if (Rune.IsLetter(rune))
            {
                if (start) written = Rune.ToUpper(rune, Culture);
                start = false;
                afterStop = false;
            }
            else if (rune.Value is '.' or '!' or '?' or '…')
            {
                afterStop = true;
            }
            else if (rune.Value is '\n' or '\r' or '\u2029')
            {
                start = true;
                afterStop = false;
            }
            else if (Rune.IsWhiteSpace(rune))
            {
                start |= afterStop;
            }
            else if (Rune.IsDigit(rune))
            {
                start = false;
                afterStop = false;
            }

            builder.Append(written.ToString());
        }

        return builder.ToString();
    }
}

public sealed class TitleCase : TextCaseTransform
{
    public override TextCaseKind Kind => TextCaseKind.Title;

    protected override string Convert(string text) => Culture.TextInfo.ToTitleCase(text.ToLower(Culture));
}

public sealed class ToggleCase : TextCaseTransform
{
    public override TextCaseKind Kind => TextCaseKind.Toggle;

    public override bool Describes(string sample) => false;

    protected override string Convert(string text) =>
        string.Concat(text.EnumerateRunes().Select(rune => (Rune.IsUpper(rune)
            ? Rune.ToLower(rune, Culture)
            : Rune.ToUpper(rune, Culture)).ToString()));
}

public abstract class CharacterMapping : TextCaseTransform
{
    public override bool Describes(string sample) => false;

    protected override string Convert(string text) => new([.. text.Select(Map)]);

    protected abstract char Map(char character);
}

public sealed class HalfWidth : CharacterMapping
{
    public override TextCaseKind Kind => TextCaseKind.HalfWidth;

    protected override char Map(char character) => character switch
    {
        '\u3000' => ' ',
        >= '\uFF01' and <= '\uFF5E' => (char)(character - 0xFEE0),
        _ => character,
    };
}

public sealed class FullWidth : CharacterMapping
{
    public override TextCaseKind Kind => TextCaseKind.FullWidth;

    protected override char Map(char character) => character switch
    {
        ' ' => '\u3000',
        >= '!' and <= '~' => (char)(character + 0xFEE0),
        _ => character,
    };
}

public sealed class Hiragana : CharacterMapping
{
    public override TextCaseKind Kind => TextCaseKind.Hiragana;

    protected override char Map(char character) => character is >= '\u30A1' and <= '\u30F6' ? (char)(character - 0x60) : character;
}

public sealed class Katakana : CharacterMapping
{
    public override TextCaseKind Kind => TextCaseKind.Katakana;

    protected override char Map(char character) => character is >= '\u3041' and <= '\u3096' ? (char)(character + 0x60) : character;
}

public interface ITextCases
{
    ITextCaseTransform this[TextCaseKind kind] { get; }

    ITextCaseTransform NextInCycle(string sample);
}

public sealed class TextCases : ITextCases
{
    private readonly IReadOnlyDictionary<TextCaseKind, ITextCaseTransform> _transforms;

    private readonly IReadOnlyList<TextCaseKind> _cycle =
    [
        TextCaseKind.Upper,
        TextCaseKind.Lower,
        TextCaseKind.Title,
        TextCaseKind.Sentence,
    ];

    public TextCases(IEnumerable<ITextCaseTransform> transforms)
    {
        _transforms = transforms.ToDictionary(transform => transform.Kind);
        var missing = Enum.GetValues<TextCaseKind>().Where(kind => !_transforms.ContainsKey(kind)).ToList();
        if (missing.Count > 0)
            throw new ArgumentException($"Missing text case transforms: {string.Join(", ", missing)}", nameof(transforms));
    }

    public TextCases()
        : this([new UpperCase(), new LowerCase(), new SentenceCase(), new TitleCase(), new ToggleCase(), new HalfWidth(), new FullWidth(), new Hiragana(), new Katakana()])
    {
    }

    public ITextCaseTransform this[TextCaseKind kind] => _transforms[kind];

    public ITextCaseTransform NextInCycle(string sample)
    {
        var text = sample ?? string.Empty;
        var current = -1;

        for (var i = 0; i < _cycle.Count && current < 0; i++)
            if (_transforms[_cycle[i]].Describes(text)) current = i;

        for (var step = 1; step <= _cycle.Count; step++)
        {
            var candidate = _transforms[_cycle[(Math.Max(current, -1) + step + _cycle.Count) % _cycle.Count]];
            if (candidate.Apply(text) != text) return candidate;
        }

        return _transforms[TextCaseKind.Upper];
    }
}
