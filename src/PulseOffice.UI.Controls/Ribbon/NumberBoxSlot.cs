using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class NumberBoxSlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is NumberBoxSpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new NumberBoxSlot(item, form, context);
}

public sealed class NumberBoxSlot : RibbonSlot
{
    private readonly TextBox _box = new();

    public NumberBoxSlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context) => Initialise();

    private NumberBoxSpec Spec => (NumberBoxSpec)Item.Control!;

    protected override Control Build()
    {
        _box.Classes.Add("ribbonNumber");
        _box.Width = Spec.Width - 16;
        _box.Height = Context.ItemHeight;
        _box.MinHeight = 0;
        _box.FontSize = Context.Metrics.FontSize;
        _box.CornerRadius = new CornerRadius(Context.Metrics.RadiusTight, 0, 0, Context.Metrics.RadiusTight);
        _box.VerticalContentAlignment = VerticalAlignment.Center;

        _box.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                Commit();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                ShowCurrent();
                args.Handled = true;
            }
        };

        _box.LostFocus += (_, _) => Update();

        var arrow = Context.Flat(Context.Chevron(0.6), width: 16);
        arrow.Classes.Add("field");
        arrow.Padding = default;
        arrow.CornerRadius = new CornerRadius(0, Context.Metrics.RadiusTight, Context.Metrics.RadiusTight, 0);

        Flyout? flyout = null;
        flyout = Context.Popover(Presets);
        arrow.Click += (_, _) => Context.Dismissal.Toggle(flyout!, _box);

        Control Presets()
        {
            var list = new StackPanel { Spacing = 0, MinWidth = Spec.Width };

            foreach (var preset in Spec.Presets)
            {
                var option = Context.Flat(new TextBlock
                {
                    Text = Format(preset),
                    FontSize = Context.Metrics.FontSize,
                    VerticalAlignment = VerticalAlignment.Center,
                });

                option.HorizontalAlignment = HorizontalAlignment.Stretch;
                option.HorizontalContentAlignment = HorizontalAlignment.Left;
                option.Padding = new Thickness(Context.Metrics.SpacingSmall, 0);
                option.Classes.Set("checked", Spec.Current() is { } current && Math.Abs(current - preset) < 0.001);
                option.Click += (_, _) =>
                {
                    flyout?.Hide();
                    Context.Invoke(Item.Command, preset);
                };

                list.Children.Add(option);
            }

            return list;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _box, arrow },
        };
    }

    protected override void Update()
    {
        if (!_box.IsFocused) ShowCurrent();

        ToolTip.SetTip(this, Context.Tooltip(Item));
    }

    private void Commit()
    {
        var text = WithoutSuffix(_box.Text ?? string.Empty).Trim();

        if ((double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            && double.IsFinite(value))
        {
            var clamped = Math.Clamp(value, Spec.Minimum, Spec.Maximum);
            Context.Invoke(Item.Command, clamped);
            _box.Text = Format(clamped);
        }
        else
            ShowCurrent();
    }

    private string WithoutSuffix(string text) =>
        Spec.Suffix.Length > 0 ? text.Replace(Spec.Suffix, string.Empty, StringComparison.Ordinal) : text;

    private void ShowCurrent() => _box.Text = Spec.Current() is { } value ? Format(value) : string.Empty;

    private string Format(double value) =>
        value.ToString("0.#", CultureInfo.CurrentCulture) + Spec.Suffix;
}
