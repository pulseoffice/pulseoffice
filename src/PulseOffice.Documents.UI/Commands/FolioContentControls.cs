using Microsoft.Extensions.Logging;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Documents.UI.Commands;

public interface IFolioContentControls
{
    bool Activate(FolioEditor editor, DocumentPosition at);

    Task<bool> ChooseAsync(FolioEditor editor, CancellationToken ct);

    Task<bool> InsertAsync(FolioEditor editor, ContentControlKind kind, CancellationToken ct);

    Task<bool> PropertiesAsync(FolioEditor editor, CancellationToken ct);

    bool Remove(FolioEditor editor);

    bool CanChoose(FolioEditor editor);
}

public sealed class FolioContentControls(
    IFolioForms forms,
    ITextCatalog texts,
    IRasterImageFiles images,
    IFolioImages pictures,
    FolioControlServices controls,
    ILogger<FolioContentControls> logger) : IFolioContentControls
{
    private const string DefaultDateFormat = "dd/MM/yyyy";

    public bool Activate(FolioEditor editor, DocumentPosition at)
    {
        if (editor.ReadOnly() || editor.ControlAt(at) is not { } span || Refuses(editor, span)) return false;
        switch (span.Control.Kind)
        {
            case ContentControlKind.RichText or ContentControlKind.PlainText when span.Control.ShowingPlaceholder:
                editor.Select(span.Start, span.End);
                return true;
            case ContentControlKind.CheckBox when !span.Control.ContentLocked && (Fillable(editor, span) || !editor.Locked()):
                editor.PlaceCaret(span.End, false);
                return editor.Change(() => controls.Editing.Toggle(editor.Document, editor.History, span.Id));
            case ContentControlKind.DropDownList or ContentControlKind.ComboBox or ContentControlKind.Date or ContentControlKind.Picture when !span.Control.ContentLocked:
                editor.PlaceCaret(at, false);
                _ = ChooseObservedAsync(editor);
                return true;
            default:
                return false;
        }
    }

    public bool CanChoose(FolioEditor editor) =>
        !editor.ReadOnly() && editor.ControlAt(editor.Caret) is { Control: { ContentLocked: false, Kind: not (ContentControlKind.RichText or ContentControlKind.PlainText) } };

    public async Task<bool> ChooseAsync(FolioEditor editor, CancellationToken ct)
    {
        if (!CanChoose(editor) || editor.ControlAt(editor.Caret) is not { } span || !Fillable(editor, span) && editor.Locked() || Refuses(editor, span)) return false;
        var document = editor.Document;
        switch (span.Control.Kind)
        {
            case ContentControlKind.CheckBox:
                return editor.Change(() => controls.Editing.Toggle(document, editor.History, span.Id));
            case ContentControlKind.DropDownList or ContentControlKind.ComboBox:
                var shown = span.Control.ShowingPlaceholder || !span.InOneParagraph ? string.Empty : Shown(editor, span);
                var options = span.Control.Items.Select(item => item.DisplayText).ToList();
                List<FormField> fields = [];
                if (options.Count > 0) fields.Add(new ChoiceField("value", texts["contentControl.field.value"], options, Math.Max(0, options.IndexOf(shown))));
                if (span.Control.Kind == ContentControlKind.ComboBox || options.Count == 0)
                    fields.Add(new TextField("typed", texts["contentControl.field.typed"], options.Contains(shown) ? string.Empty : shown));
                var chosen = await forms.AskAsync("command.folio.contentControl.choose.title", "contentControl.explanation.choose", fields, ct).ConfigureAwait(true);
                if (chosen is null) return false;
                var typed = chosen.Text("typed");
                var value = typed.Length > 0 ? typed : options.Count > 0 ? options[chosen.Choice("value", options.Count)] : string.Empty;
                if (value.Length == 0 || controls.Catalog.Find(document, span.Id) is null) return false;
                return editor.Change(() => controls.Editing.Choose(document, editor.History, span.Id, value));
            case ContentControlKind.Date:
                var picked = await forms.AskAsync("command.folio.contentControl.choose.title", "contentControl.explanation.date",
                    [new CalendarField("date", texts["contentControl.field.date"], span.Control.Date)], ct).ConfigureAwait(true);
                if (picked is null || !picked.TryGetValue("date", out var raw) || raw is not DateTime date || controls.Catalog.Find(document, span.Id) is null) return false;
                return editor.Change(() => controls.Editing.SetDate(document, editor.History, span.Id, date.Date));
            case ContentControlKind.Picture:
                if (await images.PickAsync(ct).ConfigureAwait(true) is not { } source || controls.Catalog.Find(document, span.Id) is null) return false;
                if (pictures.Build(document, span.Start.ParagraphId, source.Data, source.MediaType, source.FileName) is not { } image) return false;
                return editor.Change(() => controls.Editing.SetPicture(document, editor.History, span.Id, image));
            default:
                return false;
        }
    }

    public async Task<bool> InsertAsync(FolioEditor editor, ContentControlKind kind, CancellationToken ct)
    {
        if (editor.RefusesEdit()) return false;
        var initial = new ContentControl
        {
            Kind = kind,
            PlaceholderText = kind is ContentControlKind.CheckBox ? null : controls.Placeholders.For(kind),
            DateFormat = kind == ContentControlKind.Date ? DefaultDateFormat : null,
        };
        if (await AskAsync(initial, "command.folio.contentControl.insert." + Slug(kind) + ".title", ct).ConfigureAwait(true) is not { } control || editor.Locked()) return false;
        var range = editor.HasSelection ? editor.Selection : DocumentRange.At(editor.Caret);
        if (editor.Shielded(range) || !range.IsCollapsed && range.Start.ParagraphId != range.End.ParagraphId && (range.Start.Offset != 0 || control.Kind != ContentControlKind.RichText)) range = DocumentRange.At(range.Start);
        return editor.Change(() => controls.Editing.Insert(editor.Document, editor.History, range, control));
    }

    public async Task<bool> PropertiesAsync(FolioEditor editor, CancellationToken ct)
    {
        if (editor.ReadOnly() || editor.ControlAt(editor.Caret) is not { } span || ProtectsControl(editor, span)) return false;
        if (await AskAsync(span.Control, "command.folio.contentControl.properties.title", ct).ConfigureAwait(true) is not { } control) return false;
        if (controls.Catalog.Find(editor.Document, span.Id) is null) return false;
        return editor.Change(() => controls.Editing.Change(editor.Document, editor.History, span.Id, control));
    }

    public bool Remove(FolioEditor editor)
    {
        if (editor.ReadOnly() || editor.ControlAt(editor.Caret) is not { } span || span.Control.ControlLocked || ProtectsControl(editor, span)) return false;
        return editor.Change(() => controls.Editing.Remove(editor.Document, editor.History, span.Id));
    }

    private bool Fillable(FolioEditor editor, ContentControlSpan span) => controls.Forms.Chooses(editor.Document, span);

    private bool Refuses(FolioEditor editor, ContentControlSpan span) => !Fillable(editor, span) && ProtectsControl(editor, span);

    private bool ProtectsControl(FolioEditor editor, ContentControlSpan span) =>
        editor.RefusesEdit(span.Start.ParagraphId) || !span.InOneParagraph && editor.RefusesEdit(span.End.ParagraphId);

    private async Task ChooseObservedAsync(FolioEditor editor)
    {
        try
        {
            await ChooseAsync(editor, CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception error)
        {
            logger.LogError(error, "Choosing the value of a content control failed");
        }
    }

    private string Slug(ContentControlKind kind) => char.ToLowerInvariant(kind.ToString()[0]) + kind.ToString()[1..];

    private string Shown(FolioEditor editor, ContentControlSpan span) =>
        editor.TextOf(span.Start.ParagraphId) is { } text && span.End.Offset <= text.Length ? text[span.Start.Offset..span.End.Offset] : string.Empty;

    private async Task<ContentControl?> AskAsync(ContentControl control, string titleKey, CancellationToken ct)
    {
        var values = await forms.AskAsync(titleKey, "contentControl.explanation.properties", Fields(control), ct, 520).ConfigureAwait(true);
        return values is null ? null : Read(control, values);
    }

    private List<FormField> Fields(ContentControl control)
    {
        List<FormField> fields =
        [
            new TextField("title", texts["contentControl.field.title"], control.Title ?? string.Empty),
            new TextField("tag", texts["contentControl.field.tag"], control.Tag ?? string.Empty),
        ];
        if (control.Kind is not (ContentControlKind.CheckBox or ContentControlKind.Picture))
            fields.Add(new TextField("placeholder", texts["contentControl.field.placeholder"], control.PlaceholderText ?? string.Empty));
        switch (control.Kind)
        {
            case ContentControlKind.PlainText:
                fields.Add(new CheckField("multiLine", texts["contentControl.field.multiLine"], control.MultiLine));
                break;
            case ContentControlKind.DropDownList or ContentControlKind.ComboBox:
                fields.Add(new ChoiceItemsField("items", texts["contentControl.field.items"], [.. control.Items.Select(item => new ChoiceItem(item.DisplayText, item.Value))]));
                break;
            case ContentControlKind.Date:
                fields.Add(new TextField("dateFormat", texts["contentControl.field.dateFormat"], control.DateFormat ?? DefaultDateFormat));
                fields.Add(new TextField("dateLanguage", texts["contentControl.field.dateLanguage"], control.DateLanguage ?? string.Empty));
                break;
            case ContentControlKind.CheckBox:
                fields.Add(new CheckField("checked", texts["contentControl.field.checked"], control.Checked));
                fields.Add(new TextField("checkedSymbol", texts["contentControl.field.checkedSymbol"], control.CheckedSymbol.Character));
                fields.Add(new TextField("uncheckedSymbol", texts["contentControl.field.uncheckedSymbol"], control.UncheckedSymbol.Character));
                break;
        }
        fields.Add(new CheckField("contentLocked", texts["contentControl.field.contentLocked"], control.ContentLocked));
        fields.Add(new CheckField("controlLocked", texts["contentControl.field.controlLocked"], control.ControlLocked));
        return fields;
    }

    private ContentControl Read(ContentControl control, IReadOnlyDictionary<string, object> values)
    {
        string? Optional(string key) => values.Text(key) is { Length: > 0 } text ? text : null;
        var read = control with
        {
            Title = Optional("title"),
            Tag = Optional("tag"),
            ContentLocked = values.Flag("contentLocked"),
            ControlLocked = values.Flag("controlLocked"),
        };
        if (values.ContainsKey("placeholder")) read = read with { PlaceholderText = Optional("placeholder") };
        return control.Kind switch
        {
            ContentControlKind.PlainText => read with { MultiLine = values.Flag("multiLine") },
            ContentControlKind.DropDownList or ContentControlKind.ComboBox => read with
            {
                Items = values.TryGetValue("items", out var items) && items is IEnumerable<ChoiceItem> list
                    ? [.. list.Select(item => new ContentControlItem(item.DisplayText, item.Value))]
                    : control.Items,
            },
            ContentControlKind.Date => read with { DateFormat = Optional("dateFormat") ?? DefaultDateFormat, DateLanguage = Optional("dateLanguage") },
            ContentControlKind.CheckBox => read with
            {
                Checked = values.Flag("checked"),
                CheckedSymbol = Optional("checkedSymbol") is { } on ? control.CheckedSymbol with { Character = on } : control.CheckedSymbol,
                UncheckedSymbol = Optional("uncheckedSymbol") is { } off ? control.UncheckedSymbol with { Character = off } : control.UncheckedSymbol,
            },
            _ => read,
        };
    }
}
