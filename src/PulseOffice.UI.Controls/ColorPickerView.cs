using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Spectrum;
using PulseOffice.UI.Controls.Tabs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class ColorPickerView : UserControl
{
    private const double PanelWidth = 286;

    private static readonly string[] Hues =
        ["#EF4444", "#F97316", "#F59E0B", "#EAB308", "#84CC16", "#22C55E", "#14B8A6", "#06B6D4", "#3B82F6", "#6366F1", "#A855F7", "#EC4899"];

    private static readonly double[] Shades = [0.85, 0.65, 0.4, 0, -0.25, -0.45, -0.65];

    private static readonly string[] Greys =
        ["#000000", "#111827", "#1F2937", "#374151", "#4B5563", "#6B7280", "#9CA3AF", "#C4C9D1", "#D1D5DB", "#E5E7EB", "#F3F4F6", "#FFFFFF"];

    private readonly TextBox _hex;
    private readonly Border _preview;
    private readonly Slider? _opacity;
    private readonly SaturationValueArea _area;
    private readonly HueStrip _hueStrip;
    private readonly NumberBox[] _channels;
    private readonly PageTabStrip _tabs;
    private readonly Control _palette;
    private readonly Control _spectrum;

    private string _value;
    private SpectrumColor _hsv;
    private double _alpha;
    private bool _ready;
    private bool _syncing;

    public ColorPickerView(
        string? colour, double opacity, ITextCatalog texts, bool allowNone = false,
        InterfaceDensity density = InterfaceDensity.Compact, bool showOpacity = false)
    {
        ArgumentNullException.ThrowIfNull(texts);

        _value = Parse(colour ?? "#2563EB") is var parsed ? Hex(parsed) : "#2563EB";
        _hsv = SpectrumColor.From(Rgb(parsed));
        _alpha = double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;

        var metrics = DesignTokens.For(density);

        _preview = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            Background = Fill(_value, _alpha),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        _hex = new TextBox { Text = _value, Width = 132, FontSize = metrics.FontSize };
        _hex.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            if (_hex.Text is { Length: >= 4 } text && Color.TryParse(text.Trim(), out var typed))
                Choose(Hex(typed), notify: true, echoHex: false);
        };
        _hex.LostFocus += (_, _) => EchoHex();
        _hex.KeyDown += (_, e) =>
        {
            if (e.Key is Avalonia.Input.Key.Enter or Avalonia.Input.Key.Escape) EchoHex();
        };
        Avalonia.Automation.AutomationProperties.SetName(_hex, texts["colour.hex"]);

        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _preview, _hex },
        };

        if (allowNone)
        {
            var none = new Button
            {
                Content = texts["colour.none"],
                FontSize = metrics.FontSizeSmall,
                Padding = new Thickness(metrics.SpacingSmall, 2),
            };

            none.Click += (_, _) =>
            {
                IsNone = true;
                EchoHex();
                _preview.Background = Brushes.Transparent;
                Notify();
            };

            top.Children.Add(none);
        }

        _tabs = new PageTabStrip(metrics, [texts["colour.palette"], texts["colour.spectrum"]]) { Width = PanelWidth };
        _tabs.SelectionChanged += (_, index) => ShowSpectrum(index == 1);
        var tabs = _tabs;

        _palette = BuildPalette();

        _area = new SaturationValueArea { Width = PanelWidth, Height = 196, Colour = _hsv };
        Avalonia.Automation.AutomationProperties.SetName(_area, texts["colour.spectrumArea"]);
        _area.Picked += (_, _) => FromSpectrum(_area.Colour);

        _hueStrip = new HueStrip { Width = PanelWidth, Height = 18, Hue = _hsv.Hue };
        Avalonia.Automation.AutomationProperties.SetName(_hueStrip, texts["colour.hue"]);
        _hueStrip.Picked += (_, _) => FromSpectrum(_hsv.WithHue(_hueStrip.Hue));

        var entry = new NumberEntry(new NumericBounds(0, 255, 1), true, CultureInfo.CurrentCulture);
        _channels = Enumerable.Range(0, 3).Select(_ => new NumberBox(entry, 0, metrics, boxWidth: 44)).ToArray();
        foreach (var channel in _channels) channel.ValueCommitted += (_, _) => FromChannels();

        var channelRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto,*"), Width = PanelWidth };
        string[] names = ["R", "G", "B"];
        string[] channelNames = [texts["colour.channel.red"], texts["colour.channel.green"], texts["colour.channel.blue"]];
        for (var index = 0; index < names.Length; index++)
        {
            var label = new TextBlock
            {
                Text = names[index],
                FontSize = metrics.FontSizeSmall,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(index == 0 ? 0 : metrics.SpacingSmall, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Center,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

            Avalonia.Automation.AutomationProperties.SetName(_channels[index], channelNames[index]);
            ToolTip.SetTip(label, channelNames[index]);
            Grid.SetColumn(label, index * 2);
            Grid.SetColumn(_channels[index], index * 2 + 1);
            channelRow.Children.Add(label);
            channelRow.Children.Add(_channels[index]);
        }

        _spectrum = new StackPanel
        {
            Spacing = metrics.SpacingSmall + 2,
            Children = { _area, _hueStrip, channelRow },
        };

        var body = new StackPanel { Spacing = metrics.SpacingSmall, HorizontalAlignment = HorizontalAlignment.Left, Width = PanelWidth, Margin = new Thickness(2, 2, 4, 2), Children = { top, tabs, _palette, _spectrum } };

        if (showOpacity)
        {
            _opacity = new Slider { Minimum = 0, Maximum = 100, Value = _alpha * 100, Width = 140 };
            Avalonia.Automation.AutomationProperties.SetName(_opacity, texts["colour.opacity"]);
            _opacity.PropertyChanged += (_, args) =>
            {
                if (args.Property != RangeBase.ValueProperty) return;

                _alpha = double.IsFinite(_opacity.Value) ? Math.Clamp(_opacity.Value / 100, 0, 1) : 1;
                _preview.Background = Fill(_value, _alpha);
                Notify();
            };

            body.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = metrics.SpacingSmall,
                Children =
                {
                    new TextBlock
                    {
                        Text = texts["colour.opacity"],
                        FontSize = metrics.FontSizeSmall,
                        VerticalAlignment = VerticalAlignment.Center,
                    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                    _opacity,
                },
            });
        }

        Content = body;
        ShowChannels();
        ShowSpectrum(false);
    }

    public event EventHandler? Changed;

    public bool IsNone { get; private set; }

    public string Value => _value;

    public double Transparency => 1 - _alpha;

    public double Strength => _alpha;

    public void Select(string? colour)
    {
        if (string.IsNullOrEmpty(colour))
        {
            IsNone = true;
            EchoHex();
            _preview.Background = Brushes.Transparent;
            return;
        }

        Choose(Hex(Parse(colour)), notify: false);
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _ready = true;
    }

    private void Notify()
    {
        if (_ready) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void EchoHex()
    {
        var shown = IsNone ? string.Empty : _value;
        if (_hex.Text == shown) return;

        _syncing = true;
        _hex.Text = shown;
        _syncing = false;
    }

    private void ShowSpectrum(bool spectrum)
    {
        _palette.IsVisible = !spectrum;
        _spectrum.IsVisible = spectrum;
        _tabs.Select(spectrum ? 1 : 0);
    }

    private void FromSpectrum(SpectrumColor colour)
    {
        _hsv = colour;
        Choose(colour.ToRgb().Hex, notify: true, keepHsv: true);
    }

    private void FromChannels()
    {
        var rgb = new RgbColor((byte)_channels[0].Value, (byte)_channels[1].Value, (byte)_channels[2].Value);
        Choose(rgb.Hex, notify: true);
    }

    private void Choose(string colour, bool notify, bool echoHex = true, bool keepHsv = false)
    {
        IsNone = false;
        _value = colour;
        _preview.Background = Fill(colour, _alpha);

        if (!keepHsv)
        {
            var next = SpectrumColor.From(Rgb(Parse(colour)));
            _hsv = next.Saturation == 0 || next.Value == 0 ? next with { Hue = _hsv.Hue } : next;
        }

        _syncing = true;
        if (echoHex && _hex.Text != colour) _hex.Text = colour;
        _syncing = false;

        _area.Colour = _hsv;
        _hueStrip.Hue = _hsv.Hue;
        ShowChannels();

        if (notify) Notify();
    }

    private void ShowChannels()
    {
        var colour = Parse(_value);
        _channels[0].Value = colour.R;
        _channels[1].Value = colour.G;
        _channels[2].Value = colour.B;
    }

    private Control BuildPalette()
    {
        var swatches = new StackPanel { Spacing = 2 };

        foreach (var shade in Shades)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            foreach (var hue in Hues) row.Children.Add(Swatch(Shaded(hue, shade)));
            swatches.Children.Add(row);
        }

        var greys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var grey in Greys) greys.Children.Add(Swatch(grey));
        swatches.Children.Add(greys);

        return swatches;
    }

    private Control Swatch(string colour)
    {
        var button = new Button
        {
            Width = 22,
            Height = 22,
            Padding = default,
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Parse(colour)),
            CornerRadius = new CornerRadius(3),
            [ToolTip.TipProperty] = colour,
        };
        button.Classes.Add("swatch");

        button.Click += (_, _) => Choose(colour, notify: true);
        return button;
    }

    private static IBrush Fill(string colour, double opacity) =>
        new SolidColorBrush(Parse(colour), opacity);

    private static string Hex(Color colour) => $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";

    private static RgbColor Rgb(Color colour) => new(colour.R, colour.G, colour.B);

    private static Color Parse(string colour) =>
        Color.TryParse(colour, out var parsed) ? parsed : Colors.SteelBlue;

    private static string Shaded(string hue, double amount)
    {
        var colour = Parse(hue);

        byte Mix(byte channel) => amount >= 0
            ? (byte)(channel + (255 - channel) * amount)
            : (byte)(channel * (1 + amount));

        return $"#{Mix(colour.R):X2}{Mix(colour.G):X2}{Mix(colour.B):X2}";
    }
}
