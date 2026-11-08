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

public sealed record ColumnLayout(int Count, double Gap, bool Separator);

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
    private readonly DialogRow _gapRow;
    private readonly DialogRow _separatorRow;
    private bool _loading;

    public ColumnLayoutEditor(
        ColumnLayout initial, string unit, IThumbnailPainter<int> painter, IGalleryNavigator navigator, IMeasureUnits units,
        IIconCatalog icons, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
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
        _gap = new MeasureField(new MeasureEntry(new MeasureOptions(unit, 0, 1000, unit == "cm" ? 0.05 : 1), units, culture),
            initial.Gap, texts, metrics, icons, texts["page.columns.gap"], boxWidth: 64)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _gap.ValueCommitted += (_, _) => Edited();
        _separator = new CheckBox { Content = texts["page.columns.separator"], IsChecked = initial.Separator };
        AutomationProperties.SetName(_separator, texts["page.columns.separator"]);
        _separator.IsCheckedChanged += (_, _) => Edited();

        var section = new DialogSection(null, metrics);
        section.Add(texts["page.columns.count"], _count, editorWidth: 120);
        _gapRow = section.Add(texts["page.columns.gap"], _gap, editorWidth: 160);
        _separatorRow = section.Add(null, _separator, DialogRowKind.Indented, editorWidth: 240);
        var details = new Border { Child = section };
        Grid.SetIsSharedSizeScope(details, true);

        var layout = new StackPanel { Spacing = metrics.SpacingMedium };
        layout.Children.Add(_gallery);
        layout.Children.Add(details);
        Content = layout;
        Sync();
    }

    public event EventHandler? Changed;

    public ColumnLayout Layout => new((int)Math.Round(_count.Read()), _gap.Read(), _separator.IsChecked == true);

    public ThumbnailGallery<int> Gallery => _gallery;

    private void Edited()
    {
        if (_loading) return;
        _loading = true;
        Sync();
        _loading = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Sync()
    {
        var count = (int)Math.Round(_count.Read());
        _gallery.Select(Array.IndexOf(_presets, count));
        _gapRow.IsVisible = count > 1;
        _separatorRow.IsVisible = count > 1;
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
