using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.Scripting;

public interface IScriptReferences
{
    CellAddress Address(string? reference);

    CellRange Range(string? reference);

    CellRange? Scope(string? reference);
}

public sealed class ScriptReferences : IScriptReferences
{
    public CellAddress Address(string? reference) =>
        reference is { Length: > 0 } && CellAddress.TryParseA1(reference, out var address)
            ? address
            : throw new ArgumentException($"'{reference}' is not a cell reference.", nameof(reference));

    public CellRange Range(string? reference) =>
        reference is { Length: > 0 } && CellRange.TryParseA1(reference, out var range)
            ? range
            : throw new ArgumentException($"'{reference}' is not a cell range.", nameof(reference));

    public CellRange? Scope(string? reference) => reference is not { Length: > 0 } ? null : Range(reference);
}

public interface IScriptColors
{
    string Hex(string? hex);

    string? OptionalHex(string? hex);
}

public sealed class ScriptColors : IScriptColors
{
    public string Hex(string? hex) =>
        hex is { Length: 7 } && hex[0] == '#' && hex.Skip(1).All(Uri.IsHexDigit)
            ? hex.ToUpperInvariant()
            : throw new ArgumentException($"'{hex}' is not a colour: write it as #RRGGBB.", nameof(hex));

    public string? OptionalHex(string? hex) => hex is not { Length: > 0 } ? null : Hex(hex);
}
