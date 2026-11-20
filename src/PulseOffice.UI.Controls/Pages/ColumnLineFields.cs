using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Pages;

public enum ColumnLinePosition
{
    Top,
    Middle,
    Bottom,
}

public sealed record ColumnLine(BorderLineStyle Style, double Width, string ColorHex, int HeightPercent, ColumnLinePosition Position)
{
    public const double LargestWidth = 9;

    public static readonly ColumnLine Default = new(BorderLineStyle.Solid, 0.5, "#000000", 100, ColumnLinePosition.Top);
}

public sealed class ColumnLineFields
{
    private static readonly BorderLineStyle[] Styles = [BorderLineStyle.Solid, BorderLineStyle.Dotted, BorderLineStyle.Dashed, BorderLineStyle.Double];
    private static readonly ColumnLinePosition[] Positions = [ColumnLinePosition.Top, ColumnLinePosition.Middle, ColumnLinePosition.Bottom];

    private readonly ComboBox _style;
    private readonly NumberBox _width;
    private readonly ColorFieldControl _colour;
    private readonly NumberBox _height;
    private readonly ComboBox _position;
    private readonly List<DialogRow> _rows = [];

    public ColumnLineFields(ColumnLine initial, DialogSection section, ITextCatalog texts, DesignTokens.Metrics metrics, Action edited)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(edited);
        var culture = CultureInfo.CurrentCulture;
        _style = Choice(texts["page.columns.lineStyle"],
            [texts["page.columns.solid"], texts["border.dotted"], texts["border.dashed"], texts["border.double"]],
            Math.Max(0, Array.IndexOf(Styles, initial.Style)), edited);
        _width = new NumberBox(new NumberEntry(new NumericBounds(0.05, ColumnLine.LargestWidth), false, culture), initial.Width, metrics, boxWidth: 64);
        AutomationProperties.SetName(_width, texts["page.columns.lineWidth"]);
        _width.ValueCommitted += (_, _) => edited();
        _colour = new ColorFieldControl(initial.ColorHex, false, texts, metrics);
        AutomationProperties.SetName(_colour, texts["page.columns.lineColour"]);
        _colour.Changed += (_, _) => edited();
        _height = new NumberBox(new NumberEntry(new NumericBounds(1, 100), true, culture), Math.Clamp(initial.HeightPercent, 1, 100), metrics, boxWidth: 64);
        AutomationProperties.SetName(_height, texts["page.columns.lineHeight"]);
        _height.ValueCommitted += (_, _) => edited();
        _position = Choice(texts["page.columns.linePosition"],
            [texts["page.columns.position.top"], texts["page.columns.position.middle"], texts["page.columns.position.bottom"]],
            Math.Max(0, Array.IndexOf(Positions, initial.Position)), edited);

        _rows.Add(section.Add(texts["page.columns.lineStyle"], _style, editorWidth: 160));
        _rows.Add(section.Add(texts["page.columns.lineWidth"], _width, editorWidth: 120));
        _rows.Add(section.Add(texts["page.columns.lineColour"], _colour, editorWidth: 160));
        _rows.Add(section.Add(texts["page.columns.lineHeight"], _height, editorWidth: 120));
        _rows.Add(section.Add(texts["page.columns.linePosition"], _position, editorWidth: 160));
    }

    public ColumnLine Line => new(
        Styles[Math.Clamp(_style.SelectedIndex, 0, Styles.Length - 1)],
        _width.Read(),
        _colour.Value.Length > 0 ? _colour.Value : ColumnLine.Default.ColorHex,
        (int)Math.Round(_height.Read()),
        Positions[Math.Clamp(_position.SelectedIndex, 0, Positions.Length - 1)]);

    public bool IsVisible
    {
        set
        {
            foreach (var row in _rows) row.IsVisible = value;
        }
    }

    private ComboBox Choice(string name, IReadOnlyList<string> items, int selected, Action edited)
    {
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = items, SelectedIndex = selected };
        AutomationProperties.SetName(combo, name);
        combo.SelectionChanged += (_, _) => edited();
        return combo;
    }
}
