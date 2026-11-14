using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioToolServices(
    IDocumentAutoFormat AutoFormat,
    IFolioFieldRefresh Fields,
    ITableFormulaParser Formulas,
    IDocumentOutline Outline,
    IStyleEditing Styles,
    ISystemClipboardText Clipboard,
    IFolioForms Forms,
    IParagraphSorting Sorting,
    PulseOffice.Document.Themes.IThemePalettes Themes,
    PulseOffice.Documents.Blocks.IBlockTree Tree,
    ITextThemeApplier ThemeApplier,
    PulseOffice.UI.Controls.Themes.IThemePaletteGallery ThemeGallery,
    IStatusBarService Status,
    PulseOffice.UI.Controls.Pages.IPageNumberPositionGalleries PageNumbers,
    FolioToolViews Views,
    ITextCatalog Texts);

public sealed record FolioToolViews(
    PulseOffice.UI.Controls.TextFormat.ITextFormatPreviews Previews,
    PulseOffice.UI.Controls.TextFormat.ICharacterSampleLayout Characters,
    PulseOffice.UI.Controls.Pages.ILineNumberSampleLayout LineNumbers,
    PulseOffice.UI.Controls.Galleries.IGalleryNavigator Navigator,
    PulseOffice.UI.Controls.Fonts.IFontPickerFactory Fonts);

public sealed class SelectionFormulaContext : ITableFormulaContext
{
    public int Row => 0;

    public int Column => 0;

    public int RowCount => 0;

    public int ColumnCount => 0;

    public double? Value(int row, int column) => null;
}

public sealed class FolioToolCommands(IActiveFolioContext context, FolioToolServices services) : FolioCommandSet(context)
{
    private const string HorizontalLine = "Horizontal Line";

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Editing("folio.tools.autoFormatApply") with { Surface = CommandSurface.Advanced }, editor => Format(editor, tracked: false)),
        Action(Editing("folio.tools.autoFormatApplyTracked") with { Surface = CommandSurface.Advanced }, editor => Format(editor, tracked: true)),
        Action(Viewing("folio.tools.repaginate") with { Surface = CommandSurface.Advanced }, Repaginate),
        Action(Viewing("folio.tools.updateCharts") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && editor.Document.Objects.All.Any(item => item.Content is PulseOffice.Documents.Objects.EmbeddedContent { Value: PulseOffice.Charts.ChartDefinition }),
        }, editor => editor.Reflow()),
        Viewing("folio.tools.calculate") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { HasSelection: true },
            Execute = (_, _) => CalculateAsync(),
        },
        Viewing("folio.file.outlineToClipboard") with { Surface = CommandSurface.Advanced, Execute = (_, _) => OutlineAsync() },
        Action(Editing("folio.style.horizontalLine") with { Surface = CommandSurface.Advanced }, ApplyHorizontalLine),
        Editing("folio.tools.sort") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { HasSelection: true } editor && !editor.ReadOnly() && services.Sorting.CanSort(editor.Document, editor.Selection),
            Execute = (_, ct) => SortAsync(ct),
        },
        Editing("folio.format.comments") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.Document.Comments.All.Count > 0,
            Execute = (_, ct) => CommentsAsync(ct),
        },
        Editing("folio.insert.pageNumberWizard") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => PageNumberAsync(ct) },
        Editing("folio.format.ruby") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { HasSelection: true } editor && !editor.ReadOnly(),
            Execute = (_, ct) => RubyAsync(ct),
        },
        Editing("folio.format.theme") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => ThemeAsync(ct) },
        Editing("folio.tools.lineNumbering") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => LineNumberingAsync(ct) },
        Mark("folio.tools.protectFields", marks => marks.Fields, marks => marks with { Fields = !marks.Fields }),
        Mark("folio.tools.protectBookmarks", marks => marks.Bookmarks, marks => marks with { Bookmarks = !marks.Bookmarks }),
    ];

    private CommandDefinition Mark(string id, Func<PulseOffice.Documents.Protection.MarkProtection, bool> on,
        Func<PulseOffice.Documents.Protection.MarkProtection, PulseOffice.Documents.Protection.MarkProtection> flip) =>
        Action(Editing(id) with { Surface = CommandSurface.Advanced, OutsideHistory = true, CheckedWhen = _ => Editor is { } editor && on(editor.Document.Marks) },
            editor =>
            {
                var document = editor.Document;
                var marks = flip(document.Marks);
                editor.History.ApplyOutsideHistory(() => document.Marks = marks);
                document.Raise();
            });

    private void Set<T>(FolioEditor editor, Func<TextDocument, T> read, Action<TextDocument, T> write, T value, string id)
    {
        var document = editor.Document;
        var before = read(document);
        if (EqualityComparer<T>.Default.Equals(before, value)) return;
        write(document, value);
        editor.History.Push(new PulseOffice.Document.Commands.DelegateAction(services.Texts[$"command.{id}.title"],
            () => { write(document, value); document.Raise(); }, () => { write(document, before); document.Raise(); }));
        document.Raise();
    }

    private async Task CommentsAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        PulseOffice.UI.Controls.FormField[] fields =
        [
            new PulseOffice.UI.Controls.FontField("font", services.Texts["folio.comments.font"], "Liberation Sans", services.Views.Fonts),
            new PulseOffice.UI.Controls.NumberField("size", services.Texts["folio.comments.size"], 9, 5, 72) { Unit = "pt", Presets = [8, 9, 10, 11, 12] },
        ];
        if (await services.Forms.AskAsync("command.folio.format.comments.title", "folio.comments.explanation", fields, CommentPreview, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } editor) return;
        var properties = new CharacterProperties
        {
            Font = values.Text("font").Trim() is { Length: > 0 } font ? font : null,
            FontSize = values.Number("size", 9),
        };
        editor.Change(() =>
        {
            using (editor.History.BeginTransaction("format-comments"))
                foreach (var comment in editor.Document.Comments.All)
                {
                    var paragraphs = services.Tree.Paragraphs(comment.Content).ToList();
                    if (paragraphs.Count == 0) continue;
                    editor.Services.Editor.SetCharacter(editor.Document, editor.History,
                        new DocumentRange(new DocumentPosition(paragraphs[0].Id, 0), new DocumentPosition(paragraphs[^1].Id, paragraphs[^1].Length)), properties);
                }
        });
    }

    private async Task PageNumberAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        var initial = new PulseOffice.UI.Controls.Pages.PageNumberPosition(false, PulseOffice.UI.Controls.Pages.PageNumberAlignment.Center);
        PulseOffice.UI.Controls.FormField[] fields =
        [
            new PulseOffice.UI.Controls.Pages.PageNumberPositionField("position", services.Texts["folio.pageNumber.place"], initial, services.PageNumbers),
            new PulseOffice.UI.Controls.CheckField("total", services.Texts["folio.pageNumber.total"]),
        ];
        if (await services.Forms.AskAsync("command.folio.insert.pageNumberWizard.title", "folio.pageNumber.explanation", fields, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } editor) return;
        var position = values.TryGetValue("position", out var chosen) && chosen is PulseOffice.UI.Controls.Pages.PageNumberPosition picked ? picked : initial;
        var header = position.Top;
        var align = position.Alignment switch
        {
            PulseOffice.UI.Controls.Pages.PageNumberAlignment.Left => TextAlign.Left,
            PulseOffice.UI.Controls.Pages.PageNumberAlignment.Right => TextAlign.Right,
            _ => TextAlign.Center,
        };
        var total = values.Flag("total");
        var style = editor.Document.Styles.Page.Find(editor.Layout.Pages.FirstOrDefault()?.StyleName ?? editor.Document.InitialPageStyle) ?? editor.Document.Styles.Page.All[0];
        var inserted = string.Empty;
        using var transaction = editor.History.BeginTransaction("page-number");
        editor.Change(() =>
        {
            var region = header ? style.Header : style.Footer;
            if (!region.Enabled)
                services.Styles.Change(editor.Document, editor.History, editor.Document.Styles.Page, style.Name, target => (header ? target.Header : target.Footer).Enabled = true, "page-number");
            var current = editor.Document.Styles.Page.Find(style.Name) ?? style;
            var container = (header ? current.Header : current.Footer).Default;
            TextRun[] runs = total
                ? [new TextRun("1") { Field = TextFieldKind.PageNumber }, new TextRun(" / "), new TextRun("1") { Field = TextFieldKind.PageCount }]
                : [new TextRun("1") { Field = TextFieldKind.PageNumber }];
            var paragraph = new PulseOffice.Documents.Blocks.ParagraphBlock(editor.Document.NextId("p"),
                new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs, Align = align, StyleName = header ? DefaultStyleNames.Header : DefaultStyleNames.Footer });
            var recorder = new EditRecorder(editor.Document, "page-number").Touch(container);
            container.Add(paragraph);
            recorder.Commit(editor.History);
            inserted = paragraph.Id;
        });
        if (inserted.Length > 0) services.Fields.UpdateFields(editor, new HashSet<string>(StringComparer.Ordinal) { inserted });
    }

    private Avalonia.Controls.Control CommentPreview(PulseOffice.UI.Controls.IFormValuesFeed feed) =>
        services.Views.Previews.Create(feed, new PulseOffice.UI.Controls.TextFormat.FeedPreview<PulseOffice.UI.Controls.TextFormat.CharacterFormatState>(
            values => new PulseOffice.UI.Controls.TextFormat.CharacterFormatState { Font = values.Text("font"), Size = values.Number("size", 9) },
            state =>
            {
                var view = new PulseOffice.UI.Controls.TextFormat.CharacterSampleView(services.Views.Characters);
                view.Show(state, services.Texts["folio.comments.sample"]);
                return view;
            },
            PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
            110));

    private Avalonia.Controls.Control RubyPreview(PulseOffice.UI.Controls.IFormValuesFeed feed) =>
        services.Views.Previews.Create(feed, new PulseOffice.UI.Controls.TextFormat.FeedPreview<(string Base, string Ruby)>(
            values => (values.Text("base"), values.Text("ruby")),
            pair =>
            {
                var panel = new Avalonia.Controls.StackPanel { Background = Avalonia.Media.Brushes.White, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                panel.Children.Add(new Avalonia.Controls.TextBlock { Text = pair.Ruby, FontSize = 11, Foreground = Avalonia.Media.Brushes.DimGray, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                panel.Children.Add(new Avalonia.Controls.TextBlock { Text = pair.Base, FontSize = 24, Foreground = Avalonia.Media.Brushes.Black, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center });
                return new Avalonia.Controls.Border { Background = Avalonia.Media.Brushes.White, Padding = new Avalonia.Thickness(16, 8), Child = panel };
            },
            PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
            110));

    private Avalonia.Controls.Control LineNumberPreview(PulseOffice.UI.Controls.IFormValuesFeed feed) =>
        services.Views.Previews.Create(feed, new PulseOffice.UI.Controls.TextFormat.FeedPreview<PulseOffice.UI.Controls.Pages.LineNumberSample>(
            values => new PulseOffice.UI.Controls.Pages.LineNumberSample(values.Flag("on"), (int)values.Number("every", 5), values.Choice("side", 2) == 1,
                values.Number("distance", 0.5) * 72 / 2.54, values.Flag("blank")),
            sample =>
            {
                var view = new PulseOffice.UI.Controls.Pages.LineNumberSampleView(services.Views.LineNumbers);
                view.Show(sample);
                return view;
            },
            PulseOffice.UI.Controls.Preview.PreviewBackdrop.Page,
            190));

    private async Task RubyAsync(CancellationToken ct)
    {
        if (Editor is not { HasSelection: true } editor) return;
        var current = editor.EffectiveRun().Ruby ?? string.Empty;
        if (await services.Forms.AskAsync("command.folio.format.ruby.title", "folio.ruby.explanation",
                [
                    new PulseOffice.UI.Controls.TextField("base", services.Texts["folio.ruby.base"], editor.SelectedText()),
                    new PulseOffice.UI.Controls.TextField("ruby", services.Texts["folio.ruby.text"], current),
                ], RubyPreview, ct).ConfigureAwait(true) is not { } values
            || Editor is not { HasSelection: true } target) return;
        var ruby = values.Text("ruby").Trim() is { Length: > 0 } text ? text : null;
        target.Apply((writer, range) => writer.MapRuns(target.Document, target.History, range, "ruby", run => run with { Ruby = ruby }), target.Selection);
    }

    private async Task ThemeAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        if (await services.ThemeGallery.AskAsync("command.folio.format.theme.title", "folio.theme.explanation", null, ct).ConfigureAwait(true) is not { } chosen
            || Editor is not { } editor) return;
        var count = 0;
        editor.Change(() => count = services.ThemeApplier.Apply(editor.Document, editor.History, chosen));
        services.Status.Report(services.Texts.Format("folio.theme.applied", services.Texts["theme." + chosen.Key], count), NotificationSeverity.Success);
    }

    private async Task SortAsync(CancellationToken ct)
    {
        if (Editor is null) return;
        List<string> separators = [services.Texts["folio.sort.whole"], services.Texts["folio.sort.tab"], ";", ","];
        char?[] marks = [null, '\t', ';', ','];
        var key = services.Texts["folio.sort.group.key"];
        PulseOffice.UI.Controls.FormField[] fields =
        [
            new PulseOffice.UI.Controls.ChoiceField("separator", services.Texts["folio.sort.separator"], separators) { Section = key },
            new PulseOffice.UI.Controls.NumberField("column", services.Texts["folio.sort.column"], 1, 1, 64) { Integer = true, Section = key, ShownWhen = values => values.Choice("separator", marks.Length) > 0 },
            new PulseOffice.UI.Controls.Choices.IconChoiceField("type", services.Texts["folio.sort.type"],
            [
                new("text", string.Empty, services.Texts["folio.sort.text"]),
                new("number", string.Empty, services.Texts["folio.sort.number"]),
            ], null) { ShowLabels = true, Section = key },
            new PulseOffice.UI.Controls.Choices.IconChoiceField("order", services.Texts["folio.sort.order"],
            [
                new("ascending", string.Empty, services.Texts["folio.sort.ascending"]),
                new("descending", string.Empty, services.Texts["folio.sort.descending"]),
            ], null) { ShowLabels = true, Section = key },
            new PulseOffice.UI.Controls.CheckField("case", services.Texts["folio.sort.matchCase"]) { Section = key },
        ];
        if (await services.Forms.AskAsync("command.folio.tools.sort.title", "folio.sort.explanation", fields, ct).ConfigureAwait(true) is not { } values
            || Editor is not { HasSelection: true } editor) return;
        var sortKey = new ParagraphSortKey((int)values.Number("column", 1), marks[values.Choice("separator", marks.Length)],
            values.Choice("type", 2) == 1, values.Choice("order", 2) == 1, values.Flag("case"));
        var range = editor.Selection;
        editor.Change(() => services.Sorting.Sort(editor.Document, editor.History, range, sortKey, editor.Culture()));
    }

    private async Task LineNumberingAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var current = editor.Document.LineNumbering;
        List<string> sides = [services.Texts["folio.lineNumbers.left"], services.Texts["folio.lineNumbers.right"]];
        PulseOffice.UI.Controls.FormField[] fields =
        [
            new PulseOffice.UI.Controls.CheckField("on", services.Texts["folio.lineNumbers.on"], current is not null),
            new PulseOffice.UI.Controls.NumberField("every", services.Texts["folio.lineNumbers.every"], current?.Increment ?? 5, 1, 100) { Integer = true, Presets = [1, 5, 10], ShownWhen = On },
            new PulseOffice.UI.Controls.Choices.IconChoiceField("side", services.Texts["folio.lineNumbers.side"],
            [
                new("left", "text.align.left", sides[0]),
                new("right", "text.align.right", sides[1]),
            ], null, current?.Side == LineNumberSide.Right ? 1 : 0) { ShowLabels = true, ShownWhen = On },
            new PulseOffice.UI.Controls.NumberField("distance", services.Texts["folio.lineNumbers.distance"], Math.Round((current?.Distance ?? 0.5 * 72 / 2.54) * 2.54 / 72, 2), 0, 5, 0.1) { Unit = "cm", ShownWhen = On },
            new PulseOffice.UI.Controls.CheckField("restart", services.Texts["folio.lineNumbers.restart"], current?.RestartEachPage ?? false) { ShownWhen = On },
            new PulseOffice.UI.Controls.CheckField("blank", services.Texts["folio.lineNumbers.blank"], current?.CountBlankLines ?? true) { ShownWhen = On },
        ];
        if (await services.Forms.AskAsync("command.folio.tools.lineNumbering.title", "folio.lineNumbers.explanation", fields, LineNumberPreview, ct).ConfigureAwait(true) is not { } values
            || Editor is not { } target) return;
        LineNumbering? next = values.Flag("on")
            ? new LineNumbering((int)values.Number("every", 5))
            {
                Side = values.Choice("side", 2) == 1 ? LineNumberSide.Right : LineNumberSide.Left,
                Distance = values.Number("distance", 0.5) * 72 / 2.54,
                RestartEachPage = values.Flag("restart"),
                CountBlankLines = values.Flag("blank"),
            }
            : null;
        Set(target, document => document.LineNumbering, (document, value) => document.LineNumbering = value, next, "folio.tools.lineNumbering");
    }

    private bool On(IReadOnlyDictionary<string, object> values) => values.Flag("on");

    private void Format(FolioEditor editor, bool tracked)
    {
        var count = 0;
        var log = editor.Document.Changes;
        var recording = log.IsRecording;
        editor.Change(() =>
        {
            log.IsRecording = recording || tracked;
            try
            {
                count = services.AutoFormat.FormatAll(editor.Document, editor.History, lists: true, borders: true);
            }
            finally
            {
                log.IsRecording = recording;
            }
            if (tracked) log.ShowChanges = true;
        });
        services.Status.Report(services.Texts.Format("folio.autoformat.done", count), count > 0 ? NotificationSeverity.Success : NotificationSeverity.Info);
    }

    private void Repaginate(FolioEditor editor)
    {
        editor.Reflow();
        if (!editor.ReadOnly()) services.Fields.UpdateFields(editor);
        services.Status.Report(services.Texts.Format("folio.repaginate.done", editor.Layout.Pages.Count));
    }

    private async Task CalculateAsync()
    {
        if (Editor is not { } editor) return;
        var text = editor.SelectedText().Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        if (!services.Formulas.TryParse(text, out var node) || node is null)
        {
            services.Status.Report(services.Texts["folio.calculate.invalid"], NotificationSeverity.Warning);
            return;
        }
        var result = node.Evaluate(new SelectionFormulaContext()).ToString("0.##########", CultureInfo.CurrentCulture);
        await services.Clipboard.SetTextAsync(result).ConfigureAwait(true);
        services.Status.Report(services.Texts.Format("folio.calculate.done", result), NotificationSeverity.Success);
    }

    private async Task OutlineAsync()
    {
        if (Editor is not { } editor) return;
        var lines = services.Outline.Headings(editor.Document).Select(heading => new string('\t', Math.Max(0, heading.Level - 1)) + heading.Text);
        await services.Clipboard.SetTextAsync(string.Join(Environment.NewLine, lines)).ConfigureAwait(true);
        services.Status.Report(services.Texts["folio.outline.copied"], NotificationSeverity.Success);
    }

    private void ApplyHorizontalLine(FolioEditor editor)
    {
        var styles = editor.Document.Styles.Paragraph;
        using (editor.History.BeginTransaction("horizontal-line"))
        {
            if (!styles.Contains(HorizontalLine))
                services.Styles.Add(editor.Document, editor.History, styles, new ParagraphStyle(HorizontalLine)
                {
                    ParentName = DefaultStyleNames.Standard,
                    NextStyleName = DefaultStyleNames.TextBody,
                    Paragraph = new ParagraphProperties
                    {
                        SpaceAfter = 0.25 * 72 / 2.54,
                        Borders = new BoxBorders { Bottom = new BorderLine(0.75, 0xFF808080) { Style = BorderLineStyle.Double }, PaddingBottom = 0 },
                    },
                    Character = new CharacterProperties { FontSize = 6 },
                }, "horizontal-line-style");
            editor.Apply((writer, range) => writer.ApplyParagraphStyle(editor.Document, editor.History, range, HorizontalLine), editor.ParagraphRange());
        }
    }
}
