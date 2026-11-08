using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioBreakCommands(IActiveFolioContext context, IFolioForms forms, IEnvelopes envelopes, ITextCatalog texts, PulseOffice.UI.Controls.Services.IDialogService dialogs, PulseOffice.UI.Icons.IIconCatalog? icons = null) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Editing("folio.insert.lineBreak"), editor => editor.LineBreak()),
        Action(Editing("folio.insert.landscapeBreak") with
            {
                Surface = CommandSurface.Advanced,
                EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && StartsPages(editor, editor.Caret.ParagraphId),
            },
            editor => PageBreak(editor, DefaultStyleNames.Landscape, null)),
        Editing("folio.insert.breakDialog") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => DialogAsync(ct) },
        Editing("folio.insert.envelope") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => EnvelopeAsync(ct) },
    ];

    private async Task EnvelopeAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        var formats = envelopes.Formats;
        FormField[] fields =
        [
            new TextField("addressee", texts["folio.envelope.addressee"]) { Multiline = true },
            new TextField("sender", texts["folio.envelope.sender"]) { Multiline = true },
            new ChoiceField("format", texts["folio.envelope.format"], [.. formats.Select(format => $"{format.Name} ({format.WidthMillimetres:0.#} × {format.HeightMillimetres:0.#} mm)")]),
        ];
        var view = new FormPromptView(texts["folio.envelope.explanation"], fields, texts);
        var preview = new EnvelopePreview();
        EnvelopeRequest Read(IReadOnlyDictionary<string, object> current) =>
            new(current.Text("addressee"), current.Text("sender"), formats[current.Choice("format", formats.Count)]);
        preview.Request = Read(view.Values);
        view.ValuesChanged += (_, _) => preview.Request = Read(view.Values);
        view.Preview = preview;
        if (await dialogs.ShowAsync<Dictionary<string, object>>(view, new DialogOptions { TitleKey = "command.folio.insert.envelope.title" }, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } editor || values.Text("addressee").Trim().Length == 0) return;
        var request = Read(values);
        editor.Change(() => envelopes.Insert(editor.Document, editor.History, request));
    }

    private async Task DialogAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var styles = editor.Document.Styles.Page.All.Select(style => style.Name).ToList();
        List<PulseOffice.UI.Controls.Choices.IconChoice> kinds =
        [
            new("line", "folio.insert.lineBreak", texts["folio.break.line"]),
            new("column", "folio.insert.columnBreak", texts["folio.break.column"]),
            new("page", "folio.insert.pageBreak", texts["folio.break.page"]),
        ];
        List<string> pageStyles = [texts["folio.break.sameStyle"], .. styles];
        FormField[] fields =
        [
            new PulseOffice.UI.Controls.Choices.IconChoiceField("kind", texts["folio.break.kind"], kinds, icons, 2) { ShowLabels = true },
            new ChoiceField("style", texts["folio.break.style"], pageStyles) { ShownWhen = values => values.Choice("kind", 3) == 2 },
            new CheckField("restart", texts["folio.break.restart"]) { ShownWhen = values => values.Choice("kind", 3) == 2 },
            new NumberField("number", texts["folio.break.number"], 1, 0, 9999) { Integer = true, ShownWhen = values => values.Choice("kind", 3) == 2 && values.Flag("restart") },
        ];
        if (await forms.AskAsync("command.folio.insert.breakDialog.title", "folio.break.explanation", fields, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } current) return;
        switch (values.Choice("kind", kinds.Count))
        {
            case 0:
                current.LineBreak();
                break;
            case 1:
                Split(current, new ParagraphProperties { ColumnBreakBefore = true }, "column-break");
                break;
            default:
                var style = values.Choice("style", pageStyles.Count);
                PageBreak(current, style == 0 ? null : styles[style - 1], values.Flag("restart") ? (int)values.Number("number", 1) : null);
                break;
        }
    }

    private void PageBreak(FolioEditor editor, string? style, int? restart) =>
        editor.Insert((documents, at) =>
        {
            var created = Created(documents.SplitParagraph(editor.Document, editor.History, at));
            if (style is null && restart is null || !StartsPages(editor, at.ParagraphId))
                documents.SetParagraph(editor.Document, editor.History, DocumentRange.In(created, 0, 0), new ParagraphProperties { PageBreakBefore = true });
            else
                documents.SetPageStyle(editor.Document, editor.History, created, style ?? editor.Services.Stories.Find(editor.Document, at.ParagraphId).Paragraph.Content.PageStyleName ?? editor.Document.InitialPageStyle, restart);
            editor.PlaceCaret(new DocumentPosition(created, 0), false);
        }, "page-break");

    private bool StartsPages(FolioEditor editor, string paragraphId) =>
        ReferenceEquals(editor.Services.Stories.Find(editor.Document, paragraphId).Story, editor.Document.Body)
        && editor.Services.Tables.Editing.Locate(editor.Document, paragraphId) is null;

    private void Split(FolioEditor editor, ParagraphProperties change, string label) =>
        editor.Insert((documents, at) =>
        {
            var created = Created(documents.SplitParagraph(editor.Document, editor.History, at));
            documents.SetParagraph(editor.Document, editor.History, DocumentRange.In(created, 0, 0), change);
            editor.PlaceCaret(new DocumentPosition(created, 0), false);
        }, label);

    private string Created(DocumentEdit split) => ((ParagraphSplit)split.Changes[0]).NewParagraphId;
}
