using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using PulseOffice.Formulas;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Formulas.Editor;

public sealed class FormulaEditorView : UserControl, IDialogContent, IDialogFooterOwner, PulseOffice.UI.Controls.Text.ITextEditingTarget
{
    private readonly IFormulaParser _parser;
    private readonly IFormulaDiagnostics _diagnostics;
    private readonly IFormulaCatalog _catalog;
    private readonly IFormulaVocabulary _vocabulary = FormulaVocabulary.Default;
    private readonly IReadOnlyDictionary<string, string> _texts;
    private readonly DesignTokens.Metrics _metrics;

    private readonly TextEditor _editor = new();
    private readonly FormulaPreview _preview = new();
    private readonly TextBlock _status = new();
    private readonly TextBox _search = new();
    private readonly ListBox _entries = new();
    private readonly FormulaDocumentationPanel _detail;
    private readonly WrapPanel _palette = new();
    private readonly StackPanel _categories = new() { Orientation = Orientation.Horizontal, Spacing = 2 };

    private IReadOnlyList<FormulaEntry> _shown = [];
    private ParsedFormula _parsed;
    private string _category;
    private bool _syncing;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; private set; }

    public FormulaEditorView(
        string expression,
        IReadOnlyDictionary<string, string> texts,
        InterfaceDensity density = InterfaceDensity.Compact,
        IFormulaCatalog? catalog = null,
        IFormulaDiagnostics? diagnostics = null,
        IFormulaParser? parser = null)
    {
        _parser = parser ?? new FormulaParser();
        _diagnostics = diagnostics ?? new FormulaDiagnostics();
        _catalog = catalog ?? new FormulaCatalog();
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);
        _detail = new FormulaDocumentationPanel(_texts, density);
        _parsed = _parser.ParseWithSpans(expression ?? string.Empty);
        _category = _catalog.Categories[0];

        Content = Build(expression ?? string.Empty);

        ShowCategory(_category);
        Reload();
        Refresh();
    }

    private Control Build(string expression)
    {
        _editor.Text = expression;
        _editor.ShowLineNumbers = true;
        _editor.WordWrap = true;
        _editor.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        _editor.FontSize = _metrics.FontSize + 1;
        _editor.Options.AllowScrollBelowDocument = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.TextArea.TextView.LineTransformers.Add(new FormulaColorizer(_vocabulary, BrushFor));
        _editor.TextChanged += (_, _) => Refresh();
        _editor.TextArea.Caret.PositionChanged += (_, _) => SyncFromCaret();

        _preview.NodePicked += (_, node) => SyncFromPreview(node);
        _preview.MinHeight = 170;

        _status.FontSize = _metrics.FontSizeSmall;
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Margin = new Thickness(2, _metrics.SpacingSmall, 0, 0);

        Footer = new DialogFooter()
            .WithCancel(_texts["cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["guide"], ShowGuide)
            .WithAction(_texts["confirm"], () => Completed?.Invoke(this, _editor.Text ?? string.Empty), primary: true);

        var left = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,*,Auto") };

        var paletteFrame = Surface(new ScrollViewer
        {
            Content = _palette,
            MaxHeight = 92,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        });

        _palette.ItemSpacing = 4;
        _palette.LineSpacing = 4;

        var editorFrame = Surface(_editor);
        editorFrame.MinHeight = 96;
        editorFrame.MaxHeight = 170;

        var previewFrame = Surface(_preview);

        var sourceLabel = Caption(_texts["source"]);
        var previewLabel = Caption(_texts["preview"]);

        Grid.SetRow(_categories, 0);
        Grid.SetRow(paletteFrame, 1);
        Grid.SetRow(sourceLabel, 2);
        Grid.SetRow(editorFrame, 3);
        Grid.SetRow(previewLabel, 4);
        Grid.SetRow(previewFrame, 5);
        Grid.SetRow(_status, 6);

        left.Children.Add(_categories);
        left.Children.Add(paletteFrame);
        left.Children.Add(sourceLabel);
        left.Children.Add(editorFrame);
        left.Children.Add(previewLabel);
        left.Children.Add(previewFrame);
        left.Children.Add(_status);

        foreach (var category in _catalog.Categories)
        {
            var name = category;

            var tab = new ToggleButton
            {
                Content = _texts[$"category.{category}"],
                FontSize = _metrics.FontSizeSmall,
                Padding = new Thickness(_metrics.SpacingMedium, 3),
            };

            tab.Click += (_, _) => ShowCategory(name);
            tab.Tag = name;

            _categories.Children.Add(tab);
        }

        var right = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,2*,3*"),
            Margin = new Thickness(_metrics.SpacingMedium, 0, 0, 0),
        };

        _search.PlaceholderText = _texts["search"];
        _search.TextChanged += (_, _) => Reload();
        _search.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);

        _entries.Classes.Add("compact");
        _entries.SelectionChanged += (_, _) => ShowDetail();
        _entries.DoubleTapped += (_, _) => InsertSelected();
        _entries.Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall);

        Grid.SetRow(_search, 0);
        Grid.SetRow(_entries, 1);
        Grid.SetRow(_detail, 2);

        right.Children.Add(_search);
        right.Children.Add(_entries);
        right.Children.Add(_detail);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,300") };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);

        layout.Children.Add(left);
        layout.Children.Add(right);

        _detail.ShowGuide();

        return layout;
    }

    private Border Surface(Control child)
    {
        var frame = new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            Padding = new Thickness(2),
        };

        frame.Themed(Border.BackgroundProperty, Palette.Keys.Surface);
        frame.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        return frame;
    }

    private Control Caption(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSizeSmall,
        Margin = new Thickness(0, _metrics.SpacingSmall, 0, 2),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private IBrush BrushFor(string role)
    {
        var key = role switch
        {
            "construct" or "large" => "PulseAccentText",
            "function" => Palette.Keys.Info,
            "symbol" => Palette.Keys.Warning,
            "number" => Palette.Keys.Success,
            "text" => Palette.Keys.Danger,

            _ => Palette.Keys.TextSecondary,
        };

        return this.FindResource(key) as IBrush ?? Brushes.Gray;
    }

    private void ShowCategory(string category)
    {
        _category = category;
        _palette.Children.Clear();

        foreach (var child in _categories.Children.OfType<ToggleButton>())
            child.IsChecked = (child.Tag as string) == category;

        foreach (var entry in _catalog.InCategory(category))
        {
            var snippet = entry.Snippet;

            var button = new Button
            {
                Content = new TextBlock
                {
                    Text = entry.Label,
                    FontSize = entry.Label.Length > 3 ? _metrics.FontSizeSmall : _metrics.FontSize + 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                MinWidth = 38,
                MinHeight = 28,
                Padding = new Thickness(7, 3),
            };

            ToolTip.SetTip(button, snippet);

            button.Click += (_, _) => Insert(snippet, entry.CaretOffset);

            _palette.Children.Add(button);
        }
    }

    private void Reload()
    {
        _shown = _catalog.Search(_search.Text ?? string.Empty);

        _entries.ItemsSource = _shown
            .Select(entry => $"{entry.Label}    {entry.Id}")
            .ToList();

        if (_shown.Count > 0) _entries.SelectedIndex = 0;
        else _detail.ShowEmpty(_texts["noEntry"]);
    }

    private void ShowDetail()
    {
        if (_entries.SelectedIndex < 0 || _entries.SelectedIndex >= _shown.Count) return;

        var entry = _shown[_entries.SelectedIndex];

        var title = _texts.TryGetValue($"item.{entry.Id}", out var named) ? named : entry.Id;
        var summary = entry.HasSummary && _texts.TryGetValue($"summary.{entry.Id}", out var text) ? text : null;

        _detail.Show(entry, title, summary);
    }

    private void InsertSelected()
    {
        if (_entries.SelectedIndex < 0 || _entries.SelectedIndex >= _shown.Count) return;

        var entry = _shown[_entries.SelectedIndex];

        Insert(entry.Snippet, entry.CaretOffset);
    }

    private void Insert(string snippet, int caretOffset)
    {
        using var update = _editor.Document.RunUpdate();

        if (_editor.SelectionLength > 0)
        {
            var selectionStart = _editor.SelectionStart;
            _editor.Document.Remove(selectionStart, _editor.SelectionLength);
            _editor.CaretOffset = selectionStart;
        }

        var separator = _editor.CaretOffset > 0
            && !char.IsWhiteSpace(_editor.Text[_editor.CaretOffset - 1])
                ? " "
                : string.Empty;

        var offset = _editor.CaretOffset;

        _editor.Document.Insert(offset, separator + snippet);

        _editor.CaretOffset = caretOffset >= 0
            ? offset + separator.Length + caretOffset
            : offset + separator.Length + snippet.Length;

        _editor.Focus();
    }

    private void ShowGuide()
    {
        _entries.SelectedIndex = -1;
        _detail.ShowGuide();
    }

    private void Refresh()
    {
        var source = _editor.Text ?? string.Empty;

        _parsed = _parser.ParseWithSpans(source);
        _preview.Show(_parsed.Formula);

        var problems = _diagnostics.Check(source);

        if (problems.Count == 0)
        {
            _status.Text = source.Length == 0 ? _texts["empty"] : _texts["valid"];
            _status.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        }
        else
        {
            var first = problems[0];
            var structural = problems.Any(problem => problem.Key != "unknownWord");

            _status.Text = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _texts[$"problem.{first.Key}"],
                first.Detail,
                first.Position + 1);

            _status.Themed(
                TextBlock.ForegroundProperty,
                structural ? Palette.Keys.Danger : Palette.Keys.Warning);
        }

        SyncFromCaret();
    }

    private void SyncFromCaret()
    {
        if (_syncing) return;

        var node = _parsed.Spans.DeepestAt(_parsed.Formula.Root, _editor.CaretOffset);

        _preview.Highlight(node);
    }

    private void SyncFromPreview(MathNode node)
    {
        if (!_parsed.Spans.TryGet(node, out var span)) return;

        _syncing = true;

        try
        {
            var length = _editor.Document.TextLength;
            var start = System.Math.Clamp(span.Start, 0, length);
            _editor.Select(start, System.Math.Clamp(span.Length, 0, length - start));
            _editor.Focus();
        }
        finally
        {
            _syncing = false;
        }
    }

    public void Perform(PulseOffice.UI.Controls.Text.TextEditingAction action)
    {
        switch (action)
        {
            case PulseOffice.UI.Controls.Text.TextEditingAction.SelectAll: _editor.SelectAll(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Copy: _editor.Copy(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Cut: _editor.Cut(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Paste: _editor.Paste(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Undo: _editor.Undo(); break;
            case PulseOffice.UI.Controls.Text.TextEditingAction.Redo: _editor.Redo(); break;
        }
    }
}
