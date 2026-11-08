using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.References;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Lists;

public sealed class ListEditorControl : UserControl
{
    private readonly EditableList _list;
    private readonly ListEditMode _mode;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly Func<string, string?>? _describe;
    private readonly StackPanel _rows = new();
    private readonly TextBox? _entry;
    private readonly Button? _add;

    public ListEditorControl(
        EditableList list, ListEditMode mode, ITextCatalog texts, DesignTokens.Metrics metrics,
        string? placeholder = null, Func<string, string?>? describe = null, ListReferencePick? pick = null)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _mode = mode;
        _metrics = metrics;
        _describe = describe;

        var layout = new StackPanel { Spacing = metrics.SpacingSmall };

        layout.Children.Add(new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                MaxHeight = 280,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _rows,
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface));

        if (mode == ListEditMode.Editable)
        {
            _entry = new TextBox { PlaceholderText = placeholder ?? texts["list.newItem"] };
            Avalonia.Automation.AutomationProperties.SetName(_entry, _entry.PlaceholderText);
            _add = new Button { Content = texts["list.add"], IsEnabled = false, VerticalAlignment = VerticalAlignment.Stretch };
            _add.Classes.Add("dialogQuiet");
            _entry.TextChanged += (_, _) => _add.IsEnabled = _list.CanAdd(_entry.Text);
            _entry.KeyDown += (_, args) =>
            {
                if (args.Key != Key.Enter) return;
                Commit();
                args.Handled = true;
            };
            _add.Click += (_, _) => Commit();

            var entryRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(pick is null ? "*,Auto" : "*,Auto,Auto"),
                ColumnSpacing = metrics.SpacingSmall,
            };
            Grid.SetColumn(_add, 1);
            entryRow.Children.Add(_entry);
            entryRow.Children.Add(_add);

            if (pick is not null)
            {
                var button = new ReferencePickButton(pick.Picker, texts, metrics);
                button.Click += async (_, _) => await PickAsync(pick).ConfigureAwait(true);
                Grid.SetColumn(button, 2);
                entryRow.Children.Add(button);
            }

            layout.Children.Add(entryRow);
        }

        _list.Changed += (_, _) => Rebuild();
        Rebuild();
        Content = layout;
    }

    public IReadOnlyList<string> Items => _list.Items;

    private async Task PickAsync(ListReferencePick pick)
    {
        var chosen = await pick.Picker.PickAsync(new ReferencePickRequest(pick.Label, string.Empty, pick.IsValid))
            .ConfigureAwait(true);

        if (chosen is { Length: > 0 }) _list.Add(chosen);
        _entry?.Focus();
    }

    private void Commit()
    {
        if (_entry is null || !_list.Add(_entry.Text)) return;
        _entry.Text = string.Empty;
        _entry.Focus();
    }

    private void Rebuild()
    {
        _rows.Children.Clear();

        if (_list.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = _texts["list.empty"],
                FontSize = _metrics.FontSizeSmall + 0.5,
                Margin = new Thickness(_metrics.SpacingMedium),
                TextWrapping = TextWrapping.Wrap,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            return;
        }

        for (var index = 0; index < _list.Count; index++) _rows.Children.Add(Row(index));
    }

    private Control Row(int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("28,*,Auto"),
            ColumnSpacing = _metrics.SpacingSmall,
            Margin = new Thickness(_metrics.SpacingSmall, 2),
            MinHeight = 32,
        };

        grid.Children.Add(new TextBlock
        {
            Text = (index + 1).ToString(CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var item = _list.Items[index];
        Control body;

        if (_mode == ListEditMode.Editable)
        {
            var box = new TextBox { Text = item, VerticalAlignment = VerticalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(box, (index + 1).ToString(CultureInfo.CurrentCulture));
            box.LostFocus += (_, _) =>
            {
                if (index < _list.Count && !_list.Replace(index, box.Text)) box.Text = _list.Items[index];
            };
            body = box;
        }
        else
        {
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = item, FontSize = _metrics.FontSize }
                .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary));
            if (_describe?.Invoke(item) is { Length: > 0 } detail)
                text.Children.Add(new TextBlock { Text = detail, FontSize = _metrics.FontSizeSmall }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));
            body = text;
        }

        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(Action("↑", "list.moveUp", _list.CanMoveUp(index), () => _list.MoveUp(index)));
        actions.Children.Add(Action("↓", "list.moveDown", _list.CanMoveDown(index), () => _list.MoveDown(index)));
        if (_mode == ListEditMode.Editable)
            actions.Children.Add(Action("✕", "list.remove", true, () => _list.Remove(index)));
        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        var row = new Border { Child = grid, BorderThickness = new Thickness(0, 0, 0, index < _list.Count - 1 ? 1 : 0) }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        return row;
    }

    private Button Action(string glyph, string tipKey, bool enabled, Action run)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 28,
            Height = 26,
            Padding = new Thickness(0),
            FontSize = _metrics.FontSize,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            IsEnabled = enabled,
        };
        button.Classes.Add("dialogQuiet");
        ToolTip.SetTip(button, _texts[tipKey]);
        Avalonia.Automation.AutomationProperties.SetName(button, _texts[tipKey]);
        button.Click += (_, _) => run();
        return button;
    }
}

public sealed record ListReferencePick(string Label, IReferencePicker Picker, Func<string, bool> IsValid);

public sealed record ListField(
    string Key, string Label, IReadOnlyList<string> Initial, ListEditMode Mode = ListEditMode.Editable,
    bool Unique = false, string? Placeholder = null, Func<string, string?>? Describe = null)
    : CustomFormField(Key, Label)
{
    public Func<string, bool>? Accepts { get; init; }

    public IReferencePicker? Picker { get; init; }

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new ListEditorControl(
            new EditableList(Initial, Unique, accepts: Accepts), Mode, texts, metrics, Placeholder, Describe,
            Picker is null ? null : new ListReferencePick(Label, Picker, Accepts ?? (_ => true)));
        return new FormFieldEditor(editor, () => editor.Items.ToList());
    }
}

