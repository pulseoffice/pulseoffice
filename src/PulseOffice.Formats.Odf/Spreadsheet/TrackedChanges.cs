using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfTrackedChangesMapper
{
    private readonly OdfCellValueCodec _values = new();
    private readonly OdfProtectionMapper _protection = new();
    private readonly OdfFormulaSyntax _formulas = new();
    private readonly TokenMap<ChangeState> _states = new(ChangeState.Pending, "pending",
        (ChangeState.Pending, "pending"), (ChangeState.Accepted, "accepted"), (ChangeState.Rejected, "rejected"));

    public XElement? Build(Workbook workbook)
    {
        var log = workbook.Changes;
        if (log.Changes.Count == 0 && !log.IsRecording && !log.IsProtected && log.ShowChanges) return null;

        var element = new XElement(Namespaces.Table + "tracked-changes",
            new XAttribute(Namespaces.Table + "track-changes", log.IsRecording ? "true" : "false"));
        if (log.IsProtected) _protection.Key(element, log.ProtectionHash ?? string.Empty);
        if (log.IsProtected && log.ProtectionHash is null) element.Add(new XAttribute(Namespaces.Loext + "protected", "true"));
        if (!log.ShowChanges) element.Add(new XAttribute(Odt.OdtNamespaces.Pulse + "show-changes", "false"));

        foreach (var change in log.Changes)
            element.Add(Change(change));
        return element;
    }

    private XElement Change(TrackedChange change)
    {
        var previous = new XElement(Namespaces.Table + "change-track-table-cell");
        if (change.FormulaBefore is { Length: > 0 } formula)
            previous.Add(new XAttribute(Namespaces.Table + "formula", _formulas.ToOdf(formula)));
        if (change.Before.Kind != ValueKind.Empty) _values.Write(previous, change.Before);

        var info = new XElement(Namespaces.Office + "change-info",
            new XElement(Namespaces.Dc + "creator", change.Author ?? string.Empty),
            new XElement(Namespaces.Dc + "date", change.At.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
        if (change.Comment is { Length: > 0 } comment) info.Add(new XElement(Namespaces.Text + "p", comment));

        return new XElement(Namespaces.Table + "cell-content-change",
            new XAttribute(Namespaces.Table + "id", change.Id),
            new XAttribute(Namespaces.Table + "acceptance-state", _states.Token(change.State)),
            new XElement(Namespaces.Table + "cell-address",
                new XAttribute(Namespaces.Table + "column", Integer(change.Address.Column)),
                new XAttribute(Namespaces.Table + "row", Integer(change.Address.Row)),
                new XAttribute(Namespaces.Table + "table", Integer(change.SheetIndex))),
            info,
            new XElement(Namespaces.Table + "previous", previous));
    }

    private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public void Read(XElement spreadsheet, Workbook workbook, XDocument? settings)
    {
        Read(spreadsheet, workbook);
        var key = settings?.Descendants(Config + "config-item")
            .FirstOrDefault(item => (string?)item.Attribute(Config + "name") == "TrackedChangesProtectionKey")?.Value;
        if (key is not { Length: > 0 } || workbook.Changes.ProtectionHash is not null) return;
        workbook.Changes.IsProtected = true;
        workbook.Changes.ProtectionHash = key;
    }

    public void Read(XElement spreadsheet, Workbook workbook)
    {
        if (spreadsheet.Element(Namespaces.Table + "tracked-changes") is not { } element) return;

        var log = workbook.Changes;
        log.IsRecording = (string?)element.Attribute(Namespaces.Table + "track-changes") != "false";
        var hash = _protection.HashOf(element);
        log.IsProtected = hash is not null || (string?)element.Attribute(Namespaces.Loext + "protected") == "true";
        log.ProtectionHash = hash;
        log.ShowChanges = (string?)element.Attribute(Odt.OdtNamespaces.Pulse + "show-changes") != "false";

        var changes = element.Elements(Namespaces.Table + "cell-content-change")
            .Select(change => Change(change, workbook))
            .OfType<TrackedChange>()
            .ToList();
        log.Import(changes);
    }

    private TrackedChange? Change(XElement element, Workbook workbook)
    {
        if (element.Element(Namespaces.Table + "cell-address") is not { } address) return null;
        var sheet = Integer((string?)address.Attribute(Namespaces.Table + "table"));
        var row = Integer((string?)address.Attribute(Namespaces.Table + "row"));
        var column = Integer((string?)address.Attribute(Namespaces.Table + "column"));
        if (sheet is not { } sheetIndex || row is not { } r || column is not { } c || sheetIndex >= workbook.Sheets.Count) return null;

        var cell = new CellAddress(r, c);
        if (!cell.IsValid) return null;
        var current = workbook.Sheets[sheetIndex][cell];
        var previous = element.Element(Namespaces.Table + "previous")?.Element(Namespaces.Table + "change-track-table-cell");
        var info = element.Element(Namespaces.Office + "change-info");
        var formula = (string?)previous?.Attribute(Namespaces.Table + "formula");

        return new TrackedChange
        {
            Id = (string?)element.Attribute(Namespaces.Table + "id") ?? Guid.NewGuid().ToString("N"),
            SheetIndex = sheetIndex,
            Address = cell,
            Before = previous is null ? CellValue.Empty : _values.Read(previous),
            After = current.Value,
            FormulaBefore = formula is null ? null : _formulas.ToInternal(formula),
            FormulaAfter = current.Formula,
            Author = info?.Element(Namespaces.Dc + "creator")?.Value is { Length: > 0 } author ? author : null,
            At = DateTimeOffset.TryParse(info?.Element(Namespaces.Dc + "date")?.Value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var moment) ? moment : DateTimeOffset.Now,
            State = _states.Value((string?)element.Attribute(Namespaces.Table + "acceptance-state")),
            Comment = info?.Elements(Namespaces.Text + "p").Select(paragraph => paragraph.Value).FirstOrDefault(),
        };
    }

    private string Integer(int value) => value.ToString(CultureInfo.InvariantCulture);

    private int? Integer(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : null;
}
