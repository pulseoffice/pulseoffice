using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Formats;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class FormatLookup(
    CalculationEngine engine, SheetCell origin, Evaluator evaluator, Evaluation.Functions.ValueCoercion coercion, IFormatHints hints) : IFormatLookup
{
    private readonly int _defaultSheet = origin.SheetIndex;

    public NumberFormatCatalog Formats => engine.Workbook.NumberFormats;

    public FormatHint LastIn(string? sheetName, CellRange range)
    {
        var index = engine.ResolveSheet(sheetName, _defaultSheet);
        if (engine.SheetAt(index) is not { } sheet) return FormatHint.None;
        for (var row = Math.Min(range.End.Row, sheet.LastUsedRow); row >= range.Start.Row; row--)
            for (var column = Math.Min(range.End.Column, sheet.LastUsedColumn); column >= range.Start.Column; column--)
            {
                var address = new CellAddress(row, column);
                if (sheet.TryGetCell(address, out var cell) && !cell.Value.IsEmpty) return At(sheetName, address);
            }
        return FormatHint.None;
    }

    public CellValue? Evaluate(FormulaNode node)
    {
        var context = new WorkbookEvaluationContext(engine, origin);
        return coercion.ToScalar(evaluator.EvaluateRaw(node, context), context);
    }

    public FormatHint At(string? sheetName, CellAddress address)
    {
        var index = engine.ResolveSheet(sheetName, _defaultSheet);
        if (engine.SheetAt(index) is not { } sheet) return FormatHint.None;
        var formats = engine.Workbook.NumberFormats;
        var own = engine.Workbook.Styles.Get(sheet.StyleIdAt(address)).NumberFormatId;
        if (own != 0) return hints.Of(own, formats);
        return sheet.TryGetCell(address, out var target) && target.HasFormula ? hints.Of(target.InferredFormatId, formats) : FormatHint.None;
    }

    public FormulaNode? Expand(NameNode name)
    {
        if (engine.Workbook.ResolveName(name.Name, engine.SheetAt(_defaultSheet)?.Name)?.Expression is not { } expression) return null;
        try { return evaluator.Names.Expand(expression, origin.Address); }
        catch (FormulaSyntaxException) { return null; }
    }
}
