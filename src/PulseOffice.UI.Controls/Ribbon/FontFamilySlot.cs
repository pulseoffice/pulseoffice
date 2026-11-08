using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.UI.Commands;

namespace PulseOffice.UI.Controls.Ribbon;

public sealed class FontFamilySlotFactory : IRibbonSlotFactory
{
    public bool Accepts(RibbonItem item) => item.Control is FontFamilySpec;

    public RibbonSlot Create(RibbonItem item, RibbonShape shape, RibbonForm form, RibbonBuildContext context) =>
        new FontFamilySlot(item, form, context);
}

public sealed class FontFamilySlot : RibbonSlot
{
    private const double CompactWidth = 112;

    private Fonts.FontPicker? _picker;
    private Button? _fallback;
    private bool _updating;

    public FontFamilySlot(RibbonItem item, RibbonForm form, RibbonBuildContext context)
        : base(item, form, context) => Initialise();

    private FontFamilySpec Spec => (FontFamilySpec)Item.Control!;

    protected override Control Build()
    {
        var width = Form == RibbonForm.Compact ? CompactWidth : Spec.Width;

        if (Context.Fonts is not { } fonts)
        {
            _fallback = Context.Flat(new TextBlock { VerticalAlignment = VerticalAlignment.Center }, width: width);
            _fallback.Classes.Add("field");
            _fallback.HorizontalContentAlignment = HorizontalAlignment.Left;
            _fallback.Click += (_, _) => Context.Invoke(Item.Command);
            return _fallback;
        }

        _picker = fonts.Create(Spec.Current() ?? string.Empty);
        _picker.Width = width;
        _picker.Height = Context.ItemHeight;
        _picker.VerticalAlignment = VerticalAlignment.Center;

        _picker.FamilyChanged += (_, family) =>
        {
            if (!_updating) Context.Invoke(Item.Command, family);
        };

        return _picker;
    }

    protected override void Update()
    {
        var current = Spec.Current() ?? string.Empty;

        if (_picker is not null && _picker.Family != current)
        {
            _updating = true;
            _picker.Show(current);
            _updating = false;
        }

        if (_fallback?.Content is TextBlock text) text.Text = current;

        ToolTip.SetTip(this, Context.Tooltip(Item));
    }
}
