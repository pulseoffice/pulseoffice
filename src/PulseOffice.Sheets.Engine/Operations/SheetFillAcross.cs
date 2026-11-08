using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

[Flags]
public enum FillAcrossContent
{
    None = 0,
    Text = 1,
    Numbers = 2,
    Formulas = 4,
    Formats = 8,
    All = Text | Numbers | Formulas | Formats,
}

public sealed record FillAcrossOptions(FillAcrossContent Content, bool SkipEmpty = false)
{
    public static FillAcrossOptions All { get; } = new(FillAcrossContent.All);
}

public interface ISheetFillAcross
{
    int Fill(CalculationEngine engine, Workbook workbook, int sourceIndex, CellRange range, IEnumerable<int> targets, FillAcrossOptions options);
}

public abstract class FillAcrossPart(FillAcrossContent flag)
{
    public FillAcrossContent Flag { get; } = flag;

    public abstract bool Owns(Cell cell);

    public abstract void Write(CalculationEngine engine, SheetCell target, Cell source);
}

public sealed class TextFillPart() : FillAcrossPart(FillAcrossContent.Text)
{
    public override bool Owns(Cell cell) => !cell.HasFormula && cell.Value.Kind == ValueKind.Text;

    public override void Write(CalculationEngine engine, SheetCell target, Cell source) => engine.SetValue(target, source.Value);
}

public sealed class NumberFillPart() : FillAcrossPart(FillAcrossContent.Numbers)
{
    public override bool Owns(Cell cell) =>
        !cell.HasFormula && cell.Value.Kind is ValueKind.Number or ValueKind.Boolean or ValueKind.Error;

    public override void Write(CalculationEngine engine, SheetCell target, Cell source) => engine.SetValue(target, source.Value);
}

public sealed class FormulaFillPart() : FillAcrossPart(FillAcrossContent.Formulas)
{
    public override bool Owns(Cell cell) => cell.HasFormula && !cell.IsPartOfArrayFormula;

    public override void Write(CalculationEngine engine, SheetCell target, Cell source) => engine.SetFormula(target, source.Formula!);
}

public sealed class SheetFillAcross : ISheetFillAcross
{
    private readonly IReadOnlyList<FillAcrossPart> _parts = [new TextFillPart(), new NumberFillPart(), new FormulaFillPart()];

    public int Fill(CalculationEngine engine, Workbook workbook, int sourceIndex, CellRange range, IEnumerable<int> targets, FillAcrossOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(options);

        var source = workbook.Sheets[sourceIndex];
        var selected = _parts.Where(part => options.Content.HasFlag(part.Flag)).ToList();
        var formats = options.Content.HasFlag(FillAcrossContent.Formats);
        var filled = 0;

        foreach (var index in targets.Distinct().Where(index => index != sourceIndex && index >= 0 && index < workbook.Sheets.Count && !workbook.Sheets[index].Protection.IsEnabled))
        {
            var target = workbook.Sheets[index];

            var addresses = source.NonEmptyCellsIn(range).Select(pair => pair.Key)
                .Union(target.NonEmptyCellsIn(range).Select(pair => pair.Key))
                .ToList();

            foreach (var address in addresses)
            {
                var cell = source[address];
                if (options.SkipEmpty && source.IsBlank(address)) continue;

                FillContent(engine, new SheetCell(index, address), target[address], cell, selected);

                if (formats) target.SetOwnStyle(address, source.OwnStyleAt(address));
            }

            filled++;
        }

        return filled;
    }

    private void FillContent(CalculationEngine engine, SheetCell at, Cell existing, Cell source, IReadOnlyList<FillAcrossPart> selected)
    {
        if (selected.Count == 0) return;

        if (selected.FirstOrDefault(part => part.Owns(source)) is { } owner)
        {
            owner.Write(engine, at, source);
            return;
        }

        if (selected.Any(part => part.Owns(existing))) engine.SetValue(at, CellValue.Empty);
    }
}
