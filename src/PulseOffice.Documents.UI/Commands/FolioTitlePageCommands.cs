using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioTitlePageCommands(IActiveFolioContext context, ITitlePages titles, IFolioForms forms, IFolioStyleNames names, ITextCatalog texts)
    : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.format.titlePage") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && titles.CanStart(editor.Document),
            Execute = (_, ct) => TitlePageAsync(ct),
        },
    ];

    private async Task TitlePageAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || !titles.CanStart(editor.Document)) return;
        var styles = editor.Document.Styles.Page.All.Select(style => style.Name).ToList();
        var initial = Math.Max(0, styles.IndexOf(DefaultStyleNames.FirstPage));
        FormField[] fields =
        [
            new ChoiceField("mode", texts["folio.titlePage.mode"], [texts["folio.titlePage.insert"], texts["folio.titlePage.convert"]]),
            new NumberField("count", texts["folio.titlePage.count"], 1, 1, 20) { Integer = true },
            new ChoiceField("style", texts["folio.titlePage.style"], [.. styles.Select(names.Title)], initial),
            new CheckField("restart", texts["folio.titlePage.restart"], true),
            new NumberField("start", texts["folio.titlePage.start"], 1, 0, 9999) { Integer = true },
        ];
        if (await forms.AskAsync("command.folio.format.titlePage.title", "folio.titlePage.explanation", fields, ct).ConfigureAwait(true) is not { } values) return;
        var count = (int)Math.Round(values.Number("count", 1));
        var settings = new TitlePageSettings(count, styles[values.Choice("style", styles.Count)],
            values.Flag("restart") ? (int)Math.Round(values.Number("start", 1)) : null);
        if (values.Choice("mode", 2) == 0)
        {
            editor.Change(() => titles.Insert(editor.Document, editor.History, settings));
            return;
        }
        if (ContentStart(editor, count) is { } content)
            editor.Change(() => titles.Convert(editor.Document, editor.History, content, settings));
    }

    private string? ContentStart(PulseOffice.Documents.UI.Editing.FolioEditor editor, int count)
    {
        var pages = editor.Layout.Pages;
        if (count >= pages.Count) return null;
        var before = pages.Take(count).SelectMany(page => page.Lines).Select(line => line.ParagraphId).ToHashSet();
        return pages[count].Lines.Select(line => line.ParagraphId).FirstOrDefault(id => id is not null && !before.Contains(id));
    }
}
