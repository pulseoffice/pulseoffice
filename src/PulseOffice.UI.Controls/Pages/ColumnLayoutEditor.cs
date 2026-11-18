using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Document.Pages;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Pages;

public sealed record ColumnWidth(double Width, double Gap);

public sealed record ColumnLayout(int Count, double Gap, bool Separator)
{
    public IReadOnlyList<ColumnWidth> Widths { get; init; } = [];

    public double TextWidth { get; init; } = 17;

    public bool Uneven => Count > 1 && Widths.Count == Count;
}

public sealed class ColumnPresetPainter(IPageSketchLayout layout) : IThumbnailPainter<int>
{
    public Control Create(int item, Size size)
    {
        var preview = new PagePreview(layout) { Width = size.Width, Height = size.Height };
        Repaint(preview, item);
        return preview;
    }

    public void Repaint(Control thumbnail, int item)
    {
        if (thumbnail is PagePreview preview)
            preview.Sketch = new PageSketch(21, 29.7) { Margins = new PageSketchMargins(2, 2, 2, 2), Columns = item, ColumnGap = 1.2 };
    }
}

public interface IColumnLayoutEditors
{
    ColumnLayoutEditor Create(ColumnLayout initial, string unit, DesignTokens.Metrics metrics);
}

public sealed class ColumnLayoutEditors(IPageSketchLayout layout, IGalleryNavigator navigator, IMeasureUnits units, IIconCatalog icons, ITextCatalog texts) : IColumnLayoutEditors
{
    public ColumnLayoutEditor Create(ColumnLayout initial, string unit, DesignTokens.Metrics metrics) =>
        new(initial, unit, new ColumnPresetPainter(layout), navigator, units, icons, texts, metrics);
}

public sealed class ColumnLayoutEditor : UserControl
{
    private const int MaximumColumns = 10;

    private readonly int[] _presets = [1, 2, 3, 4];
    private readonly ThumbnailGallery<int> _gallery;
    private readonly NumberBox _count;
    private readonly MeasureField _gap;
    private readonly CheckBox _separator;
    private readonly CheckBox _equal;
    private readonly DialogRow _gapRow;
    private readonly DialogRow _separatorRow;
    private readonly DialogRow _equalRow;
    private readonly Border _widthsHost = new();
    private readonly List<(MeasureField Width, MeasureField? Gap)> _widths = [];
    private readonly string _unit;
    private readonly double _textWidth;
    private readonly IMeasureUnits _units;
    private readonly IIconCatalog _icons;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private bool _loading;

    public ColumnLayoutEditor(
        ColumnLayout initial, string unit, IThumbnailPainter<int> painter, IGalleryNavigator navigator, IMeasureUnits units,
        IIconCatalog icons, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
        _unit = unit;
        _textWidth = initial.TextWidth > 0 ? initial.TextWidth : 17;
        _units = units;
        _icons = icons;
        _texts = texts;
        _metrics = metrics;
        var culture = CultureInfo.CurrentCulture;
        _count = new NumberBox(new NumberEntry(new NumericBounds(1, MaximumColumns), true, culture), Math.Clamp(initial.Count, 1, MaximumColumns), metrics, boxWidth: 64);
        _gallery = new ThumbnailGallery<int>(
            [.. _presets.Select(count => new ThumbnailGalleryItem<int>(count, texts[$"page.columns.{count}"]))],
            painter, navigator,
            new ThumbnailGalleryOptions { TileWidth = 76, ThumbnailHeight = 70, Columns = 4, AccessibleName = texts["page.columns.presets"] },
            metrics);
        _gallery.SelectionChanged += (_, count) =>
        {
            _count.Value = count;
            Edited();
        };

        _count.ValueCommitted += (_, _) => Edited();
        _gap = Measure(initial.Gap, texts["page.columns.gap"]);
        _gap.ValueCommitted += (_, _) => Edited();
        _separator = new CheckBox { Content = texts["page.columns.separator"], IsChecked = initial.Separator };
        AutomationProperties.SetName(_separator, texts["page.columns.separator"]);
        _separator.IsCheckedChanged += (_, _) => Edited();
        _equal = new CheckBox { Content = texts["page.columns.equal"], IsChecked = !initial.Uneven };
        AutomationProperties.SetName(_equal, texts["page.columns.equal"]);
        _equal.IsCheckedChanged += (_, _) => Edited();

        var section = new DialogSection(null, metrics);
        section.Add(texts["page.columns.count"], _count, editorWidth: 120);
        _gapRow = section.Add(texts["page.columns.gap"], _gap, editorWidth: 160);
        _separatorRow = section.Add(null, _separator, DialogRowKind.Indented, editorWidth: 240);
        _equalRow = section.Add(null, _equal, DialogRowKind.Indented, editorWidth: 240);
        var details = new StackPanel { Spacing = metrics.SpacingSmall };
        details.Children.Add(section);
        details.Children.Add(_widthsHost);
        Grid.SetIsSharedSizeScope(details, true);

        var layout = new StackPanel { Spacing = metrics.SpacingMedium };
        layout.Children.Add(_gallery);
        layout.Children.Add(details);
        Content = layout;
        Rebuild(initial.Uneven ? initial.Widths : []);
        Sync();
    }

    public event EventHandler? Changed;

    public ColumnLayout Layout
    {
        get
        {
            var count = (int)Math.Round(_count.Read());
            var uneven = count > 1 && _equal.IsChecked != true && _widths.Count == count;
            return new ColumnLayout(count, _gap.Read(), _separator.IsChecked == true)
            {
                TextWidth = _textWidth,
                Widths = uneven ? [.. _widths.Select(pair => new ColumnWidth(pair.Width.Read(), pair.Gap?.Read() ?? 0))] : [],
            };
        }
    }

    public ThumbnailGallery<int> Gallery => _gallery;

    private MeasureField Measure(double value, string name)
    {
        var field = new MeasureField(new MeasureEntry(new MeasureOptions(_unit, 0, 1000, _unit == "cm" ? 0.05 : 1), _units, CultureInfo.CurrentCulture),
            value, _texts, _metrics, _icons, name, boxWidth: 64)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        return field;
    }

    private void Edited()
    {
        if (_loading) return;
        _loading = true;
        var count = (int)Math.Round(_count.Read());
        var wanted = count > 1 && _equal.IsChecked != true;
        if (wanted && _widths.Count != count) Rebuild(Equal(count));
        if (!wanted && _widths.Count > 0) Rebuild([]);
        Sync();
        _loading = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<ColumnWidth> Equal(int count)
    {
        var gap = _gap.Read();
        var width = Math.Max(0.5, (_textWidth - gap * (count - 1)) / count);
        return [.. Enumerable.Range(0, count).Select(index => new ColumnWidth(width, index < count - 1 ? gap : 0))];
    }

    private void Rebuild(IReadOnlyList<ColumnWidth> widths)
    {
        _widths.Clear();
        if (widths.Count == 0)
        {
            _widthsHost.Child = null;
            return;
        }
        var section = new DialogSection(null, _metrics);
        for (var index = 0; index < widths.Count; index++)
        {
            var label = _texts.Format("page.columns.column", index + 1);
            var width = Measure(widths[index].Width, label + " " + _texts["page.columns.width"]);
            width.ValueCommitted += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
            MeasureField? gap = null;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingMedium };
            row.Children.Add(width);
            if (index < widths.Count - 1)
            {
                gap = Measure(widths[index].Gap, label + " " + _texts["page.columns.gap"]);
                gap.ValueCommitted += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
                row.Children.Add(new TextBlock { Text = _texts["page.columns.after"], VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(gap);
            }
            section.Add(label, row, editorWidth: 320);
            _widths.Add((width, gap));
        }
        _widthsHost.Child = section;
    }

    private void Sync()
    {
        var count = (int)Math.Round(_count.Read());
        _gallery.Select(Array.IndexOf(_presets, count));
        var uneven = count > 1 && _equal.IsChecked != true;
        _gapRow.IsVisible = count > 1 && !uneven;
        _separatorRow.IsVisible = count > 1;
        _equalRow.IsVisible = count > 1;
        _widthsHost.IsVisible = uneven;
    }
}

public sealed record ColumnLayoutField(string Key, string Label, ColumnLayout Initial, string Unit, IColumnLayoutEditors Editors) : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = Editors.Create(Initial, Unit, metrics);
        return new FormFieldEditor(editor, () => editor.Layout, Labelled: false) { Observe = changed => editor.Changed += (_, _) => changed() };
    }
}
