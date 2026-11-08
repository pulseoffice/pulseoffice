using System.Globalization;

namespace PulseOffice.Text.Rich;

public readonly record struct TextFields(
    int Number, DateTime Now, int Count = 0, string Title = "", string Author = "", string FileName = "", string? Language = null);

public interface IFieldClockFormatter
{
    string Format(TextFieldKind kind, DateTime moment, string? pattern, CultureInfo culture);
}

public sealed class FieldClockFormatter : IFieldClockFormatter
{
    public string Format(TextFieldKind kind, DateTime moment, string? pattern, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var fallback = kind == TextFieldKind.Time ? "t" : "d";
        if (string.IsNullOrEmpty(pattern)) return moment.ToString(fallback, culture);
        try
        {
            return moment.ToString(pattern, culture);
        }
        catch (FormatException)
        {
            return moment.ToString(fallback, culture);
        }
    }
}

public sealed class TextFieldResolver
{
    private readonly ITextCultures _cultures;
    private readonly IReadOnlyDictionary<TextFieldKind, Func<TextFields, string?, CultureInfo, string>> _values;

    public TextFieldResolver() : this(new FieldClockFormatter(), new TextCultures())
    {
    }

    public TextFieldResolver(IFieldClockFormatter clock, ITextCultures cultures)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _cultures = cultures ?? throw new ArgumentNullException(nameof(cultures));
        _values = new Dictionary<TextFieldKind, Func<TextFields, string?, CultureInfo, string>>
        {
            [TextFieldKind.SlideNumber] = (fields, _, culture) => fields.Number.ToString(culture),
            [TextFieldKind.Date] = (fields, format, culture) => clock.Format(TextFieldKind.Date, fields.Now, format, culture),
            [TextFieldKind.Time] = (fields, format, culture) => clock.Format(TextFieldKind.Time, fields.Now, format, culture),
            [TextFieldKind.SlideCount] = (fields, _, culture) => fields.Count.ToString(culture),
            [TextFieldKind.SlideTitle] = (fields, _, _) => fields.Title,
            [TextFieldKind.Author] = (fields, _, _) => fields.Author,
            [TextFieldKind.FileName] = (fields, _, _) => fields.FileName,
        };
    }

    public string Resolve(TextFieldKind kind, TextFields fields) => Resolve(kind, fields, null, null);

    public string Resolve(TextRun run, TextFields fields) => Resolve(run.Field, fields, run.FieldDetail?.Format, run.Language);

    private string Resolve(TextFieldKind kind, TextFields fields, string? format, string? language) =>
        _values.TryGetValue(kind, out var value) ? value(fields, format, _cultures.For(language, fields.Language)) : string.Empty;
}

public sealed record DateFieldFormat(string Type, TextFieldKind Kind, string Pattern, bool Exact = true);

public interface IDateFieldFormats
{
    string TypeOf(TextFieldKind kind, string? pattern);

    string? PatternOf(string type);

    bool Reaches(TextFieldKind kind, string? pattern);
}

public sealed class DateFieldFormats : IDateFieldFormats
{
    private readonly IReadOnlyList<DateFieldFormat> _table =
    [
        new("datetime1", TextFieldKind.Date, "d"),
        new("datetime2", TextFieldKind.Date, "D"),
        new("datetime3", TextFieldKind.Date, "d' 'MMMM' 'yyyy"),
        new("datetime4", TextFieldKind.Date, "MMMM' 'd', 'yyyy"),
        new("datetime5", TextFieldKind.Date, "d'-'MMM'-'yy"),
        new("datetime6", TextFieldKind.Date, "MMMM' 'yy"),
        new("datetime7", TextFieldKind.Date, "MMM'-'yy"),
        new("datetime8", TextFieldKind.Date, "MM'/'dd'/'yyyy' 'h':'mm tt"),
        new("datetime9", TextFieldKind.Date, "MM'/'dd'/'yyyy' 'h':'mm':'ss tt"),
        new("datetime10", TextFieldKind.Time, "t"),
        new("datetime11", TextFieldKind.Time, "T"),
        new("datetime12", TextFieldKind.Time, "h':'mm tt"),
        new("datetime13", TextFieldKind.Time, "h':'mm':'ss tt"),
        new("datetime", TextFieldKind.Date, "d", Exact: false),
        new("datetimeFigureOut", TextFieldKind.Date, "d", Exact: false),
        new("datetime1", TextFieldKind.Date, "dd'.'MM'.'yy", Exact: false),
        new("datetime1", TextFieldKind.Date, "dd'.'MM'.'yyyy", Exact: false),
        new("datetime5", TextFieldKind.Date, "d'. 'MMM' 'yyyy", Exact: false),
        new("datetime3", TextFieldKind.Date, "d'. 'MMMM' 'yyyy", Exact: false),
        new("datetime2", TextFieldKind.Date, "ddd', 'd'. 'MMMM' 'yyyy", Exact: false),
        new("datetime2", TextFieldKind.Date, "dddd', 'd'. 'MMMM' 'yyyy", Exact: false),
        new("datetime10", TextFieldKind.Time, "H':'mm", Exact: false),
        new("datetime11", TextFieldKind.Time, "H':'mm':'ss", Exact: false),
        new("datetime11", TextFieldKind.Time, "H':'mm':'ss.ff", Exact: false),
        new("datetime13", TextFieldKind.Time, "h':'mm':'ss.ff tt", Exact: false),
    ];

    public string TypeOf(TextFieldKind kind, string? pattern) =>
        Row(kind, pattern)?.Type ?? Default(kind).Type;

    public string? PatternOf(string type)
    {
        var row = _table.FirstOrDefault(entry => entry.Type == type && entry.Exact) ?? _table.FirstOrDefault(entry => entry.Type == type);
        return row is null || row.Pattern == Default(row.Kind).Pattern ? null : row.Pattern;
    }

    public bool Reaches(TextFieldKind kind, string? pattern) =>
        string.IsNullOrEmpty(pattern) || Row(kind, pattern) is { Exact: true };

    private DateFieldFormat? Row(TextFieldKind kind, string? pattern) =>
        string.IsNullOrEmpty(pattern)
            ? Default(kind)
            : _table.FirstOrDefault(entry => entry.Pattern == pattern && entry.Exact)
              ?? _table.FirstOrDefault(entry => entry.Pattern == pattern);

    private DateFieldFormat Default(TextFieldKind kind) =>
        _table.First(entry => entry.Exact && entry.Kind == (kind == TextFieldKind.Time ? TextFieldKind.Time : TextFieldKind.Date));
}
