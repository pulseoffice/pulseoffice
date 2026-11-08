using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Tables.Insert;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class TableGridSlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is TableGridSpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new TableGridSlot(item, shape, form, context);
}

public sealed class TableGridSlot : RibbonSlot
{
    private const double LargeTextWidth = 92;

    private readonly RibbonShape _shape;
    private Button? _button;
    private Flyout? _flyout;

    public TableGridSlot(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context)
    {
        _shape = shape;
        Initialise();
    }

    private TableGridSpec Spec => (TableGridSpec)Item.Control!;

    public TableSizeGrid? OpenGrid { get; private set; }

    protected override Control Build()
    {
        if (Form == RibbonForm.Popover) return Panel();

        _button = _shape switch
        {
            RibbonShape.Large => Large(),
            RibbonShape.Small => Small(),
            _ => IconOnly(),
        };
        _flyout = Context.Popover(Panel);
        _flyout.Opened += (_, _) => OpenGrid?.Focus(Avalonia.Input.NavigationMethod.Tab);
        _button.Click += (_, _) => Context.Dismissal.Toggle(_flyout, _button);
        return _button;
    }

    protected override void Update()
    {
        if (_button is not null) ToolTip.SetTip(_button, Context.Tooltip(Item));
    }

    public void Open()
    {
        if (_flyout is not null && _button is not null) Context.Dismissal.Toggle(_flyout, _button);
    }

    private Control Panel()
    {
        var model = new TableSizeModel(new TableInsertLimits(Spec.MaxRows, Spec.MaxColumns), TableGridExtent.Menu, 1, 1);
        var grid = new TableSizeGrid(model) { IdleHighlight = false, HorizontalAlignment = HorizontalAlignment.Left };
        var label = new TextBlock
        {
            FontSize = Context.Metrics.FontSize,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(2, 0, 0, 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        void Describe() => label.Text = model.Hovering || grid.IsFocused
            ? Context.Texts.Format("table.insert.menuSize", model.ShownColumns, model.ShownRows)
            : Item.Title;

        model.Changed += (_, _) => Describe();
        grid.GotFocus += (_, _) =>
        {
            grid.IdleHighlight = true;
            Describe();
        };
        grid.LostFocus += (_, _) =>
        {
            grid.IdleHighlight = false;
            Describe();
        };
        grid.Picked += (_, _) =>
        {
            _flyout?.Hide();
            Context.Invoke(Item.Command, new RibbonTableSize(model.Rows, model.Columns));
        };
        Describe();
        OpenGrid = grid;

        var caption = new TextBlock
        {
            Text = Context.Texts["table.insert.more"],
            FontSize = Context.Metrics.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var more = Context.Flat(Context.Icon(Item.Command.Id.Value) is { } dots ? Row(dots, caption) : caption);
        more.HorizontalAlignment = HorizontalAlignment.Stretch;
        more.HorizontalContentAlignment = HorizontalAlignment.Left;
        more.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        more.Click += (_, _) =>
        {
            _flyout?.Hide();
            Context.Invoke(Item.Command);
        };

        return new StackPanel
        {
            Spacing = Context.Metrics.SpacingSmall,
            Children =
            {
                label,
                grid,
                new Border { Height = 1, Margin = new Thickness(0, 2) }.Themed(Border.BackgroundProperty, Palette.Keys.Border),
                more,
            },
        };
    }

    private StackPanel Row(Control icon, Control text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = Context.Metrics.SpacingSmall,
        Children = { icon, text },
    };

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
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Width = LargeTextWidth,
        });

        if (Context.Chevron(0.6) is { } chevron)
        {
            chevron.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(chevron);
        }

        var button = Context.Flat(content, height: double.NaN);
        button.MinWidth = 64;
        button.VerticalAlignment = VerticalAlignment.Stretch;
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.Classes.Add("large");
        return button;
    }

    private Button Small()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Context.Metrics.SpacingSmall, VerticalAlignment = VerticalAlignment.Center };
        if (Context.Icon(Item.Command.Id.Value) is { } icon) content.Children.Add(icon);
        content.Children.Add(new TextBlock { Text = Item.Title, FontSize = Context.Metrics.FontSize, VerticalAlignment = VerticalAlignment.Center });
        if (Context.Chevron(0.6) is { } chevron) content.Children.Add(chevron);
        var button = Context.Flat(content);
        button.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        return button;
    }

    private Button IconOnly()
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        if (Context.Icon(Item.Command.Id.Value) is { } icon) content.Children.Add(icon);
        if (Context.Chevron(0.5) is { } chevron) content.Children.Add(chevron);
        var button = Context.Flat(content, width: Context.ItemHeight + 12);
        button.Padding = default;
        return button;
    }
}
