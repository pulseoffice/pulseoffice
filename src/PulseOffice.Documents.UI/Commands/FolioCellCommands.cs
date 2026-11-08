using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioCellCommands(
    IActiveFolioContext context,
    ICellNumberFormatting formatting,
    IFolioNumberRecognition recognition,
    IFolioForms forms,
    ITextCatalog texts) : FolioCommandSet(context)
{
    private readonly CellValueKind?[] _kinds = [null, CellValueKind.Number, CellValueKind.Percent, CellValueKind.Currency, CellValueKind.Date];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        InTable("folio.table.protect", editor => Protect(editor, true)),
        InTable("folio.table.unprotect", editor => Protect(editor, false)),
        InTable("folio.table.numberFormat", _ => { }) with { Execute = (_, ct) => FormatAsync(ct) },
        Viewing("folio.table.numberRecognition") with
        {
            Surface = CommandSurface.Advanced,
            CheckedWhen = _ => recognition.Enabled,
            Execute = (_, _) =>
            {
                recognition.Enabled = !recognition.Enabled;
                return Task.CompletedTask;
            },
        },
    ];

    private CommandDefinition InTable(string id, Action<FolioEditor> run) =>
        Action(Prototype(id) with
        {
            Surface = CommandSurface.Advanced,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Editor is { InTable: true } editor && !editor.ReadOnly(),
        }, run);

    private void Protect(FolioEditor editor, bool on)
    {
        if (editor.TableAt() is not { } at) return;
        editor.Change(() => formatting.Protect(editor.Document, editor.History, at.At.Table, at.Range, on));
    }

    private async Task FormatAsync(CancellationToken ct)
    {
        if (Editor?.TableAt() is not { } at) return;
        var current = at.At.Table.Rows[at.Range.Top].Cells[at.Range.Left].NumberFormat;
        List<string> kinds = [.. _kinds.Select(kind => texts["folio.cellFormat." + (kind?.ToString().ToLowerInvariant() ?? "none")])];
        FormField[] fields =
        [
            new ChoiceField("kind", texts["folio.cellFormat.kind"], kinds, Array.IndexOf(_kinds, current?.Kind)),
            new NumberField("decimals", texts["folio.cellFormat.decimals"], current?.Decimals ?? 2, 0, 10) { Integer = true },
            new CheckField("grouping", texts["folio.cellFormat.grouping"], current?.Grouping ?? true),
            new TextField("symbol", texts["folio.cellFormat.symbol"], current?.Symbol ?? string.Empty),
            new CheckField("long", texts["folio.cellFormat.longDate"], current?.LongDate ?? false),
        ];
        if (await forms.AskAsync("command.folio.table.numberFormat.title", "folio.cellFormat.explanation", fields, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } editor || editor.TableAt() is not { } target) return;
        var kind = _kinds[values.Choice("kind", _kinds.Length)];
        var format = kind is { } chosen
            ? new CellNumberFormat(chosen, (int)values.Number("decimals", 2), values.Flag("grouping"))
            {
                Symbol = values.Text("symbol").Trim() is { Length: > 0 } symbol ? symbol : null,
                LongDate = values.Flag("long"),
            }
            : null;
        editor.Change(() => formatting.Apply(editor.Document, editor.History, target.At.Table, target.Range, format, editor.Culture()));
    }
}

public interface IFolioNumberRecognition
{
    bool Enabled { get; set; }

    void Leave(FolioEditor editor, PulseOffice.Documents.Editing.TableCellLocation at);
}

public sealed class FolioNumberRecognition(ICellNumberFormatting formatting) : IFolioNumberRecognition
{
    public bool Enabled { get; set; }

    public void Leave(FolioEditor editor, PulseOffice.Documents.Editing.TableCellLocation at)
    {
        if (!Enabled || editor.ReadOnly() || editor.Verdict() != PulseOffice.Documents.Protection.EditVerdict.Allowed) return;
        var cell = at.Table.Rows[at.Row].Cells[at.Column];
        if (cell.NumberFormat is null) return;
        formatting.Recognise(editor.Document, editor.History, cell, editor.Culture());
    }
}
