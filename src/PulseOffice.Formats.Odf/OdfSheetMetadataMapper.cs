using PulseOffice.Formats;
using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed class OdfSheetMetadataMapper
{
    private readonly OdfValidationSyntax _syntax = new();
    private readonly TableRows _rows = new();
    private readonly TableCells _cells = new();
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly TokenMap<ValidationAlert> _alerts = new(ValidationAlert.Stop, "stop",
        (ValidationAlert.Warning, "warning"), (ValidationAlert.Information, "information"));

    public XElement? BuildValidations(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var rules = Flatten(workbook);
        if (rules.Count == 0) return null;

        return new XElement(Namespaces.Table + "content-validations", rules.Select((rule, index) =>
        {
            var (sheet, validation) = rule;
            var element = new XElement(Namespaces.Table + "content-validation",
                new XAttribute(Namespaces.Table + "name", NameOf(sheet, index)));

            if (_syntax.Write(validation) is { } condition)
            {
                element.Add(new XAttribute(Namespaces.Table + "condition", condition),
                    new XAttribute(Namespaces.Table + "allow-empty-cell", validation.AllowBlank ? "true" : "false"));
                if (validation.Kind == ValidationKind.List)
                    element.Add(new XAttribute(Namespaces.Table + "display-list", validation.ShowDropdown ? "unsorted" : "no"));
            }

            element.Add(new XAttribute(Namespaces.Table + "base-cell-address", _ranges.WriteAddress(sheet.Name, validation.Range.Start)));

            if (validation.InputTitle is { Length: > 0 } || validation.InputMessage is { Length: > 0 })
                element.Add(Message("help-message", validation.InputTitle, validation.InputMessage, validation.ShowInputMessage));

            var error = Message("error-message", validation.ErrorTitle, validation.ErrorMessage, validation.ShowErrorAlert);
            error.Add(new XAttribute(Namespaces.Table + "message-type", _alerts.Token(validation.Alert)));
            element.Add(error);
            return element;
        }));
    }

    private XElement Message(string name, string? title, string? text, bool display)
    {
        var message = new XElement(Namespaces.Table + name, new XAttribute(Namespaces.Table + "display", display ? "true" : "false"));
        if (title is { Length: > 0 }) message.Add(new XAttribute(Namespaces.Table + "title", title));
        if (text is { Length: > 0 })
            foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                message.Add(new XElement(Namespaces.Text + "p", line));
        return message;
    }

    private string? MessageText(XElement? message)
    {
        var lines = message?.Elements(Namespaces.Text + "p").Select(paragraph => paragraph.Value).ToList();
        return lines is null || lines.Count == 0 ? null : string.Join('\n', lines);
    }

    public OdfSheetValidationNames ValidationNames(Workbook workbook, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var flat = Flatten(workbook);
        return new OdfSheetValidationNames([.. Enumerable.Range(0, flat.Count)
            .Where(index => ReferenceEquals(flat[index].Sheet, sheet))
            .Select(index => (flat[index].Validation.Range, NameOf(sheet, index)))]);
    }

    public void ReadValidations(XElement content, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(workbook);

        var declared = content.Descendants(Namespaces.Table + "content-validation")
            .GroupBy(element => element.Attribute(Namespaces.Table + "name")?.Value ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (declared.Count == 0) return;

        foreach (var table in content.Descendants(Namespaces.Table + "table"))
        {
            if (workbook.FindSheet(table.Attribute(Namespaces.Table + "name")?.Value ?? string.Empty) is not { } sheet) continue;

            var ranges = new Dictionary<string, (CellAddress First, CellAddress Last)>(StringComparer.Ordinal);
            foreach (var row in _rows.In(table))
            {
                if (row.Index >= CellAddress.MaxRows) break;
                foreach (var cell in _cells.Of(row))
                {
                    if (cell.Address.Column >= CellAddress.MaxColumns) break;
                    if (cell.Element.Attribute(Namespaces.Table + "content-validation-name")?.Value is not { } name) continue;

                    var last = new CellAddress(
                        (int)Math.Min((long)row.Index + row.Repeat - 1, CellAddress.MaxRows - 1),
                        (int)Math.Min((long)cell.Address.Column + cell.Repeat - 1, CellAddress.MaxColumns - 1));
                    ranges[name] = ranges.TryGetValue(name, out var existing)
                        ? (Corner(existing.First, cell.Address, Math.Min), Corner(existing.Last, last, Math.Max))
                        : (cell.Address, last);
                }
            }

            foreach (var (name, range) in ranges)
                if (declared.TryGetValue(name, out var element))
                    sheet.Validations.Add(Parse(element, new CellRange(range.First, range.Last)));
        }
    }

    private DataValidation Parse(XElement element, CellRange range)
    {
        var error = element.Element(Namespaces.Table + "error-message");
        var help = element.Element(Namespaces.Table + "help-message");
        var validation = new DataValidation
        {
            Range = range,
            AllowBlank = element.Attribute(Namespaces.Table + "allow-empty-cell")?.Value != "false",
            ShowDropdown = element.Attribute(Namespaces.Table + "display-list")?.Value != "no",
            ErrorTitle = error?.Attribute(Namespaces.Table + "title")?.Value,
            ErrorMessage = MessageText(error),
            InputTitle = help?.Attribute(Namespaces.Table + "title")?.Value,
            InputMessage = MessageText(help),
            ShowInputMessage = help?.Attribute(Namespaces.Table + "display")?.Value != "false",
            ShowErrorAlert = error?.Attribute(Namespaces.Table + "display")?.Value != "false",
            Alert = _alerts.Value(error?.Attribute(Namespaces.Table + "message-type")?.Value),
        };
        return _syntax.Read(element.Attribute(Namespaces.Table + "condition")?.Value, validation);
    }

    private List<(Worksheet Sheet, DataValidation Validation)> Flatten(Workbook workbook) =>
        [.. workbook.Sheets.SelectMany(sheet => sheet.Validations.Select(validation => (sheet, validation)))];

    private string NameOf(Worksheet sheet, int index) => string.Create(CultureInfo.InvariantCulture, $"val{index}_{sheet.Name}");

    private CellAddress Corner(CellAddress first, CellAddress second, Func<int, int, int> pick) =>
        new(pick(first.Row, second.Row), pick(first.Column, second.Column));
}

public sealed class OdfSheetValidationNames(IReadOnlyList<(CellRange Range, string Name)> validations)
{
    public string? For(CellAddress address)
    {
        foreach (var (range, name) in validations)
            if (range.Contains(address)) return name;
        return null;
    }
}
