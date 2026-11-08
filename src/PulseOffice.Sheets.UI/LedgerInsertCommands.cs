using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Links;

namespace PulseOffice.Sheets.UI;

public sealed class LedgerInsertCommands(
    IActiveSheetContext context,
    IHyperlinkPrompt hyperlinks,
    ITextCatalog texts) : LedgerCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Insert("ledger.insert.currentDate", "Ctrl+;", (workbook, sheet, address) =>
        {
            var serial = (DateTime.Today - new DateTime(1899, 12, 30)).TotalDays;

            sheet.SetValue(address, CellValue.Number(serial));
            sheet.SetStyle(address, workbook.Styles.Derive(
                sheet[address].StyleId, style => style with { NumberFormatId = 14 }));
        }),

        Insert("ledger.insert.currentTime", "Ctrl+Shift+;", (workbook, sheet, address) =>
        {
            var now = DateTime.Now;
            var serial = now.TimeOfDay.TotalDays;

            sheet.SetValue(address, CellValue.Number(serial));
            sheet.SetStyle(address, workbook.Styles.Derive(
                sheet[address].StyleId, style => style with { NumberFormatId = 20 }));
        }),

        Insert("ledger.insert.sheetName", null, (_, sheet, address) =>
            sheet.SetValue(address, CellValue.Text(sheet.Name))),
    ];

    public IEnumerable<CommandDefinition> GetDialogCommands() =>
    [
        Prototype("ledger.insert.hyperlink") with
        {
            MenuPath = "insert/link",
            DefaultGesture = "Ctrl+K",
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, ct) => HyperlinkAsync(ct),
        },
    ];

    private CommandDefinition Insert(
        string id, string? gesture, Action<Workbook, Worksheet, Addressing.CellAddress> write) =>
        Prototype(id) with
        {
            MenuPath = "insert/fields",
            DefaultGesture = gesture,
            Surface = CommandSurface.Advanced,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.HasTarget,
            Execute = (_, _) =>
            {
                Apply(id, write);
                return Task.CompletedTask;
            },
        };

    private void Apply(string id, Action<Workbook, Worksheet, Addressing.CellAddress> write)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is null) return;

        var address = Context.Selection.Start;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var previous = sheet[address];

        write(workbook, sheet, address);

        Record(texts[$"command.{id}.title"], sheet, address, previous);
    }

    private void Record(string title, Worksheet sheet, Addressing.CellAddress address, Cell previous)
    {
        if (Context.Engine is not { } engine) return;

        var target = new SheetCell(Context.SheetIndex, address);
        var updated = sheet[address];

        void Set(Cell cell)
        {
            sheet[address] = cell;
            engine.RecalculateFrom([target]);
            Context.RequestRefresh();
        }

        Set(updated);
        Context.History?.Push(new DelegateAction(title, redo: () => Set(updated), undo: () => Set(previous)));
    }

    private async Task HyperlinkAsync(CancellationToken ct)
    {
        if (Context.Workbook is not { } workbook) return;
        if (Context.Engine is null) return;

        var address = Context.Selection.Start;
        var sheet = workbook.Sheets[Context.SheetIndex];
        var cell = sheet[address];

        var request = new LinkRequest("command.ledger.insert.hyperlink.title")
        {
            Address = cell.Hyperlink ?? string.Empty,
            Text = cell.Value.ToString(),
            Places = Places(workbook),
            CanRemove = cell.Hyperlink is not null,
        };

        if (await hyperlinks.AskAsync(request, ct).ConfigureAwait(true) is not { } choice) return;

        var previous = cell;
        var textChanged = !string.Equals(choice.Text, request.Text, StringComparison.Ordinal);

        sheet[address] = choice.Remove
            ? cell with { Hyperlink = null }
            : textChanged
                ? cell with { Value = CellValue.Text(choice.Text), Formula = null, Hyperlink = choice.Address }
                : cell with { Hyperlink = choice.Address };

        Record(texts["command.ledger.insert.hyperlink.title"], sheet, address, previous);
    }

    private IReadOnlyList<LinkPlace> Places(Workbook workbook)
    {
        var sheets = workbook.Sheets.Select(sheet => new LinkPlace(sheet.Name, "#" + SheetReference(sheet.Name) + ".A1", "link.place.sheet")).ToList();
        var names = workbook.DefinedNames.Where(name => name.Scope is null).OrderBy(name => name.Name, StringComparer.CurrentCulture)
            .Select(name => new LinkPlace(name.Name, "#" + name.Name, "link.place.range")).ToList();
        var places = new List<LinkPlace> { new(texts["link.place.sheets"], string.Empty, "link.place.sheet") { Selectable = false, Children = sheets } };
        if (names.Count > 0) places.Add(new LinkPlace(texts["link.place.names"], string.Empty, "link.place.range") { Selectable = false, Children = names });
        return places;
    }

    private string SheetReference(string name) =>
        name.Contains('\'') || name.Contains('.') || name.Contains(' ') ? "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'" : name;
}
