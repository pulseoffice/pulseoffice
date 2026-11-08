using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioPageCommands(IActiveFolioContext context, IFolioPageStyles pages, IFolioPageDialog dialog, PulseOffice.Documents.Editing.IStyleEditing styles, ITextCatalog texts)
    : FolioCommandSet(context)
{
    private readonly IReadOnlyList<(string Key, double Centimetres)> _margins =
    [
        ("narrow", 1.27),
        ("normal", 2.0),
        ("moderate", 2.54),
        ("wide", 3.18),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Orientation("folio.page.portrait", PageOrientation.Portrait),
        Orientation("folio.page.landscape", PageOrientation.Landscape),
        Editing("folio.page.margins") with
        {
            RibbonControl = new ChoiceSpec(MarginChoices, () => Editor is { } editor ? MarginKey(pages.Read(pages.Current(editor)).Margins) : null)
            {
                MoreCommandId = "folio.page.setup",
                Width = 96,
            },
            Execute = (ctx, ct) => ctx.Argument is string key && _margins.FirstOrDefault(entry => entry.Key == key) is { Key: not null } chosen
                ? Change(geometry => geometry with { Margins = Margins.Uniform(Twip.FromCentimeters(chosen.Centimetres)) }, "page-margins")
                : SetupAsync(ct),
        },
        Editing("folio.page.columns") with
        {
            RibbonControl = new ChoiceSpec(ColumnChoices, () => Editor is { } editor ? pages.Read(pages.Current(editor)).Columns.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : null)
            {
                MoreCommandId = "folio.page.setup",
                Width = 72,
            },
            Execute = (ctx, ct) => ctx.Argument is string key && int.TryParse(key, System.Globalization.CultureInfo.InvariantCulture, out var count)
                ? Change(geometry => geometry with { Columns = Columns(count) }, "page-columns")
                : SetupAsync(ct),
        },
        Editing("folio.page.setup") with { Execute = (_, ct) => SetupAsync(ct) },
        Editing("folio.page.borders") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => SetupAsync(ct, FolioPageTab.Borders) },
        Editing("folio.page.columnsDialog") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => SetupAsync(ct, FolioPageTab.Columns) },
        Editing("folio.page.watermark") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => SetupAsync(ct, FolioPageTab.Watermark) },
        Region("folio.page.header", style => style.Header, PulseOffice.Documents.Styles.DefaultStyleNames.Header),
        Region("folio.page.footer", style => style.Footer, PulseOffice.Documents.Styles.DefaultStyleNames.Footer),
    ];

    private CommandDefinition Region(string id, Func<PulseOffice.Documents.Styles.PageStyle, PulseOffice.Documents.Styles.HeaderFooterContent> region, string paragraphStyle) =>
        Editing(id) with
        {
            CheckedWhen = _ => Editor is { } editor && region(pages.Current(editor)).Enabled,
            Execute = (_, _) =>
            {
                if (Editor is not { } editor) return Task.CompletedTask;
                var name = pages.Current(editor).Name;
                editor.Change(() => styles.Change(editor.Document, editor.History, editor.Document.Styles.Page, name, style => Toggle(editor, region(style), paragraphStyle), id));
                if (region(pages.Current(editor)) is { Enabled: true } shown && shown.Default.Blocks.OfType<ParagraphBlock>().LastOrDefault() is { } last)
                    editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(last.Id, last.Length), new PulseOffice.Documents.Editing.DocumentPosition(last.Id, last.Length));
                return Task.CompletedTask;
            },
        };

    private void Toggle(FolioEditor editor, PulseOffice.Documents.Styles.HeaderFooterContent region, string paragraphStyle)
    {
        region.Enabled = !region.Enabled;
        if (region.Enabled && !region.Default.Blocks.OfType<ParagraphBlock>().Any())
            region.Default.Add(new ParagraphBlock(editor.Document.NextId("p"), string.Empty, paragraphStyle));
    }

    private CommandDefinition Orientation(string id, PageOrientation orientation) =>
        Editing(id) with
        {
            CheckedWhen = _ => Editor is { } editor && pages.Current(editor).Orientation == orientation,
            Execute = (_, _) => Change(geometry => geometry with { Orientation = orientation }, "page-orientation"),
        };

    private async Task SetupAsync(CancellationToken ct, FolioPageTab tab = FolioPageTab.Page)
    {
        if (Editor is not { } editor) return;
        await dialog.EditAsync(editor, pages.Current(editor).Name, ct, tab).ConfigureAwait(true);
    }

    private Task Change(Func<PageGeometry, PageGeometry> change, string description)
    {
        if (Editor is { } editor) pages.Change(editor, change, description);
        return Task.CompletedTask;
    }

    private SectionColumns Columns(int count) =>
        count <= 1 ? SectionColumns.Single : new SectionColumns(Math.Clamp(count, 1, 10), Twip.FromCentimeters(1.25));

    private IReadOnlyList<RibbonChoice> MarginChoices() =>
        [.. _margins.Select(entry => new RibbonChoice(entry.Key, texts[$"folio.page.margins.{entry.Key}"]))];

    private IReadOnlyList<RibbonChoice> ColumnChoices() =>
        [.. Enumerable.Range(1, 3).Select(count => new RibbonChoice(count.ToString(System.Globalization.CultureInfo.InvariantCulture), texts[$"folio.page.columns.{count}"]))];

    private string? MarginKey(Margins margins) =>
        _margins.FirstOrDefault(entry => Near(entry.Centimetres, margins.Left) && Near(entry.Centimetres, margins.Right) && Near(entry.Centimetres, margins.Top) && Near(entry.Centimetres, margins.Bottom)).Key;

    private bool Near(double centimetres, Twip value) => Math.Abs(centimetres - value.ToCentimeters()) < 0.02;
}
