using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal interface IDrawingFieldTypes
{
    string TypeOf(TextRun run);

    TextFieldKind KindOf(string? type);

    FieldDetail? DetailOf(string? type);
}

internal sealed class DrawingFieldTypes(IDateFieldFormats dates) : IDrawingFieldTypes
{
    private readonly IReadOnlyList<(TextFieldKind Kind, string Type)> _written =
    [
        (TextFieldKind.SlideNumber, "slidenum"),
        (TextFieldKind.SlideCount, "pulseoffice.slidecount"),
        (TextFieldKind.SlideTitle, "pulseoffice.slidetitle"),
        (TextFieldKind.Author, "pulseoffice.author"),
        (TextFieldKind.FileName, "pulseoffice.filename"),
    ];

    public DrawingFieldTypes() : this(new DateFieldFormats())
    {
    }

    public string TypeOf(TextRun run) =>
        run.Field is TextFieldKind.Date or TextFieldKind.Time
            ? dates.TypeOf(run.Field, run.FieldDetail?.Format)
            : _written.FirstOrDefault(entry => entry.Kind == run.Field).Type ?? "slidenum";

    public TextFieldKind KindOf(string? type) => type switch
    {
        null => TextFieldKind.None,
        "datetime10" or "datetime11" or "datetime12" or "datetime13" => TextFieldKind.Time,
        _ when type.StartsWith("datetime", StringComparison.Ordinal) => TextFieldKind.Date,
        _ => _written.FirstOrDefault(entry => entry.Type == type).Kind,
    };

    public FieldDetail? DetailOf(string? type) =>
        type is not null && dates.PatternOf(type) is { } pattern ? new FieldDetail { Format = pattern } : null;
}
