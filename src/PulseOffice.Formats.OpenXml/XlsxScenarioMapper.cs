using System.Globalization;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Worksheet = PulseOffice.Sheets.Worksheet;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using SheetScenario = PulseOffice.Sheets.Scenario;
using XmlScenario = DocumentFormat.OpenXml.Spreadsheet.Scenario;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxScenarioMapper
{
    public Scenarios? Build(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.Scenarios.Count == 0) return null;

        var container = new Scenarios();

        foreach (var scenario in sheet.Scenarios)
        {
            var element = new XmlScenario
            {
                Name = scenario.Name,
                Comment = scenario.Comment,
                Count = (uint)scenario.Values.Count,
                Locked = scenario.PreventChanges ? true : null,
            };

            foreach (var (address, value) in scenario.Values)
                element.AppendChild(new InputCells
                {
                    CellReference = address.ToA1(),
                    Val = Text(value),
                });

            container.AppendChild(element);
        }

        return container;
    }

    public void Read(Scenarios? container, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        if (container is null) return;

        foreach (var element in container.Elements<XmlScenario>())
        {
            var values = new Dictionary<CellAddress, CellValue>();

            foreach (var input in element.Elements<InputCells>())
            {
                if (input.CellReference?.Value is not { } reference) continue;
                if (!CellAddress.TryParseA1(reference, out var address)) continue;

                values[address] = Value(input.Val?.Value);
            }

            if (values.Count == 0) continue;

            sheet.Scenarios.Add(new SheetScenario
            {
                Name = element.Name?.Value ?? "Scenario",
                Comment = element.Comment?.Value,
                PreventChanges = element.Locked?.Value == true,
                Range = new CellRange(
                    new CellAddress(values.Keys.Min(cell => cell.Row), values.Keys.Min(cell => cell.Column)),
                    new CellAddress(values.Keys.Max(cell => cell.Row), values.Keys.Max(cell => cell.Column))),
                Values = values,
            });
        }
    }

    private string Text(CellValue value) => value.Kind switch
    {
        ValueKind.Number => value.AsNumber().ToString(CultureInfo.InvariantCulture),
        ValueKind.Boolean => value.AsBoolean() ? "TRUE" : "FALSE",
        ValueKind.Empty => string.Empty,

        _ => value.AsText(),
    };

    private CellValue Value(string? text)
    {
        if (string.IsNullOrEmpty(text)) return CellValue.Empty;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return CellValue.Number(number);

        if (string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase)) return CellValue.True;
        if (string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase)) return CellValue.False;

        return CellValue.Text(text);
    }
}
