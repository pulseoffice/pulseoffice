using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PulseOffice.Exchange;

public sealed class ExchangeScalarConverter : JsonConverter<object>
{
    private const string KindMember = "$scalar";
    private const string ValueMember = "Value";
    private const string DateKind = "DateTime";
    private const string OffsetKind = "DateTimeOffset";
    private const string DurationKind = "TimeSpan";
    private const string BytesKind = "Bytes";

    public override bool HandleNull => true;

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.StartObject => Tagged(ref reader),
            _ => throw new JsonException($"Unsupported exchange scalar token {reader.TokenType}"),
        };

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case double number:
                Number(writer, number);
                break;
            case float number:
                Number(writer, number);
                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                Number(writer, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case DateTime date:
                Tag(writer, DateKind, date.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset moment:
                Tag(writer, OffsetKind, moment.ToString("O", CultureInfo.InvariantCulture));
                break;
            case TimeSpan duration:
                Tag(writer, DurationKind, duration.ToString("c", CultureInfo.InvariantCulture));
                break;
            case byte[] bytes:
                Tag(writer, BytesKind, Convert.ToBase64String(bytes));
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private void Number(Utf8JsonWriter writer, double number)
    {
        if (double.IsFinite(number)) writer.WriteNumberValue(number);
        else writer.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
    }

    private void Tag(Utf8JsonWriter writer, string kind, string text)
    {
        writer.WriteStartObject();
        writer.WriteString(KindMember, kind);
        writer.WriteString(ValueMember, text);
        writer.WriteEndObject();
    }

    private object Tagged(ref Utf8JsonReader reader)
    {
        string? kind = null;
        string? text = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Malformed exchange scalar");
            var name = reader.GetString();
            if (!reader.Read() || reader.TokenType != JsonTokenType.String) throw new JsonException("Malformed exchange scalar");
            if (name == KindMember) kind = reader.GetString();
            else if (name == ValueMember) text = reader.GetString();
        }
        if (text is null) throw new JsonException("Exchange scalar without a value");
        object? parsed = kind switch
        {
            DateKind when DateTime.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) => date,
            OffsetKind when DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment) => moment,
            DurationKind when TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out var duration) => duration,
            BytesKind => Bytes(text),
            _ => null,
        };
        return parsed ?? throw new JsonException($"Invalid exchange scalar of kind {kind}");
    }

    private byte[]? Bytes(string text)
    {
        var buffer = new byte[text.Length * 3 / 4 + 3];
        return Convert.TryFromBase64String(text, buffer, out var written) ? buffer[..written] : null;
    }
}
