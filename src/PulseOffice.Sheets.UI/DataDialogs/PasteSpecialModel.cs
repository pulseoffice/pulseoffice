using System.Globalization;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.DataDialogs;

public enum PastePart { All, Formulas, Values, Formats, Comments, Validation }

public sealed record PasteSpecialChoice(
    PastePart Part = PastePart.All,
    PasteOperation Operation = PasteOperation.None,
    bool SkipBlanks = false,
    bool Transpose = false,
    bool Link = false)
{
    public bool OperationApplies => Part is PastePart.All or PastePart.Formulas or PastePart.Values;

    public PasteRequest Request() => new()
    {
        Parts = Part switch
        {
            PastePart.Formulas => PasteParts.Formulas | PasteParts.Values,
            PastePart.Values => PasteParts.ValuesOnly,
            PastePart.Formats => PasteParts.StylesOnly,
            PastePart.Comments => PasteParts.Comments,
            PastePart.Validation => PasteParts.Validation,
            _ => PasteParts.Everything,
        },
        Operation = OperationApplies ? Operation : PasteOperation.None,
        SkipBlanks = SkipBlanks,
        Transpose = Transpose,
        Link = Link,
    };
}

public sealed record PasteSketchCell(string Text, bool Formula, bool Formatted, bool Comment, bool Validated)
{
    public static readonly PasteSketchCell Blank = new(string.Empty, false, false, false, false);
}

public sealed record PasteSketch(IReadOnlyList<IReadOnlyList<PasteSketchCell>> Source, IReadOnlyList<IReadOnlyList<PasteSketchCell>> Result);

public interface IPasteSketcher
{
    PasteSketch Sketch(PasteSpecialChoice choice);
}

public sealed class PasteSketcher : IPasteSketcher
{
    private const double Existing = 2;

    private readonly IReadOnlyList<IReadOnlyList<SampleCell>> _source =
    [
        [new(10, false, false, false, false), new(null, false, false, false, false), new(20, true, false, false, false)],
        [new(5, false, true, false, false), new(3, false, false, true, false), new(8, false, false, false, true)],
    ];

    public PasteSketch Sketch(PasteSpecialChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);

        var rows = _source.Count;
        var columns = _source[0].Count;
        var source = _source.Select(row => (IReadOnlyList<PasteSketchCell>)[.. row.Select(cell => cell.Shown())]).ToList();

        var (resultRows, resultColumns) = choice.Transpose ? (columns, rows) : (rows, columns);
        var result = new List<IReadOnlyList<PasteSketchCell>>();
        for (var row = 0; row < resultRows; row++)
        {
            var line = new List<PasteSketchCell>();
            for (var column = 0; column < resultColumns; column++)
            {
                var (sourceRow, sourceColumn) = choice.Transpose ? (column, row) : (row, column);
                line.Add(Pasted(_source[sourceRow][sourceColumn], choice));
            }

            result.Add(line);
        }

        return new PasteSketch(source, result);
    }

    private PasteSketchCell Pasted(SampleCell cell, PasteSpecialChoice choice)
    {
        var kept = new PasteSketchCell(Number(Existing), false, false, false, false);

        if (choice.Link) return new PasteSketchCell(Number(cell.Value ?? 0), true, false, false, false);
        if (choice.SkipBlanks && cell.Value is null) return kept;

        var values = choice.Part is PastePart.All or PastePart.Formulas or PastePart.Values;
        var text = values ? Combined(cell.Value, choice.Operation) : kept.Text;

        return new PasteSketchCell(
            text,
            Formula: cell.Formula && choice.Part is PastePart.All or PastePart.Formulas && choice.Operation == PasteOperation.None,
            Formatted: cell.Formatted && choice.Part is PastePart.All or PastePart.Formats,
            Comment: cell.Comment && choice.Part is PastePart.All or PastePart.Comments,
            Validated: cell.Validated && choice.Part is PastePart.All or PastePart.Validation);
    }

    private string Combined(double? value, PasteOperation operation)
    {
        if (value is not { } source) return operation == PasteOperation.None ? string.Empty : Number(Existing);

        return operation switch
        {
            PasteOperation.Add => Number(Existing + source),
            PasteOperation.Subtract => Number(Existing - source),
            PasteOperation.Multiply => Number(Existing * source),
            PasteOperation.Divide => Number(Math.Round(Existing / source, 2)),
            _ => Number(source),
        };
    }

    private string Number(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    private sealed record SampleCell(double? Value, bool Formula, bool Formatted, bool Comment, bool Validated)
    {
        public PasteSketchCell Shown() => new(
            Value is { } number ? number.ToString("0.##", CultureInfo.CurrentCulture) : string.Empty,
            Formula, Formatted, Comment, Validated);
    }
}
