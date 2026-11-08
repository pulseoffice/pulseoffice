using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Forms;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Macros;

public sealed class MacroEventRow
{
    private readonly MacroEventSlot _slot;
    private readonly MacroEventsPanel _panel;
    private readonly MacroEventsChrome _chrome;
    private readonly Border _dot;
    private readonly ComboBox _choice;
    private readonly TextBlock _missing;
    private readonly Button _edit;
    private readonly Button _create;
    private readonly Button _remove;
    private List<MacroChoice> _options = [];
    private bool _filling;

    public MacroEventRow(MacroEventSlot slot, MacroEventsPanel panel, MacroEventsChrome chrome)
    {
        _slot = slot ?? throw new ArgumentNullException(nameof(slot));
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _chrome = chrome ?? throw new ArgumentNullException(nameof(chrome));

        var texts = chrome.Texts;

        _dot = chrome.Dot();
        ToolTip.SetTip(_dot, texts["macroEvents.assigned"]);

        _choice = new ComboBox
        {
            Width = 220,
            MinHeight = 30,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        _choice.ItemTemplate = new FuncDataTemplate<MacroChoice>((choice, _) => Present(choice));
        _choice.SelectionChanged += (_, _) => Choose();
        Avalonia.Automation.AutomationProperties.SetName(_choice, texts[slot.TitleKey]);

        _missing = new TextBlock
        {
            FontSize = chrome.Metrics.FontSizeSmall,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        _edit = chrome.Quiet("ledger.comment.edit", texts["macroEvents.edit"], texts["macroEvents.edit.tip"],
            () => _ = _panel.EditAsync(_panel.Assignments.For(_slot.Key)));
        _create = chrome.Quiet("ui.add", texts["macroEvents.new"], texts["macroEvents.new.tip"],
            () => _panel.CreateFor(_slot));
        _remove = chrome.Quiet("ui.close", texts["macroEvents.remove"], texts["macroEvents.remove.tip"],
            () => _panel.Assignments.Clear(_slot.Key));

        View = Layout();
    }

    public Control View { get; }

    public void Refresh()
    {
        var bound = _panel.Assignments.For(_slot.Key);
        var status = _panel.StatusOf(bound);
        var macros = _panel.Macros;

        _filling = true;

        _options = [MacroChoice.None, .. macros.Select(MacroChoice.Of)];
        if (status == MacroBindingStatus.Missing) _options.Add(MacroChoice.Missing(bound!));

        _choice.ItemsSource = _options;
        _choice.SelectedIndex = Math.Max(0, _options.FindIndex(option => string.Equals(option.Name, bound, StringComparison.OrdinalIgnoreCase)));

        _filling = false;

        _dot.Opacity = status == MacroBindingStatus.Assigned ? 1 : 0;
        _missing.Text = status == MacroBindingStatus.Missing ? _chrome.Texts.Format("macroEvents.missing", bound!) : string.Empty;
        _missing.IsVisible = status == MacroBindingStatus.Missing;

        _choice.IsEnabled = !_panel.ReadOnly;
        _edit.IsEnabled = status == MacroBindingStatus.Assigned;
        _edit.IsVisible = status == MacroBindingStatus.Assigned;
        _create.IsVisible = !_panel.ReadOnly;
        _remove.IsVisible = !_panel.ReadOnly && status != MacroBindingStatus.Unassigned;
        _remove.IsEnabled = status != MacroBindingStatus.Unassigned;
    }

    private void Choose()
    {
        if (_filling || _choice.SelectedIndex < 0 || _choice.SelectedIndex >= _options.Count) return;

        _panel.Assignments.Assign(_slot.Key, _options[_choice.SelectedIndex].Name);
    }

    private Control Layout()
    {
        var metrics = _chrome.Metrics;
        var texts = _chrome.Texts;

        var heading = new DockPanel();
        DockPanel.SetDock(_dot, Dock.Left);
        heading.Children.Add(_dot);
        heading.Children.Add(_chrome.Title(texts[_slot.TitleKey]));

        var indent = new Thickness(_dot.Width + metrics.SpacingSmall, 0, 0, 0);
        var description = _chrome.Description(texts[_slot.DescriptionKey]);
        description.Margin = indent;
        _missing.Margin = indent;

        var labels = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(heading);
        labels.Children.Add(description);
        labels.Children.Add(_missing);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall / 2,
            Width = 300,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _edit, _create, _remove },
        };

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _choice, actions },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        grid.Children.Add(labels);
        grid.Children.Add(right);

        return new Border
        {
            Padding = new Thickness(metrics.SpacingLarge - 2, metrics.SpacingMedium),
            Background = Brushes.Transparent,
            Child = grid,
        };
    }

    private Control Present(MacroChoice? choice) => choice switch
    {
        { Macro: { } macro } => MacroItem(macro),
        { Name: { } name } => MissingItem(name),
        _ => NoneItem(),
    };

    private Control NoneItem() => new TextBlock
    {
        Text = _chrome.Texts["form.event.none"],
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
        TextAlignment = TextAlignment.Left,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled);

    private Control MacroItem(DocumentMacro macro)
    {
        var name = new TextBlock
        {
            Text = macro.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var item = new DockPanel { LastChildFill = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        var icon = _chrome.Icon(MacroEventsChrome.MacroIcon, 13);
        icon.Margin = new Thickness(0, 0, 6, 0);
        var pill = _chrome.LanguagePill(macro.Language);
        pill.Margin = new Thickness(6, 0, 0, 0);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(pill, Dock.Right);
        item.Children.Add(icon);
        item.Children.Add(pill);
        item.Children.Add(name);
        return item;
    }

    private Control MissingItem(string name) => new TextBlock
    {
        Text = _chrome.Texts.Format("macroEvents.missing.item", name),
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
}

public sealed record MacroChoice(string? Name, DocumentMacro? Macro)
{
    public static MacroChoice None { get; } = new(null, null);

    public static MacroChoice Of(DocumentMacro macro) => new(macro.Name, macro);

    public static MacroChoice Missing(string name) => new(name, null);
}
