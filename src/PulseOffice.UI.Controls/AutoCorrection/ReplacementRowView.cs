using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed class ReplacementRowView : Border
{
    public const string Columns = "2*,Auto,3*,120,Auto";

    private readonly AutoCorrectChrome _chrome;
    private readonly ReplacementEntry _entry;
    private readonly Func<string, string, string, EntryProblem> _update;
    private readonly Action<string> _remove;
    private readonly Grid _display;
    private readonly Button _delete;

    public ReplacementRowView(
        AutoCorrectChrome chrome,
        ReplacementEntry entry,
        Func<string, string, string, EntryProblem> update,
        Action<string> remove)
    {
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _remove = remove ?? throw new ArgumentNullException(nameof(remove));

        _delete = chrome.IconButton("ui.delete", chrome.Texts["autocorrect.delete"], () => _remove(_entry.From));
        _delete.Opacity = 0;
        _display = Display();

        Padding = new Thickness(chrome.Metrics.SpacingSmall, 2);
        Background = Brushes.Transparent;
        Child = _display;
        ToolTip.SetTip(this, chrome.Texts["autocorrect.editHint"]);

        PointerEntered += (_, _) => _delete.Opacity = 1;
        PointerExited += (_, _) => _delete.Opacity = 0;
        DoubleTapped += (_, e) =>
        {
            e.Handled = true;
            Edit();
        };
    }

    public event EventHandler<EntryProblem>? Rejected;

    private Grid Display()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), MinHeight = 26 };

        Add(grid, Text(_entry.From, Palette.Keys.TextPrimary, mono: true), 0);
        Add(grid, Arrow(), 1);
        Add(grid, Text(_entry.To, Palette.Keys.TextPrimary, mono: false), 2);
        Add(grid, _entry.Origin is EntryOrigin.User or EntryOrigin.Modified ? _chrome.OriginBadge(_entry.Origin) : new Border(), 3);
        Add(grid, _delete, 4);

        return grid;
    }

    private void Edit()
    {
        var from = _chrome.Field(string.Empty);
        var to = _chrome.Field(string.Empty);
        from.Text = _entry.From;
        to.Text = _entry.To;
        Avalonia.Automation.AutomationProperties.SetName(from, _chrome.Texts["autocorrect.column.from"]);
        Avalonia.Automation.AutomationProperties.SetName(to, _chrome.Texts["autocorrect.column.to"]);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns) };
        Add(grid, from, 0);
        Add(grid, Arrow(), 1);
        Add(grid, to, 2);
        var confirm = _chrome.IconButton("ui.check", _chrome.Texts["autocorrect.save"], () => Commit(from.Text, to.Text));
        var cancel = _chrome.IconButton("ui.close", _chrome.Texts["prompt.cancel"], Cancel);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { confirm, cancel } };
        Grid.SetColumnSpan(actions, 2);
        Add(grid, actions, 3);

        void Keys(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Commit(from.Text, to.Text);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Cancel();
            }
        }

        from.KeyDown += Keys;
        to.KeyDown += Keys;

        Child = grid;
        to.AttachedToVisualTree += (_, _) => to.Focus();
    }

    private void Commit(string? from, string? to)
    {
        var problem = _update(_entry.From, from ?? string.Empty, to ?? string.Empty);
        if (problem != EntryProblem.None) Rejected?.Invoke(this, problem);
    }

    private void Cancel() => Child = _display;

    private TextBlock Text(string text, string colour, bool mono) => new TextBlock
    {
        Text = text,
        FontFamily = mono ? new FontFamily("Menlo, Consolas, monospace") : FontFamily.Default,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(_chrome.Metrics.SpacingSmall, 0),
    }.Themed(TextBlock.ForegroundProperty, colour);

    private TextBlock Arrow() => new TextBlock
    {
        Text = "→",
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(_chrome.Metrics.SpacingSmall, 0),
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled);

    private void Add(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}
