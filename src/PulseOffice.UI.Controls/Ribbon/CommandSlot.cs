using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class CommandSlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => true;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new CommandSlot(item, shape, form, context);
}

public sealed class CommandSlot : RibbonSlot
{
    private const double SmallWidthLimit = 240;
    private const double LargeTextWidth = 92;
    private const double TileWidth = 72;

    private readonly RibbonShape _shape;
    private Button? _button;

    public CommandSlot(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context)
    {
        _shape = shape;
        Initialise();
    }

    protected override Control Build()
    {
        _button = _shape switch
        {
            RibbonShape.Large => Large(),
            RibbonShape.Icon => IconOnly(),
            RibbonShape.Tile => Tile(),
            _ => Small(),
        };

        _button.Click += (_, _) => Context.Invoke(Item.Command);
        return _button;
    }

    protected override void Update()
    {
        if (_button is null) return;

        _button.Classes.Set("checked", Item.IsChecked == true);
        ToolTip.SetTip(_button, Context.Tooltip(Item));
    }

    private Button Small()
    {
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Context.Icon(Item.Command.Id.Value) is { } icon)
        {
            icon.Margin = new Thickness(0, 0, Context.Metrics.SpacingSmall, 0);
            icon.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(icon);
        }

        var label = new TextBlock
        {
            Text = Item.Title,
            FontSize = Context.Metrics.FontSize,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(label, 1);
        content.Children.Add(label);

        var button = Context.Flat(content);
        button.MaxWidth = SmallWidthLimit;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;

        return button;
    }

    private Button IconOnly()
    {
        var button = Context.Flat(
            Context.Icon(Item.Command.Id.Value) ?? (object)Abbreviation(),
            width: Context.ItemHeight + 2);

        button.Padding = default;
        return button;
    }

    private Button Large()
    {
        var content = new StackPanel
        {
            Spacing = Context.Metrics.SpacingSmall,
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
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Width = LargeTextWidth,
        });

        var button = Context.Flat(content, height: double.NaN);
        button.MinWidth = 64;
        button.VerticalAlignment = VerticalAlignment.Stretch;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.Classes.Add("large");

        return button;
    }

    private Button Tile()
    {
        var content = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Context.Icon(Item.Command.Id.Value, Context.Metrics.IconSize * 1.6) is { } icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(icon);
        }

        content.Children.Add(new TextBlock
        {
            Text = Item.Title,
            FontSize = Context.Metrics.FontSizeSmall,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            MaxWidth = TileWidth - 8,
        });

        var button = Context.Flat(content, width: TileWidth, height: Context.ItemHeight * 2 + 4);
        button.Classes.Add("tile");
        button.Margin = new Thickness(2);

        return button;
    }

    private TextBlock Abbreviation() => new()
    {
        Text = Item.Title.Length > 2 ? Item.Title[..2] : Item.Title,
        FontSize = Context.Metrics.FontSizeSmall,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
