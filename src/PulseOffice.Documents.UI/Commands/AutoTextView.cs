using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Documents.Blocks;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Commands;

public enum AutoTextAction
{
    Insert,
    Create,
    Delete,
}

public sealed record AutoTextChoice(AutoTextAction Action, string Name);

public sealed class AutoTextView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly IAutoTextLibrary _library;
    private readonly IBlockTree _tree;
    private readonly ITextCatalog _texts;
    private readonly ListBox _entries = new() { MinHeight = 220, MaxHeight = 260 };
    private readonly TextBox _search = new();
    private readonly TextBox _name = new();
    private readonly StackPanel _preview = new() { Spacing = 4 };
    private readonly TextBlock _empty = new();
    private readonly Button _insert;
    private readonly Button _delete;
    private readonly Button _create;
    private readonly DialogFooter _footer;
    private IReadOnlyList<string> _shown = [];

    public event EventHandler<object?>? Completed;

    public AutoTextView(IAutoTextLibrary library, IBlockTree tree, ITextCatalog texts, bool hasSelection, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(texts);
        _library = library;
        _tree = tree;
        _texts = texts;
        var metrics = DesignTokens.For(density);

        _search.PlaceholderText = texts["folio.fields.search"];
        AutomationProperties.SetName(_search, texts["folio.fields.search"]);
        AutomationProperties.SetName(_entries, texts["folio.autotext.entry"]);
        _name.PlaceholderText = texts["folio.autotext.name"];
        AutomationProperties.SetName(_name, texts["folio.autotext.name"]);
        _empty.Text = texts["folio.autotext.none"];
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _footer = new DialogFooter(density)
            .WithCancel(texts["folio.fields.close"], () => Completed?.Invoke(this, null))
            .WithAction(texts["folio.autotext.delete"], () => Finish(AutoTextAction.Delete), out _delete)
            .WithAction(texts["folio.autotext.insert"], () => Finish(AutoTextAction.Insert), out _insert, primary: true);

        _create = new Button { Content = texts["folio.autotext.new"], IsEnabled = false };
        ToolTip.SetTip(_create, texts[hasSelection ? "folio.autotext.new.hint" : "folio.autotext.needSelection"]);
        _create.Click += (_, _) =>
        {
            if (_name.Text?.Trim() is { Length: > 0 } name) Completed?.Invoke(this, new AutoTextChoice(AutoTextAction.Create, name));
        };
        _name.TextChanged += (_, _) => _create.IsEnabled = hasSelection && (_name.Text?.Trim().Length ?? 0) > 0;

        _search.TextChanged += (_, _) => Fill();
        _search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Down || _entries.ItemCount == 0) return;
            _entries.SelectedIndex = Math.Max(0, _entries.SelectedIndex);
            _entries.Focus(NavigationMethod.Directional);
            e.Handled = true;
        };
        _entries.SelectionChanged += (_, _) => ShowPreview();
        _entries.DoubleTapped += (_, _) => Finish(AutoTextAction.Insert);
        _entries.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Finish(AutoTextAction.Insert);
                e.Handled = true;
            }
        };

        var left = new DockPanel { LastChildFill = true, Width = 220 };
        _search.Margin = new Thickness(0, 0, 0, metrics.SpacingSmall);
        DockPanel.SetDock(_search, Dock.Top);
        left.Children.Add(_search);
        left.Children.Add(new Panel { Children = { Framed(_entries, metrics), _empty } });

        var paper = new Border
        {
            Background = Brushes.White,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12),
            Height = 220,
            ClipToBounds = true,
            Child = _preview,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        AutomationProperties.SetName(paper, texts["folio.fields.preview"]);

        var create = new DialogSection(texts["folio.autotext.new"], metrics);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingSmall };
        Grid.SetColumn(_create, 1);
        row.Children.Add(_name);
        row.Children.Add(_create);
        create.Add(texts["folio.autotext.name"], row, editorWidth: 300);

        var right = new StackPanel { Spacing = metrics.SpacingMedium, Children = { paper, create } };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = texts["folio.autotext.explanation"], TextWrapping = TextWrapping.Wrap, FontSize = metrics.FontSize }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                grid,
            },
        };
        Fill();
    }

    public Control? Footer => _footer;

    public double? PreferredWidth => 640;

    public ListBox Entries => _entries;

    public TextBox Search => _search;

    public TextBox NameBox => _name;

    public Button CreateButton => _create;

    public IReadOnlyList<string> PreviewLines => [.. _preview.Children.OfType<TextBlock>().Select(line => line.Text ?? string.Empty)];

    private string? Selected => _entries.SelectedIndex >= 0 && _entries.SelectedIndex < _shown.Count ? _shown[_entries.SelectedIndex] : null;

    private void Fill()
    {
        var filter = _search.Text?.Trim() ?? string.Empty;
        _shown = [.. _library.Names.Where(name => filter.Length == 0 || name.Contains(filter, StringComparison.CurrentCultureIgnoreCase))];
        _entries.ItemsSource = _shown;
        _entries.SelectedIndex = _shown.Count > 0 ? 0 : -1;
        _entries.IsVisible = _shown.Count > 0;
        _empty.IsVisible = _shown.Count == 0;
        ShowPreview();
    }

    private void ShowPreview()
    {
        _preview.Children.Clear();
        var name = Selected;
        _insert.IsEnabled = name is not null;
        _delete.IsEnabled = name is not null;
        if (name is null || _library.Load(name) is not { } fragment) return;
        foreach (var paragraph in _tree.Paragraphs(fragment.Blocks).Take(12))
            _preview.Children.Add(new TextBlock { Text = paragraph.Text.Length == 0 ? " " : paragraph.Text, Foreground = Brushes.Black, TextWrapping = TextWrapping.Wrap });
    }

    private void Finish(AutoTextAction action)
    {
        if (Selected is { } name) Completed?.Invoke(this, new AutoTextChoice(action, name));
    }

    private Border Framed(Control content, DesignTokens.Metrics metrics) => new Border
    {
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(metrics.RadiusTight),
        Padding = new Thickness(2),
        Child = content,
    }.Themed(Border.BorderBrushProperty, Palette.Keys.FieldBorder)
     .Themed(Border.BackgroundProperty, Palette.Keys.Surface);
}
