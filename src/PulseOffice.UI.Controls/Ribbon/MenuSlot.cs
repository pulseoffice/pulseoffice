using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class MenuSlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Menu is not null;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new MenuSlot(item, shape, form, context);
}

public sealed class MenuSlot : RibbonSlot
{
    private const double LargeTextWidth = 72;

    private readonly RibbonShape _shape;
    private Button? _button;

    public MenuSlot(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context)
    {
        _shape = shape;
        Initialise();
    }

    private IReadOnlyList<RibbonItem> Entries => Item.Menu ?? [];

    protected override Control Build()
    {
        _button = _shape == RibbonShape.Large && Form != RibbonForm.Popover ? Large() : Small();

        Flyout? flyout = null;
        flyout = Context.Popover(() => List(flyout));
        _button.Click += (_, _) => Context.Dismissal.Toggle(flyout!, _button);

        return _button;
    }

    protected override void Update()
    {
        if (_button is null) return;
        ToolTip.SetTip(_button, string.Join("\n", Entries.Select(entry => entry.Title).Prepend(Item.Title)));
    }

    private Button Large()
    {
        var content = new StackPanel
        {
            Spacing = Context.Metrics.SpacingSmall / 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Context.Icon(Item.Command.Id.Value, Context.Metrics.IconSize * 1.5) is { } icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(icon);
        }

        content.Children.Add(new TextBlock
        {
            Text = Item.Title,
            FontSize = Context.Metrics.FontSize,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = LargeTextWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (Context.Chevron(0.6) is { } chevron)
        {
            chevron.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(chevron);
        }

        var button = Context.Flat(content, height: double.NaN);
        button.MinWidth = 56;
        button.VerticalAlignment = VerticalAlignment.Stretch;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.Classes.Add("large");
        return button;
    }

    private Button Small()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Context.Metrics.SpacingSmall };

        if (Context.Icon(Item.Command.Id.Value) is { } icon)
        {
            icon.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(icon);
        }

        content.Children.Add(new TextBlock
        {
            Text = Item.Title,
            FontSize = Context.Metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (Context.Chevron(0.6) is { } chevron)
        {
            chevron.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(chevron);
        }

        var button = Context.Flat(content);
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        return button;
    }

    private Control List(Flyout? flyout)
    {
        var layout = new StackPanel { Spacing = 1, MinWidth = 220 };
        layout.Children.Add(Context.Caption(Item.Title));

        foreach (var entry in Entries)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };

            if (Context.Icon(entry.Command.Id.Value) is { } icon)
            {
                icon.Margin = new Thickness(0, 0, Context.Metrics.SpacingMedium, 0);
                icon.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(icon);
            }

            var label = new TextBlock
            {
                Text = entry.Title,
                FontSize = Context.Metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            var button = Context.Flat(row);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
            button.IsEnabled = entry.IsEnabled;
            ToolTip.SetTip(button, Context.Tooltip(entry));
            button.Click += (_, _) =>
            {
                flyout?.Hide();
                Context.Invoke(entry.Command);
            };

            layout.Children.Add(button);
        }

        return layout;
    }
}
