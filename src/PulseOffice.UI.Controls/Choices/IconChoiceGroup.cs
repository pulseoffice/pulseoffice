using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Choices;

public sealed record IconChoice(string Key, string Icon, string Label);

public sealed class IconChoiceGroup : UserControl
{
    private readonly IReadOnlyList<IconChoice> _choices;
    private readonly ExclusiveChoices _group = new();

    public IconChoiceGroup(
        IReadOnlyList<IconChoice> choices, IIconCatalog? icons, DesignTokens.Metrics metrics,
        string accessibleName, int selected = 0, bool showLabels = false, int columns = 0)
    {
        ArgumentNullException.ThrowIfNull(choices);
        _choices = choices;

        Panel row = columns > 0
            ? new UniformGrid { Columns = columns }
            : new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var choice in choices)
        {
            var item = new ToggleButton
            {
                MinWidth = metrics.ControlHeight + 4,
                Height = columns > 0 ? double.NaN : metrics.ControlHeight,
                MinHeight = metrics.ControlHeight,
                Padding = new Thickness(showLabels ? metrics.SpacingSmall : 0, columns > 0 && showLabels ? 2 : 0),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = columns > 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center,
                HorizontalAlignment = columns > 0 ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
                Margin = columns > 0 ? new Thickness(1) : new Thickness(0, 0, 2, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = Face(choice, icons, metrics, showLabels, wraps: columns > 0),
            };
            item.Classes.Add("iconChoice");
            _group.Add(item, choice.Label);
            row.Children.Add(item);
        }

        _group.Select(Math.Clamp(selected, -1, choices.Count - 1), notify: false);
        _group.Changed += (_, index) => SelectionChanged?.Invoke(this, index);

        var frame = new Border
        {
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusTight + 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = row,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.FieldBorder)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        AutomationProperties.SetName(this, accessibleName);
        AutomationProperties.SetName(frame, accessibleName);
        Content = frame;
    }

    public event EventHandler<int>? SelectionChanged;

    public IReadOnlyList<ToggleButton> Items => _group.Items;

    public int SelectedIndex
    {
        get => _group.Selected;
        set => _group.Select(value, notify: true);
    }

    public string? SelectedKey => _group.Selected >= 0 ? _choices[_group.Selected].Key : null;

    public void SelectKey(string key)
    {
        var index = _choices.ToList().FindIndex(choice => choice.Key == key);
        if (index >= 0) SelectedIndex = index;
    }

    private static Control Face(IconChoice choice, IIconCatalog? icons, DesignTokens.Metrics metrics, bool showLabel, bool wraps)
    {
        Control? glyph = !string.IsNullOrEmpty(choice.Icon) && icons?.Find(choice.Icon) is { } geometry
            ? new IconView(geometry, metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary)
            : null;

        if (glyph is null || showLabel)
        {
            var label = new TextBlock
            {
                Text = choice.Label,
                FontSize = metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = wraps ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap,
            };
            if (glyph is null) return label;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var face = new DockPanel { Children = { glyph, label } };
            DockPanel.SetDock(glyph, Dock.Left);
            glyph.Margin = new Thickness(0, 0, metrics.SpacingSmall, 0);
            return face;
        }

        return glyph;
    }
}

public sealed record IconChoiceField(string Key, string Label, IReadOnlyList<IconChoice> Choices, IIconCatalog? Icons, int Initial = 0)
    : CustomFormField(Key, Label)
{
    public bool ShowLabels { get; init; }

    public int Columns { get; init; }

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var group = new IconChoiceGroup(Choices, Icons, metrics, Label, Initial, ShowLabels, Columns);
        return new FormFieldEditor(group, () => Math.Max(group.SelectedIndex, 0)) { Observe = changed => group.SelectionChanged += (_, _) => changed() };
    }
}
