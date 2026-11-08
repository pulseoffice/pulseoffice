using System.Globalization;

namespace PulseOffice.Sheets.Values;

public enum ValueKind { Empty, Number, Text, Boolean, Error }

public enum CellError
{
    None = 0,
    Null,
    DivideByZero,
    Value,
    Reference,
    Name,
    Number,
    NotAvailable,
    Circular,
    Spill,
    Calc,
}

public readonly struct CellValue : IEquatable<CellValue>
{
    private readonly double _number;
    private readonly string? _text;

    private CellValue(ValueKind kind, double number, string? text, CellError error)
    {
        Kind = kind;
        _number = number;
        _text = text;
        Error = error;
    }

    public ValueKind Kind { get; }
    public CellError Error { get; }

    public static readonly CellValue Empty = new(ValueKind.Empty, 0, null, CellError.None);
    public static readonly CellValue True = new(ValueKind.Boolean, 1, null, CellError.None);
    public static readonly CellValue False = new(ValueKind.Boolean, 0, null, CellError.None);

    public static CellValue Number(double value) => new(ValueKind.Number, value, null, CellError.None);
    public static CellValue Text(string value) => new(ValueKind.Text, 0, value, CellError.None);
    public static CellValue Boolean(bool value) => value ? True : False;
    public static CellValue FromError(CellError error) => new(ValueKind.Error, 0, null, error);

    public bool IsEmpty => Kind == ValueKind.Empty;
    public bool IsError => Kind == ValueKind.Error;
    public bool IsNumeric => Kind is ValueKind.Number or ValueKind.Boolean;

    public double AsNumber() => _number;
    public string AsText() => _text ?? string.Empty;
    public bool AsBoolean() => _number != 0;

    public bool TryCoerceToNumber(out double result)
    {
        switch (Kind)
        {
            case ValueKind.Number or ValueKind.Boolean:
                result = _number;
                return true;
            case ValueKind.Empty:
                result = 0;
                return true;
            case ValueKind.Text:
                return double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                result = 0;
                return false;
        }
    }

    public string ErrorText => Error switch
    {
        CellError.Null => "#NULL!",
        CellError.DivideByZero => "#DIV/0!",
        CellError.Value => "#VALUE!",
        CellError.Reference => "#REF!",
        CellError.Name => "#NAME?",
        CellError.Number => "#NUM!",
        CellError.NotAvailable => "#N/A",
        CellError.Circular => "#VALUE!",
        CellError.Spill => "#SPILL!",
        CellError.Calc => "#CALC!",
        _ => string.Empty,
    };

    public bool Equals(CellValue other) =>
        Kind == other.Kind && Error == other.Error &&
        _number.Equals(other._number) && string.Equals(_text, other._text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CellValue v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(Kind, _number, _text, Error);
    public static bool operator ==(CellValue a, CellValue b) => a.Equals(b);
    public static bool operator !=(CellValue a, CellValue b) => !a.Equals(b);

    public override string ToString() => Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Number => _number.ToString(CultureInfo.InvariantCulture),
        ValueKind.Text => _text ?? string.Empty,
        ValueKind.Boolean => AsBoolean() ? "TRUE" : "FALSE",
        ValueKind.Error => ErrorText,
        _ => string.Empty,
    };
}
