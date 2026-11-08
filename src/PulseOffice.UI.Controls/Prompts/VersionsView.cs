using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Prompts;

public sealed record VersionRow(string Name, DateTimeOffset Created, string? Author, string Comment);

public enum VersionActionKind { Save, Open, Delete, Compare }

public sealed record VersionAction(VersionActionKind Kind, int Index, string Comment);

public sealed class VersionsView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly IReadOnlyList<VersionRow> _rows;
    private readonly ListBox _list = new() { MinHeight = 200, MaxHeight = 300 };
    private readonly TextBox _comment = new();
    private readonly Button _open;
    private readonly Button _compare;
    private readonly Button _delete;

    public VersionsView(IReadOnlyList<VersionRow> rows, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        ArgumentNullException.ThrowIfNull(texts);
        var metrics = DesignTokens.For(density);

        _comment.PlaceholderText = texts["versions.comment.placeholder"];
        AutomationProperties.SetName(_comment, texts["versions.comment"]);
        AutomationProperties.SetName(_list, texts["versions.list"]);

        foreach (var row in rows.Reverse()) _list.Items.Add(new ListBoxItem { Content = Row(row, metrics, texts) });
        _list.SelectedIndex = rows.Count > 0 ? 0 : -1;

        var layout = new TwoPaneDialogLayout(metrics) { Header = new HelpTip(texts["versions.explanation"], texts, metrics) };

        var save = layout.AddSection(texts["versions.section.new"]);
        var saveButton = new Button { Content = texts["versions.saveNow"] };
        saveButton.Classes.Add("dialogPrimary");
        saveButton.Click += (_, _) => Completed?.Invoke(this, new VersionAction(VersionActionKind.Save, -1, _comment.Text ?? string.Empty));
        var saveRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto") };
        Grid.SetColumn(saveButton, 2);
        saveRow.Children.Add(_comment);
        saveRow.Children.Add(saveButton);
        save.Add(texts["versions.comment"], saveRow, editorWidth: 420);

        var saved = layout.AddSection(texts["versions.section.saved"]);
        var empty = new TextBlock { Text = texts["versions.empty"], FontSize = metrics.FontSizeSmall, Margin = new Thickness(metrics.SpacingMedium) }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        saved.Add(null, rows.Count == 0 ? empty : new PanelListFrame(metrics, _list), DialogRowKind.Wide, editorWidth: 520);

        _compare = new Button { Content = texts["versions.compareSelected"] };
        _compare.Classes.Add("dialogPrimary");
        _compare.Click += (_, _) => Act(VersionActionKind.Compare);
        _open = new Button { Content = texts["versions.openSelected"] };
        _open.Classes.Add("dialogQuiet");
        _open.Click += (_, _) => Act(VersionActionKind.Open);
        _delete = new Button { Content = texts["versions.deleteSelected"] };
        _delete.Classes.Add("dialogQuiet");
        _delete.Click += (_, _) => Act(VersionActionKind.Delete);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall };
        actions.Children.Add(_compare);
        actions.Children.Add(_open);
        actions.Children.Add(_delete);
        if (rows.Count > 0) saved.Add(null, actions, DialogRowKind.Wide);

        Content = layout;
        Footer = new DialogFooter(density).WithCancel(texts["prompt.close"], () => Completed?.Invoke(this, null));

        _list.SelectionChanged += (_, _) => Update();
        _list.DoubleTapped += (_, _) => Act(VersionActionKind.Compare);
        Update();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ListBox List => _list;

    public string Comment
    {
        get => _comment.Text ?? string.Empty;
        set => _comment.Text = value;
    }

    public void Act(VersionActionKind kind)
    {
        if (kind == VersionActionKind.Save)
        {
            Completed?.Invoke(this, new VersionAction(kind, -1, Comment));
            return;
        }

        if (SelectedIndex is { } index) Completed?.Invoke(this, new VersionAction(kind, index, string.Empty));
    }

    private int? SelectedIndex => _list.SelectedIndex >= 0 ? _rows.Count - 1 - _list.SelectedIndex : null;

    private void Update() => _compare.IsEnabled = _open.IsEnabled = _delete.IsEnabled = SelectedIndex is not null;

    private Control Row(VersionRow row, DesignTokens.Metrics metrics, ITextCatalog texts)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 2) };
        var left = new StackPanel { Spacing = 1 };
        left.Children.Add(new TextBlock { Text = row.Comment.Length > 0 ? row.Comment : row.Name, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        left.Children.Add(new TextBlock
        {
            Text = texts.Format("versions.row", row.Author ?? texts["versions.unknownAuthor"], row.Name),
            FontSize = metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
        grid.Children.Add(left);
        var date = new TextBlock
        {
            Text = row.Created.LocalDateTime.ToString("g", CultureInfo.CurrentCulture),
            FontSize = metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(metrics.SpacingMedium, 0, 0, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(date, 1);
        grid.Children.Add(date);
        AutomationProperties.SetName(grid, $"{row.Name}, {date.Text}, {row.Author}, {row.Comment}");
        return grid;
    }
}
