using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Printing;

public sealed record HeaderFooterZones(string Left, string Center, string Right);

public sealed class HeaderFooterZonesEditor : UserControl
{
    private readonly IHeaderFooterTokens _tokens;
    private readonly IReadOnlyList<HeaderFooterField> _fields;
    private readonly IReadOnlyList<HeaderFooterPreset> _presets;
    private readonly TextBox[] _zones;
    private readonly ComboBox _presetBox;
    private TextBox _target;
    private bool _syncing;

    public HeaderFooterZonesEditor(
        string title, HeaderFooterZones initial, IReadOnlyList<HeaderFooterField> fields, IReadOnlyList<HeaderFooterPreset> presets,
        IHeaderFooterTokens tokens, IIconCatalog? icons, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _fields = fields ?? throw new ArgumentNullException(nameof(fields));
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));

        var keys = new[] { "headerFooter.left", "headerFooter.center", "headerFooter.right" };
        var values = new[] { initial.Left, initial.Center, initial.Right };
        _zones = new TextBox[3];
        var band = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = metrics.SpacingSmall };
        for (var index = 0; index < 3; index++)
        {
            var zone = new TextBox
            {
                Text = _tokens.ToDisplay(values[index], fields),
                PlaceholderText = texts[keys[index]],
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 54,
                FontSize = metrics.FontSize,
                TextAlignment = index switch { 0 => TextAlignment.Left, 1 => TextAlignment.Center, _ => TextAlignment.Right },
                VerticalContentAlignment = VerticalAlignment.Top,
            };
            AutomationProperties.SetName(zone, title + " — " + texts[keys[index]]);
            var captured = zone;
            zone.GotFocus += (_, _) => _target = captured;
            zone.TextChanged += (_, _) =>
            {
                if (_syncing) return;
                _presetBox!.SelectedIndex = -1;
                Changed?.Invoke(this, EventArgs.Empty);
            };
            Grid.SetColumn(zone, index);
            band.Children.Add(zone);
            _zones[index] = zone;
        }
        _target = _zones[1];

        _presetBox = new ComboBox
        {
            ItemsSource = presets.Select(preset => preset.Label).ToList(),
            PlaceholderText = texts["headerFooter.presets"],
            MinWidth = 180,
            HorizontalAlignment = HorizontalAlignment.Right,
            FontSize = metrics.FontSizeSmall + 0.5,
        };
        AutomationProperties.SetName(_presetBox, title + " — " + texts["headerFooter.presets"]);
        _presetBox.SelectionChanged += (_, _) =>
        {
            if (_syncing || _presetBox.SelectedIndex < 0) return;
            var preset = _presets[_presetBox.SelectedIndex];
            Set(new HeaderFooterZones(preset.Left, preset.Center, preset.Right), keepPreset: true);
        };

        var caption = new TextBlock
        {
            Text = title.ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = metrics.FontSizeSmall - 0.5,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.6,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(caption);
        Grid.SetColumn(_presetBox, 1);
        head.Children.Add(_presetBox);

        var inserts = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4 };
        foreach (var field in fields)
        {
            var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            if (icons?.Find(field.Icon) is { } glyph) face.Children.Add(new IconView(glyph, 14) { VerticalAlignment = VerticalAlignment.Center });
            face.Children.Add(new TextBlock { Text = field.Label, FontSize = metrics.FontSizeSmall + 0.5, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = face, Padding = new Thickness(8, 3), Focusable = true };
            button.Classes.Add("dialogQuiet");
            AutomationProperties.SetName(button, texts.Format("headerFooter.insert", field.Label));
            ToolTip.SetTip(button, texts.Format("headerFooter.insert", field.Label));
            var chosen = field;
            button.Click += (_, _) => Insert(chosen);
            InsertButtons.Add(button);
            inserts.Children.Add(button);
        }

        Content = new StackPanel
        {
            Spacing = metrics.SpacingSmall,
            Children = { head, band, inserts },
        };
    }

    public event EventHandler? Changed;

    public List<Button> InsertButtons { get; } = [];

    public IReadOnlyList<TextBox> Zones => _zones;

    public ComboBox Presets => _presetBox;

    public HeaderFooterZones Value => new(Code(0), Code(1), Code(2));

    public void Target(int zone) => _target = _zones[Math.Clamp(zone, 0, 2)];

    public void Insert(HeaderFooterField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        var box = _target;
        var text = box.Text ?? string.Empty;
        var caret = Math.Clamp(box.CaretIndex, 0, text.Length);
        var token = _tokens.Token(field);
        box.Text = text.Insert(caret, token);
        box.CaretIndex = caret + token.Length;
        box.Focus();
    }

    public void Set(HeaderFooterZones zones, bool keepPreset = false)
    {
        ArgumentNullException.ThrowIfNull(zones);
        _syncing = true;
        _zones[0].Text = _tokens.ToDisplay(zones.Left, _fields);
        _zones[1].Text = _tokens.ToDisplay(zones.Center, _fields);
        _zones[2].Text = _tokens.ToDisplay(zones.Right, _fields);
        if (!keepPreset) _presetBox.SelectedIndex = -1;
        _syncing = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string Code(int zone) => _tokens.ToCode(_zones[zone].Text ?? string.Empty, _fields);
}
