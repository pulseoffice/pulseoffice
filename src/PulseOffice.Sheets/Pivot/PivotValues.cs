using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public static class PivotValues
{
    public static string KeyOf(CellValue value) => value.Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Text => TextKeyOf(value.AsText()),
        ValueKind.Boolean => value.AsBoolean() ? TrueKey : FalseKey,
        ValueKind.Error => value.ErrorText,
        _ => value.AsNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
    };

    public const string TextMarker = "\u2060";

    public static string TextKeyOf(string text) => ReadsAsAnotherKind(text) ? TextMarker + text : text;

    public static string NameOf(string key) => key.StartsWith(TextMarker, StringComparison.Ordinal) ? key[TextMarker.Length..] : key;

    public static bool IsMarkedText(string key) => key.StartsWith(TextMarker, StringComparison.Ordinal);

    private static bool ReadsAsAnotherKind(string text) =>
        text is TrueKey or FalseKey
        || text.StartsWith(TextMarker, StringComparison.Ordinal)
        || (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            && string.Equals(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal));

    public const string TrueKey = "TRUE";

    public const string FalseKey = "FALSE";

    public static bool? BooleanOf(string key) => key switch
    {
        TrueKey => true,
        FalseKey => false,
        _ => null,
    };
}
