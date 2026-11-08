using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Document.Borders;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Borders;

public sealed class BorderEditor : UserControl
{
    private readonly BorderEditorModel _model;
    private readonly ITextCatalog _texts;
    private readonly IBorderFramePainter _painter;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<(BorderPosition Position, ToggleButton Button)> _sides = [];
    private readonly ListBox _dashes;
    private readonly NumberBox _width;
    private readonly ColorFieldControl _colour;

    public BorderEditor(BorderEditorModel model, ITextCatalog texts, IBorderFramePainter painter, IBorderSchemaGeometry geometry, DesignTokens.Metrics metrics)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _painter = painter ?? throw new ArgumentNullException(nameof(painter));
        ArgumentNullException.ThrowIfNull(geometry);
        _metrics = metrics;

        Schema = new BorderSchema(model, painter, geometry, texts["borderEditor.schema"], texts["borderEditor.schema.help"])
        {
            Width = 200,
            Height = 150,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        _dashes = new ListBox
        {
            ItemsSource = model.Scope.Dashes.Select(Dash).ToList(),
            SelectedIndex = Math.Max(0, IndexOf(model.Scope.Dashes, model.Stroke.Dash)),
            IsVisible = model.Scope.Dashes.Count > 1,
            MaxHeight = 140,
        };
        AutomationProperties.SetName(_dashes, texts["borderEditor.style"]);
        _dashes.SelectionChanged += (_, _) =>
        {
            if (_dashes.SelectedIndex >= 0) _model.SetDash(_model.Scope.Dashes[_dashes.SelectedIndex]);
        };

        _width = new NumberBox(new NumberEntry(new NumericBounds(0.25, 12, 0.25), false, CultureInfo.CurrentCulture), model.Stroke.WidthPoints, metrics, unit: "pt", boxWidth: 56);
        AutomationProperties.SetName(_width, texts["borderEditor.width"]);
        _width.ValueCommitted += (_, value) => _model.SetWidth(value);

        _colour = new ColorFieldControl(model.Stroke.ColorHex, false, texts, metrics);
        AutomationProperties.SetName(_colour, texts["borderEditor.color"]);
        _colour.Changed += (_, _) => _model.SetColor(_colour.Value);

        var left = new StackPanel { Spacing = metrics.SpacingSmall };
        left.Children.Add(Caption("borderEditor.presets"));
        left.Children.Add(Presets());
        left.Children.Add(Caption("borderEditor.schema"));
        left.Children.Add(Schema);
        left.Children.Add(Sides());

        var right = new StackPanel { Spacing = metrics.SpacingSmall, MinWidth = 150 };
        if (_dashes.IsVisible)
        {
            right.Children.Add(Caption("borderEditor.style"));
            right.Children.Add(_dashes);
        }
        right.Children.Add(Caption("borderEditor.width"));
        right.Children.Add(_width);
        right.Children.Add(Caption("borderEditor.color"));
        right.Children.Add(_colour);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge };
        Grid.SetColumn(right, 1);
        layout.Children.Add(left);
        layout.Children.Add(right);
        Content = layout;

        _model.Changed += (_, _) => Sync();
        Sync();
    }

    public BorderSchema Schema { get; }

    public BorderEditorModel Model => _model;

    public BorderFrame Frame => _model.Frame;

    public IReadOnlyList<ToggleButton> SideButtons => [.. _sides.Select(side => side.Button)];

    private Control Presets()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 220 };
        var sample = new BorderStroke(BorderDash.Solid, 1, BorderEditorModel.DefaultColor);
        foreach (var preset in _model.Presets)
        {
            var name = _texts[$"borderEditor.preset.{preset.Key}"];
            var button = new Button
            {
                Content = new BorderGlyph(_painter, _model.Scope, preset.Apply(BorderFrame.Empty, _model.Scope, sample)),
                Padding = new Thickness(4),
                Margin = new Thickness(0, 0, 4, 4),
            };
            button.Classes.Add("dialogQuiet");
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => _model.Apply(preset);
            panel.Children.Add(button);
        }
        return panel;
    }

    private Control Sides()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = 220 };
        var sample = new BorderStroke(BorderDash.Solid, 1, BorderEditorModel.DefaultColor);
        foreach (var position in _model.Scope.Positions)
        {
            var name = _texts[$"borderEditor.position.{Key(position)}"];
            var button = new ToggleButton
            {
                Content = new BorderGlyph(_painter, _model.Scope, BorderFrame.Empty.With(position, sample), 18),
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 4, 4),
            };
            button.Classes.Add("borderSide");
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => _model.Toggle(position);
            _sides.Add((position, button));
            panel.Children.Add(button);
        }
        return panel;
    }

    private void Sync()
    {
        foreach (var (position, button) in _sides) button.IsChecked = _model.Frame.Has(position);
        var dash = IndexOf(_model.Scope.Dashes, _model.Stroke.Dash);
        if (dash >= 0 && _dashes.SelectedIndex != dash) _dashes.SelectedIndex = dash;
        if (Math.Abs(_width.Value - _model.Stroke.WidthPoints) > 0.001) _width.Value = _model.Stroke.WidthPoints;
    }

    private Control Dash(BorderDash dash)
    {
        var name = _texts[$"borderEditor.dash.{Key(dash)}"];
        var sample = new BorderDashSample(_painter, dash);
        AutomationProperties.SetName(sample, name);
        ToolTip.SetTip(sample, name);
        return sample;
    }

    private TextBlock Caption(string key) => new TextBlock
    {
        Text = _texts[key],
        FontSize = _metrics.FontSizeSmall,
        FontWeight = FontWeight.SemiBold,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private int IndexOf(IReadOnlyList<BorderDash> dashes, BorderDash dash)
    {
        for (var i = 0; i < dashes.Count; i++)
            if (dashes[i] == dash) return i;
        return -1;
    }

    private string Key<T>(T value) where T : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}

public interface IBorderEditors
{
    BorderEditor Create(BorderFrameScope scope, BorderFrame initial, DesignTokens.Metrics metrics);
}

public sealed class BorderEditors(
    ITextCatalog texts,
    IBorderPresetCatalog presets,
    IBorderFrameEditing editing,
    IBorderFramePainter painter,
    IBorderSchemaGeometry geometry) : IBorderEditors
{
    public BorderEditors(ITextCatalog texts)
        : this(texts, new BorderPresetCatalog(), new BorderFrameEditing(), new BorderFramePainter(new BorderSchemaGeometry()), new BorderSchemaGeometry())
    {
    }

    public BorderEditor Create(BorderFrameScope scope, BorderFrame initial, DesignTokens.Metrics metrics) =>
        new(new BorderEditorModel(scope, initial, presets, editing), texts, painter, geometry, metrics);
}
