using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfScenarioMapper
{
    private const int MaxCells = 1 << 20;
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly OdfCellValueCodec _values = new();
    private readonly TableRows _rows = new();
    private readonly TableCells _cells = new();
    private readonly OdfFormulaSyntax _formulas = new();

    public IEnumerable<XElement> Build(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        return workbook.Sheets.SelectMany(Build);
    }

    public IEnumerable<XElement> Build(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.Scenarios.Select(scenario => BuildTable(sheet, scenario));
    }

    public bool IsScenario(XElement table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return table.Element(Namespaces.Table + "scenario") is not null;
    }

    public void Read(XElement table, Workbook workbook, Worksheet? fallbackOwner = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(workbook);

        if (table.Element(Namespaces.Table + "scenario") is not { } element) return;
        if (_ranges.Read(element.Attribute(Namespaces.Table + "scenario-ranges")?.Value) is not { } scenarioRange) return;

        var (sheetName, range) = scenarioRange;
        if ((workbook.FindSheet(sheetName) ?? fallbackOwner) is not { } owner) return;

        var values = new Dictionary<CellAddress, CellValue>();
        var formulas = new Dictionary<CellAddress, string>();
        foreach (var row in _rows.In(table))
        {
            if (row.Index >= range.RowCount) break;
            var rowCount = Math.Min(row.Repeat, range.RowCount - row.Index);
            foreach (var cell in _cells.Of(row))
            {
                if (cell.Address.Column >= range.ColumnCount) break;
                var value = _values.Read(cell.Element);
                var formula = _formulas.ToInternal(cell.Element.Attribute(Namespaces.Table + "formula")?.Value);
                if (value.IsEmpty && formula is null) continue;

                var columnCount = Math.Min(cell.Repeat, range.ColumnCount - cell.Address.Column);
                for (var rowOffset = 0; rowOffset < rowCount; rowOffset++)
                    for (var columnOffset = 0; columnOffset < columnCount && values.Count < MaxCells; columnOffset++)
                    {
                        var address = new CellAddress(range.Start.Row + row.Index + rowOffset, range.Start.Column + cell.Address.Column + columnOffset);
                        values[address] = value;
                        if (formula is not null) formulas[address] = formula;
                    }
            }
        }

        owner.Scenarios.Add(new Scenario
        {
            Name = table.Attribute(Namespaces.Table + "name")?.Value ?? "Scenario",
            Comment = element.Attribute(Namespaces.Table + "comment")?.Value,
            Range = range,
            Values = values,
            Formulas = formulas,
            PreventChanges = element.Attribute(Namespaces.Table + "protected")?.Value == "true",
        });
    }

    private XElement BuildTable(Worksheet sheet, Scenario scenario)
    {
        var table = new XElement(Namespaces.Table + "table",
            new XAttribute(Namespaces.Table + "name", scenario.Name),
            new XElement(Namespaces.Table + "scenario",
                new XAttribute(Namespaces.Table + "display-border", "true"),
                new XAttribute(Namespaces.Table + "border-color", scenario.BorderColorHex ?? "#000080"),
                new XAttribute(Namespaces.Table + "is-active", "false"),
                new XAttribute(Namespaces.Table + "protected", scenario.PreventChanges ? "true" : "false"),
                new XAttribute(Namespaces.Table + "scenario-ranges", _ranges.Write(sheet.Name, scenario.Range)),
                new XAttribute(Namespaces.Table + "comment", scenario.Comment ?? string.Empty)),
            new XElement(Namespaces.Table + "table-column",
                new XAttribute(Namespaces.Table + "number-columns-repeated", scenario.Range.ColumnCount.ToString(CultureInfo.InvariantCulture))));

        for (var row = scenario.Range.Start.Row; row <= scenario.Range.End.Row; row++)
        {
            var rowElement = new XElement(Namespaces.Table + "table-row");
            for (var column = scenario.Range.Start.Column; column <= scenario.Range.End.Column; column++)
            {
                var cell = new XElement(Namespaces.Table + "table-cell");
                var stored = scenario.CellAt(new CellAddress(row, column));
                if (stored.HasFormula) cell.Add(new XAttribute(Namespaces.Table + "formula", _formulas.ToOdf(stored.Formula!)));
                if (!stored.Value.IsEmpty) _values.Write(cell, stored.Value);
                rowElement.Add(cell);
            }
            table.Add(rowElement);
        }
        return table;
    }
}
