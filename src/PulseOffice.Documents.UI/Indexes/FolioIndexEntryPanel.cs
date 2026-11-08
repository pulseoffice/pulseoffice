using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Indexes;

public sealed record FolioIndexTarget(string Index, string Title);

public interface IFolioIndexEntries
{
    IReadOnlyList<FolioIndexTarget> Targets(FolioEditor editor);

    bool Insert(FolioEditor editor, string index, string entry, string key);
}

public sealed class FolioIndexEntries(IIndexEditing indexes, ITextCatalog texts) : IFolioIndexEntries
{
    public const string Alphabetical = "alphabetical";

    public IReadOnlyList<FolioIndexTarget> Targets(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var users = indexes.Indexes(editor.Document).Where(section => section.Index!.Kind == IndexKind.User)
            .Select(section => section.Index!.UserName ?? "User-Defined").Distinct();
        return
        [
            new(Alphabetical, texts["folio.index.kind.alphabetical"]),
            new("toc", texts["folio.index.kind.contents"]),
            .. users.Select(user => new FolioIndexTarget(user, user)),
        ];
    }

    public bool Insert(FolioEditor editor, string index, string entry, string key)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (entry.Trim().Length == 0 || editor.RefusesEdit()) return false;
        var trimmed = entry.Trim();
        var keyed = key.Trim();
        var at = editor.HasSelection ? editor.Selection.Start : editor.Caret;
        return editor.Change(() =>
        {
            var paragraph = editor.Services.Stories.Find(editor.Document, at.ParagraphId).Paragraph;
            var recorder = new EditRecorder(editor.Document, "index-entry").Touch(paragraph);
            var text = index == Alphabetical || keyed.Length == 0 ? trimmed : keyed + ", " + trimmed;
            paragraph.State = paragraph.State with
            {
                Marks = [.. paragraph.Marks, new IndexEntryMark(at.Offset, text, MarkEdge.Point) { Index = index, Key1 = keyed.Length > 0 && index == Alphabetical ? keyed : null }],
            };
            recorder.Commit(editor.History);
        });
    }
}

public sealed class FolioIndexEntryPanel : UserControl
{
    private readonly IActiveFolioContext _context;
    private readonly IFolioIndexEntries _entries;
    private readonly ITextCatalog _texts;
    private readonly ComboBox _index = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _entry = new();
    private readonly TextBox _key = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _insert;
    private IReadOnlyList<FolioIndexTarget> _targets = [];
    private FolioEditor? _watched;
    private bool _typed;
    private bool _seeding;

    public FolioIndexEntryPanel(IActiveFolioContext context, IFolioIndexEntries entries, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _entries = entries;
        _texts = texts;
        var metrics = DesignTokens.For(density);
        AutomationProperties.SetName(_index, texts["folio.indexEntry.index"]);
        AutomationProperties.SetName(_entry, texts["folio.indexEntry.entry"]);
        AutomationProperties.SetName(_key, texts["folio.indexEntry.key"]);
        _status.FontSize = metrics.FontSizeSmall;
        _status.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        _insert = new Button { Content = texts["folio.fields.insert"], HorizontalAlignment = HorizontalAlignment.Right };
        _insert.Classes.Add("accent");
        _insert.Click += (_, _) => Insert();
        _entry.TextChanged += (_, _) =>
        {
            if (!_seeding) _typed = (_entry.Text ?? string.Empty).Length > 0;
            _insert.IsEnabled = (_entry.Text?.Trim().Length ?? 0) > 0 && _watched is not null;
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !_insert.IsEnabled) return;
            Insert();
            e.Handled = true;
        };

        Content = new StackPanel
        {
            Margin = new Thickness(metrics.SpacingMedium),
            Spacing = metrics.SpacingSmall,
            Children =
            {
                new TextBlock { Text = texts["folio.indexEntry.panel.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = metrics.FontSizeSmall }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                Label(texts["folio.indexEntry.index"], metrics), _index,
                Label(texts["folio.indexEntry.entry"], metrics), _entry,
                Label(texts["folio.indexEntry.key"], metrics), _key,
                _insert,
                _status,
            },
        };

        context.Changed += (_, _) => Watch();
        Watch();
    }

    public ComboBox IndexBox => _index;

    public TextBox EntryBox => _entry;

    public TextBox KeyBox => _key;

    public string Status => _status.Text ?? string.Empty;

    public void Insert()
    {
        if (_watched is not { } editor || _index.SelectedIndex < 0 || _index.SelectedIndex >= _targets.Count) return;
        var entry = _entry.Text ?? string.Empty;
        if (!_entries.Insert(editor, _targets[_index.SelectedIndex].Index, entry, _key.Text ?? string.Empty)) return;
        _status.Text = _texts.Format("folio.fields.inserted", entry.Trim());
        _typed = false;
        Seed();
    }

    private void Watch()
    {
        var editor = _context.Target?.Editor;
        if (!ReferenceEquals(editor, _watched))
        {
            if (_watched is not null) _watched.Changed -= OnEditorChanged;
            _watched = editor;
            if (editor is not null) editor.Changed += OnEditorChanged;
        }
        _targets = editor is null ? [] : _entries.Targets(editor);
        var selected = Math.Max(0, _index.SelectedIndex);
        _index.ItemsSource = _targets.Select(target => target.Title).ToList();
        _index.SelectedIndex = _targets.Count == 0 ? -1 : Math.Min(selected, _targets.Count - 1);
        Seed();
    }

    private void OnEditorChanged(object? sender, EventArgs e) => Seed();

    private void Seed()
    {
        if (_typed) return;
        _seeding = true;
        _entry.Text = _watched is { HasSelection: true } editor ? editor.SelectedText().ReplaceLineEndings(" ").Trim() : string.Empty;
        _seeding = false;
        _insert.IsEnabled = (_entry.Text?.Trim().Length ?? 0) > 0 && _watched is not null;
    }

    private TextBlock Label(string text, DesignTokens.Metrics metrics) =>
        new() { Text = text, FontSize = metrics.FontSizeSmall, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, metrics.SpacingSmall, 0, 0) };
}
