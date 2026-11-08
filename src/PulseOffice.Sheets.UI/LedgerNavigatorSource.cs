using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Navigation;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerNavigatorSource : INavigatorSource
{
    private const string Sheets = "sheets";
    private const string Names = "names";
    private const string Objects = "objects";
    private const string Comments = "comments";

    private readonly IActiveSheetContext _context;
    private readonly Dialogs.ISheetReferenceReader _reader;
    private readonly ITextCatalog _texts;

    public LedgerNavigatorSource(IActiveSheetContext context, Dialogs.ISheetReferenceReader reader, ITextCatalog texts)
    {
        _context = context;
        _reader = reader;
        _texts = texts;
        context.TargetChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<NavigatorCategory> Categories =>
        [.. new[] { Sheets, Names, Objects, Comments }.Select(key => new NavigatorCategory(key, _texts["ledger.navigator." + key]))];

    public IReadOnlyList<NavigatorItem> Items(string category)
    {
        if (_context.Workbook is not { } workbook) return [];
        return category switch
        {
            Sheets => [.. workbook.Sheets.Select((sheet, index) => new NavigatorItem(index.ToString(System.Globalization.CultureInfo.InvariantCulture), sheet.Name, 0) { Movable = true })],
            Names => [.. workbook.DefinedNames.OrderBy(name => name.Name, StringComparer.CurrentCultureIgnoreCase).Select(name => new NavigatorItem(name.Name, name.Name + "  " + name.Expression, 0))],
            Objects => [.. workbook.Sheets.SelectMany((sheet, index) => sheet.Objects.Select(item => new NavigatorItem(index + ":" + item.Id, sheet.Name + " · " + (item.Content.Name is { Length: > 0 } name ? name : item.Id), 0)))],
            _ => [.. workbook.Sheets.SelectMany((sheet, index) => sheet.Comments.OrderBy(pair => pair.Key).Select(pair =>
                new NavigatorItem(index + ":" + pair.Key.ToA1(), sheet.Name + "!" + pair.Key.ToA1() + "  " + pair.Value.Text, 0)))],
        };
    }

    public string? Current(string category) =>
        category == Sheets ? _context.SheetIndex.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

    public void Go(string category, string id)
    {
        if (_context.Workbook is not { } workbook) return;
        switch (category)
        {
            case Sheets when int.TryParse(id, out var sheet):
                _context.SelectSheet(sheet);
                break;
            case Names when workbook.DefinedNames.FirstOrDefault(name => name.Name == id) is { } defined
                            && _reader.Read(workbook, defined.Expression.TrimStart('='), _context.SheetIndex, Dialogs.ReferenceShape.Range) is [var reference, ..]:
                _context.SelectSheet(reference.SheetIndex);
                _context.SelectRange(reference.Range);
                break;
            case Objects when Split(id) is ({ } index, var objectId) && workbook.Sheets[index].Objects.FirstOrDefault(item => item.Id == objectId) is { } placed:
                _context.SelectSheet(index);
                _context.SelectObject(placed);
                break;
            case Comments when Split(id) is ({ } index, var cell) && _reader.ReadLocal(workbook, cell, index, Dialogs.ReferenceShape.Cell) is { } range:
                _context.SelectSheet(index);
                _context.SelectRange(range);
                break;
        }
        _context.RequestRefresh();
    }

    private (int? Sheet, string Remainder) Split(string id)
    {
        var colon = id.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && int.TryParse(id[..colon], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var sheet) && _context.Workbook is { } workbook && sheet < workbook.Sheets.Count
            ? (sheet, id[(colon + 1)..])
            : (null, id);
    }

    public bool CanMove(string category, string id, NavigatorMove move) =>
        category == Sheets && _context.Workbook is { } workbook && !workbook.Protection.Structure && int.TryParse(id, out var index)
        && move switch
        {
            NavigatorMove.Up => index > 0,
            NavigatorMove.Down => index < workbook.Sheets.Count - 1,
            _ => false,
        };

    public void Move(string category, string id, NavigatorMove move)
    {
        if (!CanMove(category, id, move)) return;
        var from = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture);
        Reorder(from, move == NavigatorMove.Up ? from - 1 : from + 1);
    }

    public bool CanDrop(string category, string id, string? beforeId) =>
        category == Sheets && _context.Workbook is { Protection.Structure: false } && int.TryParse(id, out _) && (beforeId is null || int.TryParse(beforeId, out _));

    public void Drop(string category, string id, string? beforeId)
    {
        if (!CanDrop(category, id, beforeId) || _context.Workbook is not { } workbook) return;
        var from = int.Parse(id, System.Globalization.CultureInfo.InvariantCulture);
        var before = beforeId is null ? workbook.Sheets.Count : int.Parse(beforeId, System.Globalization.CultureInfo.InvariantCulture);
        Reorder(from, before > from ? before - 1 : before);
    }

    private void Reorder(int from, int to)
    {
        if (_context.Workbook is not { } workbook || workbook.Protection.Structure || from == to || to < 0 || to >= workbook.Sheets.Count) return;
        if (!workbook.MoveSheet(from, to)) return;
        _context.History?.Push(new DelegateAction(_texts["history.sheet.move"], redo: () => workbook.MoveSheet(from, to), undo: () => workbook.MoveSheet(to, from)));
        _context.SelectSheet(to);
        _context.RequestRefresh();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
