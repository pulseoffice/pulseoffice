using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ReferenceFunctions(
    ReferenceText references,
    Localization.IFormulaLocalization localization,
    PulseOffice.Sheets.Formatting.ISpreadsheetLocale locale,
    PulseOffice.Platform.IHostSystem host) : IFunctionProvider
{
    public string Category => "Ricerca e riferimento";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .ArrayResult("ROW", 0, 1, r => Positions(r, wantRow: true), handlesErrors: true)
        .ArrayResult("COLUMN", 0, 1, r => Positions(r, wantRow: false), handlesErrors: true)

        .RawHandlingErrors("SHEET", 0, 1, r =>
        {
            if (!r.Has(0)) return CellValue.Number(r.Context.SheetIndex(null));

            var reference = AsReference(r, 0);
            var name = reference?.SheetName ?? r.Context.CurrentSheetName;
            var index = r.Context.SheetIndex(name);
            return index == 0 ? CellValue.FromError(CellError.Reference) : CellValue.Number(index);
        })

        .RawHandlingErrors("SHEETS", 0, 1, r => r.Has(0)
            ? AsReference(r, 0) is null ? CellValue.FromError(CellError.Reference) : CellValue.Number(1)
            : CellValue.Number(r.Context.SheetCount))

        .RawHandlingErrors("ISREF", 1, 1, r => CellValue.Boolean(AsReference(r, 0) is not null))

        .RawHandlingErrors("ISFORMULA", 1, 1, r =>
        {
            var reference = AsReference(r, 0);
            if (reference is null) return CellValue.FromError(CellError.Reference);
            return CellValue.Boolean(
                r.Context.GetFormulaText(reference.SheetName, reference.Range.Start) is not null);
        })

        .RawHandlingErrors("FORMULA", 1, 1, r =>
        {
            var reference = AsReference(r, 0);
            if (reference is null) return CellValue.FromError(CellError.Reference);

            var formula = r.Context.GetFormulaText(reference.SheetName, reference.Range.Start);

            return formula is null
                ? CellValue.FromError(CellError.NotAvailable)
                : CellValue.Text(Shown(formula));
        })

        .RawHandlingErrors("CELL", 1, 2, r =>
        {
            var kind = r.Text(0).ToLowerInvariant();
            if (r.HasError) return r.Error;

            var reference = r.Has(1) ? AsReference(r, 1) : null;
            var address = reference?.Range.Start ?? r.Context.CurrentCell.Address;
            var sheetName = reference?.SheetName ?? r.Context.CurrentSheetName;
            CellValue Value() => r.Context.GetCellValue(reference?.SheetName, address);

            return kind switch
            {
                "address" => CellValue.Text($"${CellAddress.ColumnName(address.Column)}${address.Row + 1}"),
                "col" => CellValue.Number(address.Column + 1),
                "row" => CellValue.Number(address.Row + 1),
                "sheet" => CellValue.Number(r.Context.SheetIndex(sheetName)),
                "filename" => CellValue.Text(string.Empty),
                "contents" => Value(),
                "type" => Value() is var content ? CellValue.Text(content.IsEmpty ? "b" : content.Kind == ValueKind.Text ? "l" : "v") : CellValue.Empty,
                "format" => CellValue.Text(r.Context.GetStyle(reference?.SheetName, address) == 0 ? "G" : "F"),
                "prefix" => CellValue.Text(string.Empty),
                "protect" => CellValue.Number(0),
                "width" => CellValue.Number(10),
                "color" or "parentheses" => CellValue.Number(0),
                _ => F.Invalid(),
            };
        })

        .RawHandlingErrors("INFO", 1, 1, r =>
        {
            var kind = r.Text(0).ToLowerInvariant();
            if (r.HasError) return r.Error;

            return kind switch
            {
                "numfile" => CellValue.Number(1),
                "recalc" => CellValue.Text("Automatic"),
                "system" => CellValue.Text(host.IsWindows ? "pcdos" : host.IsMacOS ? "mac" : "unix"),
                "release" => CellValue.Text("PulseOffice"),
                "osversion" => CellValue.Text(host.VersionString),
                "origin" => CellValue.Text("$A:$A$1"),
                "directory" => CellValue.Text(string.Empty),
                _ => F.Invalid(),
            };
        })

        .ArrayResult("INDIRECT", 1, 2, r =>
        {
            var text = r.Text(0);
            var a1Style = r.Boolean(1, true);
            if (r.HasError) return EvalValue.Scalar(r.Error);
            return references.Resolve(text, a1Style, r.Context) is { } reference
                ? reference
                : EvalValue.Error(CellError.Reference);
        })

        .ArrayResult("OFFSET", 3, 5, r =>
        {
            var reference = AsReference(r, 0);
            var rowOffset = r.Integer(1);
            var columnOffset = r.Integer(2);
            var height = r.Has(3) ? r.Integer(3) : reference?.Range.RowCount ?? 1;
            var width = r.Has(4) ? r.Integer(4) : reference?.Range.ColumnCount ?? 1;
            if (r.HasError) return EvalValue.Scalar(r.Error);
            if (reference is null || height == 0 || width == 0) return EvalValue.Error(CellError.Reference);

            var start = reference.Range.Start.Offset(rowOffset, columnOffset);
            var end = start.Offset(height - M.Sign(height), width - M.Sign(width));
            if (!start.IsValid || !end.IsValid) return EvalValue.Error(CellError.Reference);
            return reference with { Range = new CellRange(start, end) };
        })

        .Raw("HYPERLINK", 1, 2, r =>
        {
            var url = r.Value(0);
            var label = r.Has(1) ? r.Value(1) : url;
            return label.IsError ? label : url.IsError ? url : label;
        })

        .Build();

    private static RangeValue? AsReference(ArgumentReader r, int index) =>
    r.Raw(index) as RangeValue;

    private static EvalValue Positions(ArgumentReader r, bool wantRow)
    {
        if (!r.Has(0) && r.Context.ArrayArea is { IsSingleCell: false } area)
            return new ArrayValue([.. Enumerable.Range(area.Start.Row, area.RowCount).Select(row => (IReadOnlyList<CellValue>)
                [.. Enumerable.Range(area.Start.Column, area.ColumnCount).Select(column => CellValue.Number((wantRow ? row : column) + 1))])]);
        if (AsReference(r, 0) is not { Range.IsSingleCell: false } reference) return EvalValue.Scalar(Position(r, wantRow));
        var range = reference.Range;
        return wantRow
            ? new ArrayValue([.. Enumerable.Range(range.Start.Row + 1, range.RowCount).Select(row => (IReadOnlyList<CellValue>)[CellValue.Number(row)])])
            : new ArrayValue([[.. Enumerable.Range(range.Start.Column + 1, range.ColumnCount).Select(column => CellValue.Number(column))]]);
    }

    private static CellValue Position(ArgumentReader r, bool wantRow)
    {
        var address = r.Has(0)
            ? AsReference(r, 0)?.Range.Start
            : r.Context.CurrentCell.Address;

        if (address is null) return CellValue.FromError(CellError.Reference);
        return CellValue.Number(wantRow ? address.Value.Row + 1 : address.Value.Column + 1);
    }

    private string Shown(string formula)
    {
        var language = locale.Culture.TwoLetterISOLanguageName;
        if (formula.StartsWith("{=", StringComparison.Ordinal) && formula.EndsWith('}'))
            return "{" + localization.ToDisplay(formula[1..^1], language) + "}";
        return localization.ToDisplay(formula.StartsWith('=') || formula.StartsWith('{') ? formula : "=" + formula, language);
    }
}
