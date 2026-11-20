using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Conditional;

public sealed class ConditionalVisualOptions(ConditionalPainting painting, IGalleryNavigator navigator, ITextCatalog texts, DesignTokens.Metrics metrics, Func<ConditionalRule> current, Action<ConditionalRule> update)
{
    private readonly IReadOnlyList<DataBarAxis> _axes = Enum.GetValues<DataBarAxis>();
    private readonly IReadOnlyList<DataBarDirection> _directions = Enum.GetValues<DataBarDirection>();
    private readonly IReadOnlyList<ConditionalThresholdKind> _thresholdKinds =
        [ConditionalThresholdKind.Number, ConditionalThresholdKind.Percent, ConditionalThresholdKind.Percentile, ConditionalThresholdKind.Formula];

    public IEnumerable<Control> BarOptions()
    {
        var rule = current();
        var negative = new ColorFieldControl(rule.NegativeBarColorHex ?? string.Empty, true, texts, metrics);
        negative.Changed += (_, _) => update(current() with { NegativeBarColorHex = negative.Value is { Length: > 0 } hex ? hex : null });
        yield return Labelled(texts["conditional.bar.negative"], negative);

        var border = new ColorFieldControl(rule.Bar.BorderColorHex ?? string.Empty, true, texts, metrics);
        border.Changed += (_, _) => update(current() with { Bar = current().Bar with { BorderColorHex = border.Value is { Length: > 0 } hex ? hex : null } });
        yield return Labelled(texts["conditional.bar.border"], border);

        var axis = Choices(_axes.Select(item => texts["conditional.bar.axis." + Camel(item.ToString())]), IndexOf(_axes, rule.Bar.Axis), texts["conditional.bar.axis"]);
        axis.SelectionChanged += (_, _) =>
        {
            if (axis.SelectedIndex >= 0) update(current() with { Bar = current().Bar with { Axis = _axes[axis.SelectedIndex] } });
        };
        yield return Labelled(texts["conditional.bar.axis"], axis);

        var axisColour = new ColorFieldControl(rule.Bar.AxisColorHex, false, texts, metrics);
        axisColour.Changed += (_, _) =>
        {
            if (axisColour.Value is { Length: > 0 } hex) update(current() with { Bar = current().Bar with { AxisColorHex = hex } });
        };
        yield return Labelled(texts["conditional.bar.axisColour"], axisColour);

        var direction = Choices(_directions.Select(item => texts["conditional.bar.direction." + Camel(item.ToString())]), IndexOf(_directions, rule.Bar.Direction), texts["conditional.bar.direction"]);
        direction.SelectionChanged += (_, _) =>
        {
            if (direction.SelectedIndex >= 0) update(current() with { Bar = current().Bar with { Direction = _directions[direction.SelectedIndex] } });
        };
        yield return Labelled(texts["conditional.bar.direction"], direction);
    }

    public IReadOnlyList<ConditionalThreshold> IconThresholds(ConditionalRule rule)
    {
        var count = rule.IconCount;
        return rule.Thresholds.Count == count
            ? rule.Thresholds
            : [.. Enumerable.Range(0, count).Select(index => new ConditionalThreshold(ConditionalThresholdKind.Percent, Math.Round(100.0 * index / count, 2)))];
    }

    public IReadOnlyList<ConditionalIconChoice> IconChoices(ConditionalRule rule)
    {
        var count = rule.IconCount;
        return rule.CustomIcons.Count == count
            ? rule.ReverseIcons ? [.. rule.CustomIcons.Reverse()] : rule.CustomIcons
            : [.. Enumerable.Range(0, count).Select(index => new ConditionalIconChoice(rule.IconSetName, rule.ReverseIcons ? count - 1 - index : index))];
    }

    public Control IconRules()
    {
        var rule = current();
        var count = rule.IconCount;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            ColumnSpacing = metrics.SpacingSmall,
            RowSpacing = metrics.SpacingSmall,
        };
        for (var row = 0; row < count; row++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var row = 0; row < count; row++)
        {
            var position = count - 1 - row;
            var icon = new ConditionalIconPicker(painting, navigator, texts, metrics, IconChoices(rule)[position], texts.Format("conditional.icons.icon", row + 1), IconCell);
            icon.Picked += (_, picked) =>
            {
                var choices = IconChoices(current()).ToList();
                choices[position] = picked;
                update(current() with { CustomIcons = choices, ReverseIcons = false });
            };
            Place(grid, icon, row, 0);
            if (position == 0)
            {
                Place(grid, new TextBlock { Text = texts["conditional.icons.otherwise"], VerticalAlignment = VerticalAlignment.Center }, row, 1);
                continue;
            }
            var threshold = IconThresholds(rule)[position];
            var comparison = Choices([">=", ">"], threshold.GreaterOrEqual ? 0 : 1, texts["conditional.icons.comparison"]);
            comparison.MinWidth = 64;
            comparison.SelectionChanged += (_, _) => Threshold(position, item => item with { GreaterOrEqual = comparison.SelectedIndex == 0 });
            var value = new TextBox
            {
                Text = threshold.Kind == ConditionalThresholdKind.Formula ? threshold.Formula ?? string.Empty : threshold.Value.ToString(CultureInfo.CurrentCulture),
                MinWidth = 90,
            };
            AutomationProperties.SetName(value, texts["conditional.icons.value"]);
            value.PropertyChanged += (_, change) =>
            {
                if (change.Property != TextBox.TextProperty) return;
                var text = value.Text ?? string.Empty;
                Threshold(position, item => item.Kind == ConditionalThresholdKind.Formula
                    ? item with { Formula = text }
                    : double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number) ? item with { Value = number } : item);
            };
            var kind = Choices(_thresholdKinds.Select(item => texts["conditional.threshold." + Camel(item.ToString())]), Math.Max(0, IndexOf(_thresholdKinds, threshold.Kind)), texts["conditional.icons.type"]);
            kind.SelectionChanged += (_, _) =>
            {
                if (kind.SelectedIndex < 0) return;
                var chosen = _thresholdKinds[kind.SelectedIndex];
                Threshold(position, item => chosen == ConditionalThresholdKind.Formula
                    ? item with { Kind = chosen, Formula = value.Text ?? string.Empty }
                    : item with { Kind = chosen, Formula = null });
            };
            Place(grid, comparison, row, 1);
            Place(grid, value, row, 2);
            Place(grid, kind, row, 3);
        }
        return Labelled(texts["conditional.icons.rules"], grid);
    }

    private void Threshold(int position, Func<ConditionalThreshold, ConditionalThreshold> change)
    {
        var thresholds = IconThresholds(current()).ToList();
        thresholds[position] = change(thresholds[position]);
        update(current() with { Thresholds = thresholds });
    }

    private Control IconCell(ConditionalIconChoice choice) =>
        choice.IsNone
            ? new TextBlock { Text = texts["conditional.icons.none"], VerticalAlignment = VerticalAlignment.Center }
            : new IconThumbnail(painting, choice) { Width = 18, Height = 18 };

    private ComboBox Choices(IEnumerable<string> items, int selected, string name)
    {
        var box = new ComboBox { ItemsSource = items.ToList(), SelectedIndex = selected, MinWidth = 160 };
        AutomationProperties.SetName(box, name);
        return box;
    }

    private void Place(Grid grid, Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    private int IndexOf<T>(IReadOnlyList<T> items, T item) => items.ToList().IndexOf(item);

    private Control Labelled(string caption, Control editor) =>
        new StackPanel
        {
            Spacing = metrics.SpacingSmall / 2,
            Children =
            {
                new TextBlock { Text = caption, FontSize = metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                editor,
            },
        };

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private sealed class IconThumbnail(ConditionalPainting painting, ConditionalIconChoice choice) : Control
    {
        public override void Render(DrawingContext context) => painting.Icon(context, new Rect(Bounds.Size), choice);
    }
}
