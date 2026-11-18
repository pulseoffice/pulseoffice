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
    private readonly TextCodecs.IOdfParagraphText _paragraphs;

    public OdfSheetMetadataMapper() : this(new TextCodecs.OdfParagraphText())
    {
    }

    internal OdfSheetMetadataMapper(TextCodecs.IOdfParagraphText paragraphs) => _paragraphs = paragraphs;

    private readonly TableRows _rows = new();
    private readonly TableCells _cells = new();
    private readonly OdfQualifiedRanges _ranges = new();
    private readonly OdfTableFormulas _tableFormulas = new();
    private readonly TokenMap<ValidationAlert> _alerts = new(ValidationAlert.Stop, "stop",
        (ValidationAlert.Warning, "warning"), (ValidationAlert.Information, "information"));

    public XElement? BuildValidations(Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        var rules = Flatten(workbook);
        if (rules.Count == 0) return null;

        return new XElement(Namespaces.Table + "content-validations", rules.Select((rule, index) =>
        {
            var (sheet, structured) = rule;
            var at = new SheetCell(workbook.IndexOf(sheet), structured.Range.Start);
            var validation = structured with
            {
                FirstFormula = _tableFormulas.FlattenText(structured.FirstFormula, workbook, at),
                SecondFormula = _tableFormulas.FlattenText(structured.SecondFormula, workbook, at),
            };
            var element = new XElement(Namespaces.Table + "content-validation",
                new XAttribute(Namespaces.Table + "name", NameOf(sheet, index)),
                _tableFormulas.Kept(structured.FirstFormula, validation.FirstFormula, structured.SecondFormula, validation.SecondFormula));

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
        if (text is { Length: > 0 }) message.Add(_paragraphs.Paragraphs(text));
        return message;
    }

    private string? MessageText(XElement? message)
    {
        var lines = message?.Elements(Namespaces.Text + "p").Select(_paragraphs.Line).ToList();
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

            var areas = new Dictionary<string, ValidationAreas>(StringComparer.Ordinal);
            foreach (var row in _rows.In(table))
            {
                if (row.Index >= CellAddress.MaxRows) break;
                var lastRow = (int)Math.Min((long)row.Index + row.Repeat - 1, CellAddress.MaxRows - 1);
                foreach (var cell in _cells.Of(row))
                {
                    if (cell.Address.Column >= CellAddress.MaxColumns) break;
                    if (cell.Element.Attribute(Namespaces.Table + "content-validation-name")?.Value is not { } name) continue;
                    if (!declared.ContainsKey(name)) continue;

                    var lastColumn = (int)Math.Min((long)cell.Address.Column + cell.Repeat - 1, CellAddress.MaxColumns - 1);
                    if (!areas.TryGetValue(name, out var area)) areas[name] = area = new ValidationAreas();
                    area.Add(row.Index, lastRow, cell.Address.Column, lastColumn);
                }
            }

            foreach (var (name, area) in areas)
            {
                var element = declared[name];
                var rectangles = area.Rectangles();
                var parsed = Parse(element, rectangles[0]);
                parsed = parsed with
                {
                    FirstFormula = _tableFormulas.Read(element) ?? parsed.FirstFormula,
                    SecondFormula = _tableFormulas.ReadSecond(element) ?? parsed.SecondFormula,
                };
                var anchor = _ranges.Read(element.Attribute(Namespaces.Table + "base-cell-address")?.Value)?.Range.Start ?? rectangles[0].Start;
                var anchored = parsed with { Range = new CellRange(anchor, anchor) };
                foreach (var rectangle in rectangles)
                    sheet.Validations.Add(_anchoring.Reanchored(anchored, rectangle.Start, rectangle));
            }
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

    private readonly PulseOffice.Sheets.Engine.Operations.ValidationAnchoring _anchoring = new();
}

internal sealed class ValidationAreas
{
    private readonly List<CellRange> _closed = [];
    private readonly List<(int First, int Last)> _spans = [];
    private Dictionary<(int First, int Last), (int Start, int End)> _open = [];
    private int _row = -1;
    private int _rowLast = -1;

    public void Add(int row, int rowLast, int firstColumn, int lastColumn)
    {
        if (row != _row)
        {
            Settle();
            _row = row;
            _rowLast = rowLast;
        }

        if (_spans.Count > 0 && _spans[^1].Last + 1 == firstColumn) _spans[^1] = (_spans[^1].First, lastColumn);
        else _spans.Add((firstColumn, lastColumn));
    }

    public IReadOnlyList<CellRange> Rectangles()
    {
        Settle();
        Close(_open);
        _open = [];
        return [.. _closed.OrderBy(range => range.Start.Row).ThenBy(range => range.Start.Column)];
    }

    private void Settle()
    {
        if (_spans.Count == 0) return;

        var next = new Dictionary<(int First, int Last), (int Start, int End)>();
        foreach (var span in _spans)
        {
            var continues = _open.TryGetValue(span, out var band) && band.End == _row - 1;
            if (continues) _open.Remove(span);
            next[span] = (continues ? band.Start : _row, _rowLast);
        }
        Close(_open);
        _open = next;
        _spans.Clear();
    }

    private void Close(Dictionary<(int First, int Last), (int Start, int End)> bands)
    {
        foreach (var (span, band) in bands)
            _closed.Add(new CellRange(new CellAddress(band.Start, span.First), new CellAddress(band.End, span.Last)));
    }
}

public sealed class OdfSheetValidationNames
{
    private readonly int[] _bandStarts;
    private readonly List<(CellRange Range, string Name)>[] _bands;

    public OdfSheetValidationNames(IReadOnlyList<(CellRange Range, string Name)> validations)
    {
        ArgumentNullException.ThrowIfNull(validations);

        _bandStarts = [.. validations
            .SelectMany(validation => new[] { validation.Range.Start.Row, validation.Range.End.Row + 1 })
            .Distinct()
            .Order()];
        _bands = [.. _bandStarts.Select(_ => new List<(CellRange, string)>())];
        foreach (var validation in validations)
        {
            var last = Array.BinarySearch(_bandStarts, validation.Range.End.Row + 1);
            for (var band = Array.BinarySearch(_bandStarts, validation.Range.Start.Row); band < last; band++)
                _bands[band].Add(validation);
        }
    }

    public string? For(CellAddress address)
    {
        var band = Array.BinarySearch(_bandStarts, address.Row);
        if (band < 0) band = ~band - 1;
        if (band < 0) return null;

        foreach (var (range, name) in _bands[band])
            if (range.Contains(address)) return name;
        return null;
    }
}
