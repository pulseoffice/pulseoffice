using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Choices;

public sealed record Swatch(string Key, string Label, string Fill)
{
    public string? Foreground { get; init; }

    public string? Sample { get; init; }

    public string? Caption { get; init; }
}

public sealed class SwatchStrip : UserControl
{
    private readonly IReadOnlyList<Swatch> _swatches;
    private readonly ExclusiveChoices _group = new();
    private readonly ColorFieldValue _colours = new();
    private readonly ColorPickerView? _picker;
    private readonly Border? _customMark;

    public SwatchStrip(
        IReadOnlyList<Swatch> swatches, ITextCatalog texts, DesignTokens.Metrics metrics, string accessibleName,
        string? selectedKey = null, bool allowCustom = true, IFlyoutDismissal? dismissal = null)
    {
        ArgumentNullException.ThrowIfNull(swatches);
        ArgumentNullException.ThrowIfNull(texts);
        _swatches = swatches;

        var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 2, LineSpacing = 2 };
        foreach (var swatch in swatches)
        {
            var item = new ToggleButton
            {
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(metrics.RadiusTight + 2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = Face(swatch, metrics),
            };
            item.Classes.Add("swatch");
            _group.Add(item, swatch.Label);
            row.Children.Add(item);
        }

        var index = swatches.ToList().FindIndex(swatch => swatch.Key == selectedKey);
        _group.Select(index, notify: false);
        _group.Changed += (_, chosen) =>
        {
            if (chosen < 0) return;
            CustomColour = null;
            ShowCustom();
            Changed?.Invoke(this, EventArgs.Empty);
        };

        if (allowCustom)
        {
            _customMark = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                IsVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

            _picker = new ColorPickerView(null, 1, texts, allowNone: false);
            _picker.Changed += (_, _) => Choose(_picker.Value);

            var flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft, Content = _picker };
            var flyouts = dismissal ?? new FlyoutDismissal();
            flyouts.PassThrough(flyout);

            var more = new Button
            {
                Height = metrics.ControlHeight,
                Padding = new Thickness(metrics.SpacingSmall, 0),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = metrics.SpacingSmall,
                    Children = { _customMark, new TextBlock { Text = texts["swatch.more"], FontSize = metrics.FontSize, VerticalAlignment = VerticalAlignment.Center } },
                },
            };
            AutomationProperties.SetName(more, texts["swatch.more"]);
            more.Click += (_, _) => flyouts.Toggle(flyout, more);
            MoreButton = more;
            row.Children.Add(more);
        }

        AutomationProperties.SetName(this, accessibleName);
        Content = row;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ToggleButton> Items => _group.Items;

    public Button? MoreButton { get; }

    public string? CustomColour { get; private set; }

    public string? SelectedKey => _group.Selected >= 0 ? _swatches[_group.Selected].Key : null;

    public string? Value => CustomColour ?? (_group.Selected >= 0 ? _swatches[_group.Selected].Fill : null);

    public void Select(string key)
    {
        var index = _swatches.ToList().FindIndex(swatch => swatch.Key == key);
        if (index >= 0) _group.Select(index, notify: true);
    }

    public void Choose(string colour)
    {
        var normalized = _colours.Normalize(colour);
        if (normalized.Length == 0) return;

        var match = _swatches.ToList().FindIndex(swatch => string.Equals(_colours.Normalize(swatch.Fill), normalized, StringComparison.Ordinal));
        if (match >= 0)
        {
            _group.Select(match, notify: true);
            return;
        }

        _group.Select(-1, notify: false);
        CustomColour = normalized;
        ShowCustom();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowCustom()
    {
        if (_customMark is null) return;
        _customMark.IsVisible = CustomColour is not null;
        _customMark.Background = CustomColour is not null && Color.TryParse(CustomColour, out var colour) ? new SolidColorBrush(colour) : null;
    }

    private static Control Face(Swatch swatch, DesignTokens.Metrics metrics)
    {
        var size = metrics.ControlHeight - 4;
        var tile = new Border
        {
            MinWidth = size,
            Height = size,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            Background = Color.TryParse(swatch.Fill, out var fill) ? new SolidColorBrush(fill) : Brushes.Transparent,
            Padding = new Thickness(metrics.SpacingSmall / 2, 0),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        if (swatch.Sample is { Length: > 0 } sample)
            tile.Child = new TextBlock
            {
                Text = sample,
                FontSize = metrics.FontSize,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = swatch.Foreground is { } ink && Color.TryParse(ink, out var colour) ? new SolidColorBrush(colour) : Brushes.Black,
            };

        if (swatch.Caption is not { Length: > 0 } caption) return tile;

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                tile,
                new TextBlock { Text = caption, FontSize = metrics.FontSizeSmall, HorizontalAlignment = HorizontalAlignment.Center }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
            },
        };
    }
}

public sealed record SwatchStripField(string Key, string Label, IReadOnlyList<Swatch> Swatches, string? Initial = null, bool AllowCustom = true)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var strip = new SwatchStrip(Swatches, texts, metrics, Label, Initial, AllowCustom);
        return new FormFieldEditor(strip, () => strip.Value ?? string.Empty) { Observe = changed => strip.Changed += (_, _) => changed() };
    }
}
