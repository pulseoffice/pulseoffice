using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Formats;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.StylePanels;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioStyleCommands(
    IActiveFolioContext context,
    ISidePanelHost panel,
    IFolioStyleActions actions,
    IFolioStyleDialogs dialogs,
    IStyleEditing styles,
    IFolioForms forms,
    IFileDialogService files,
    ITextDocumentFormatRegistry formats,
    ITextCatalog texts) : FolioCommandSet(context)
{
    public const string PanelKey = "styles";

    private readonly IReadOnlyList<(string Id, string? Style)> _characterStyles =
    [
        ("folio.charStyle.none", null),
        ("folio.charStyle.emphasis", DefaultStyleNames.Emphasis),
        ("folio.charStyle.strong", DefaultStyleNames.StrongEmphasis),
        ("folio.charStyle.quotation", DefaultStyleNames.Quotation),
        ("folio.charStyle.source", DefaultStyleNames.SourceText),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.styles.panel") with
        {
            DefaultGesture = "F11",
            Execute = (_, _) =>
            {
                panel.Show(PanelKey);
                return Task.CompletedTask;
            },
        },
        Editing("folio.style.new") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => Run(editor => actions.NewAsync(editor, StyleFamily.Paragraph, ct)) },
        Editing("folio.style.update") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.Document.Styles.Paragraph.Contains(editor.CurrentParagraph().StyleName ?? DefaultStyleNames.Standard),
            Execute = (_, _) => Run(editor =>
            {
                actions.Update(editor, StyleFamily.Paragraph);
                return Task.CompletedTask;
            }),
        },
        Editing("folio.style.edit") with
        {
            Surface = CommandSurface.Advanced,
            Execute = (_, ct) => Run(editor => dialogs.EditAsync(editor, StyleFamily.Paragraph, editor.CurrentParagraph().StyleName ?? DefaultStyleNames.Standard, ct)),
        },
        Editing("folio.style.load") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => LoadAsync(ct) },
        .. _characterStyles.Select(CharacterStyle),
        Editing("folio.format.paintbrush") with
        {
            CheckedWhen = _ => Editor?.Brush is not null,
            Execute = (_, _) => Run(editor =>
            {
                editor.Arm(editor.Brush is not null ? null : editor.Services.Brush.Capture(editor.Document, editor.HasSelection ? editor.Selection.Start : editor.Caret));
                return Task.CompletedTask;
            }),
        },
    ];

    private CommandDefinition CharacterStyle((string Id, string? Style) entry) =>
        Editing(entry.Id) with
        {
            CheckedWhen = _ => entry.Style is not null && Editor?.EffectiveRun().StyleName == entry.Style,
            Execute = (_, _) => Run(editor =>
            {
                if (entry.Style is null || editor.Document.Styles.Character.Contains(entry.Style))
                    editor.Apply((documents, range) => documents.ApplyCharacterStyle(editor.Document, editor.History, range, entry.Style), editor.CharacterRange());
                return Task.CompletedTask;
            }),
        };

    private Task Run(Func<FolioEditor, Task> action) => Editor is { } editor ? action(editor) : Task.CompletedTask;

    private async Task LoadAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var filters = formats.ReadableFormats.Select(format => new FileFilter(format.DisplayName, format.Extensions)).ToList();
        await using var chosen = await files.OpenFileAsync(texts["command.folio.style.load.title"], filters, ct).ConfigureAwait(true);
        if (chosen is null) return;
        var values = await forms.AskAsync("command.folio.style.load.title", "folio.styles.load.explanation",
            [new CheckField("overwrite", texts["folio.styles.load.overwrite"], false)], ct, 420).ConfigureAwait(true);
        if (values is null) return;
        await using var stream = await chosen.OpenRead(ct).ConfigureAwait(true);
        var copy = new MemoryStream();
        await stream.CopyToAsync(copy, ct).ConfigureAwait(true);
        copy.Position = 0;
        var read = formats.Read(chosen.Path, copy);
        if (read.Value is not { } template) return;
        editor.Change(() => styles.Load(editor.Document, editor.History, template.Styles, values.Flag("overwrite")));
    }
}
