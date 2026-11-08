using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.References;

namespace PulseOffice.Sheets.UI.Dialogs;

public enum ReferenceShape { Cell, Range, RangeList }

public interface ISheetReferenceReader
{
    IReadOnlyList<SheetRangeReference>? Read(Workbook workbook, string text, int currentSheet, ReferenceShape shape);

    CellRange? ReadLocal(Workbook workbook, string text, int currentSheet, ReferenceShape shape);
}

public sealed class SheetReferenceReader(ISheetRangeReferences references) : ISheetReferenceReader
{
    public IReadOnlyList<SheetRangeReference>? Read(Workbook workbook, string text, int currentSheet, ReferenceShape shape)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var parts = shape == ReferenceShape.RangeList
            ? (text ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [(text ?? string.Empty).Trim()];

        if (parts.Length == 0 || parts.Any(part => part.Length == 0)) return null;

        var read = new List<SheetRangeReference>();

        foreach (var part in parts)
        {
            if (references.Parse(workbook, part, currentSheet) is not { } reference) return null;
            if (shape == ReferenceShape.Cell && reference.Range.CellCount != 1) return null;
            read.Add(reference);
        }

        return read;
    }

    public CellRange? ReadLocal(Workbook workbook, string text, int currentSheet, ReferenceShape shape) =>
        Read(workbook, text, currentSheet, shape) is [var reference] && reference.SheetIndex == currentSheet
            ? reference.Range
            : null;
}

public sealed class SheetReferenceSource(
    Workbook workbook, int currentSheet, ReferenceShape shape, ISheetReferenceReader reader, IReferencePicker? picker)
    : IReferenceSource
{
    public bool IsValid(string text) => reader.Read(workbook, text, currentSheet, shape) is not null;

    public IReferencePicker? Picker { get; } = picker;
}

public interface ISheetReferenceSources
{
    IReferenceSource For(ReferenceShape shape);

    IReferencePicker Picker(ReferenceShape shape);

    IReferenceSource ForExpression();
}

public sealed class ExpressionReferenceSource(IReferencePicker picker) : IReferenceSource
{
    public bool IsValid(string text) => !string.IsNullOrWhiteSpace(text);

    public IReferencePicker? Picker { get; } = picker;
}

public sealed class SheetReferenceSources(
    IActiveSheetContext context, ISheetReferenceReader reader, ISheetRangeReferences references, IReferencePickers pickers)
    : ISheetReferenceSources
{
    public IReferenceSource For(ReferenceShape shape) => new SheetReferenceSource(
        context.Workbook ?? new Workbook(), context.SheetIndex, shape, reader, Picker(shape));

    public IReferencePicker Picker(ReferenceShape shape) =>
        pickers.For(new SheetReferencePickTarget(context, references, shape));

    public IReferenceSource ForExpression() => new ExpressionReferenceSource(
        pickers.For(new SheetReferencePickTarget(context, references, ReferenceShape.Range, ReferenceQualification.Always)));
}
