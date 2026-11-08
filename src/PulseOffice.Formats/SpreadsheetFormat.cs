using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets;

namespace PulseOffice.Formats;

[Flags]
public enum FormatCapabilities
{
    None = 0,
    Formulas = 1 << 0,
    MultipleSheets = 1 << 1,
    CellFormatting = 1 << 2,
    ConditionalFormatting = 1 << 3,
    Charts = 1 << 4,
    PivotTables = 1 << 5,
    Macros = 1 << 6,
    DynamicArrays = 1 << 7,
    QueryBindings = 1 << 8,

    PlainTabular = None,
}

public sealed record SpreadsheetFormat : IDocumentFormat
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Extensions { get; init; }
    public required string MimeType { get; init; }
    public required FormatCapabilities Capabilities { get; init; }

    public bool IsNative { get; init; }

    public bool CanRead { get; init; } = true;
    public bool CanWrite { get; init; } = true;

    public bool Supports(FormatCapabilities capability) => (Capabilities & capability) == capability;

    public bool Matches(string path) =>
        Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}

public sealed record FidelityReport(IReadOnlyList<Diagnostic> Losses)
{
    public static readonly FidelityReport Perfect = new([]);

    public bool IsPerfect => Losses.Count == 0;
}

public interface ISpreadsheetReader : IDocumentReader<Workbook, SpreadsheetFormat>;

public interface ISpreadsheetWriter : IDocumentWriter<Workbook, SpreadsheetFormat>;
