using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Document.Pages;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Preview;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Pages;

public sealed record PageMargins(double Top, double Bottom, double Left, double Right)
{
    public bool Mirrored { get; init; }
}

public sealed record MarginPreset(string Key, PageMargins Margins);

public interface IMarginPresetCatalog
{
    IReadOnlyList<MarginPreset> Presets(string unit, bool mirrored);

    MarginPreset? Matching(IReadOnlyList<MarginPreset> presets, PageMargins margins);
}

public sealed class MarginPresetCatalog(IMeasureUnits units) : IMarginPresetCatalog
{
    private readonly (string Key, double Top, double Bottom, double Left, double Right, bool Mirrored)[] _centimetres =
    [
        ("normal", 2, 2, 2, 2, false),
        ("narrow", 1.27, 1.27, 1.27, 1.27, false),
        ("moderate", 2.54, 2.54, 1.91, 1.91, false),
        ("wide", 2.54, 2.54, 5.08, 5.08, false),
        ("mirrored", 2.54, 2.54, 3.18, 2.54, true),
    ];

    public IReadOnlyList<MarginPreset> Presets(string unit, bool mirrored) =>
        [.. _centimetres.Where(preset => mirrored || !preset.Mirrored).Select(preset => new MarginPreset(preset.Key,
            new PageMargins(In(preset.Top, unit), In(preset.Bottom, unit), In(preset.Left, unit), In(preset.Right, unit)) { Mirrored = preset.Mirrored }))];

    public MarginPreset? Matching(IReadOnlyList<MarginPreset> presets, PageMargins margins)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(margins);
        return presets.FirstOrDefault(preset => Near(preset.Margins.Top, margins.Top) && Near(preset.Margins.Bottom, margins.Bottom)
            && Near(preset.Margins.Left, margins.Left) && Near(preset.Margins.Right, margins.Right) && preset.Margins.Mirrored == margins.Mirrored);
    }

    private double In(double centimetres, string unit) =>
        units.Find("cm") is { } from && units.Find(unit) is { } to ? Math.Round(units.Convert(centimetres, from, to), 2) : centimetres;

    private bool Near(double first, double second) => Math.Abs(first - second) < 0.02;
}

public sealed class MarginPresetPainter(IPageSketchLayout layout) : IThumbnailPainter<MarginPreset>
{
    public Control Create(MarginPreset item, Size size)
    {
        var preview = new PagePreview(layout) { Width = size.Width, Height = size.Height };
        Repaint(preview, item);
        return preview;
    }

    public void Repaint(Control thumbnail, MarginPreset item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (thumbnail is not PagePreview preview) return;
        var margins = item.Margins;
        preview.Sketch = new PageSketch(21, 29.7)
        {
            Margins = new PageSketchMargins(margins.Left, margins.Top, margins.Right, margins.Bottom),
            Body = PageSketchBody.TextLines,
        };
    }
}

public interface IPageMarginsEditors
{
    PageMarginsEditor Create(PageMargins initial, string unit, bool offerMirrored, DesignTokens.Metrics metrics);
}

public sealed class PageMarginsEditors(
    IMarginPresetCatalog presets,
    IPageSketchLayout layout,
    IGalleryNavigator navigator,
    IMeasureUnits units,
    IIconCatalog icons,
    ITextCatalog texts) : IPageMarginsEditors
{
    public PageMarginsEditor Create(PageMargins initial, string unit, bool offerMirrored, DesignTokens.Metrics metrics) =>
        new(initial, unit, presets.Presets(unit, offerMirrored), presets, new MarginPresetPainter(layout), navigator, units, icons, texts, metrics);
}

public sealed class PageMarginsEditor : UserControl
{
    private readonly IReadOnlyList<MarginPreset> _presets;
    private readonly IMarginPresetCatalog _catalog;
    private readonly ThumbnailGallery<MarginPreset> _gallery;
    private readonly MeasureField _top;
    private readonly MeasureField _bottom;
    private readonly MeasureField _left;
    private readonly MeasureField _right;
    private bool _mirrored;
    private bool _loading;

    public PageMarginsEditor(
        PageMargins initial, string unit, IReadOnlyList<MarginPreset> presets, IMarginPresetCatalog catalog,
        IThumbnailPainter<MarginPreset> painter, IGalleryNavigator navigator, IMeasureUnits units, IIconCatalog icons,
        ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(texts);
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _mirrored = initial.Mirrored;
        var culture = CultureInfo.CurrentCulture;

        _gallery = new ThumbnailGallery<MarginPreset>(
            [.. presets.Select(preset => new ThumbnailGalleryItem<MarginPreset>(preset, texts[$"page.margins.{preset.Key}"]))],
            painter, navigator,
            new ThumbnailGalleryOptions { TileWidth = 76, ThumbnailHeight = 70, Columns = 5, AccessibleName = texts["page.margins.presets"] },
            metrics);
        _gallery.SelectionChanged += (_, preset) => Apply(preset.Margins);

        MeasureField Field(string key, double value)
        {
            var field = new MeasureField(new MeasureEntry(new MeasureOptions(unit, 0, units.Find("cm") is { } cm && units.Find(unit) is { } target ? units.Convert(50, cm, target) : 50, unit == "cm" ? 0.1 : 1), units, culture),
                value, texts, metrics, icons, texts[key], boxWidth: 64)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            field.ValueCommitted += (_, _) => Edited();
            return field;
        }

        _top = Field("page.margins.top", initial.Top);
        _bottom = Field("page.margins.bottom", initial.Bottom);
        _left = Field(initial.Mirrored ? "page.margins.inner" : "page.margins.left", initial.Left);
        _right = Field(initial.Mirrored ? "page.margins.outer" : "page.margins.right", initial.Right);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = metrics.SpacingMedium,
            RowSpacing = metrics.SpacingSmall,
        };
        Place(grid, texts["page.margins.top"], _top, 0, 0, metrics);
        Place(grid, texts["page.margins.bottom"], _bottom, 0, 2, metrics);
        Place(grid, texts[initial.Mirrored ? "page.margins.inner" : "page.margins.left"], _left, 1, 0, metrics);
        Place(grid, texts[initial.Mirrored ? "page.margins.outer" : "page.margins.right"], _right, 1, 2, metrics);

        var layout = new StackPanel { Spacing = metrics.SpacingMedium };
        layout.Children.Add(_gallery);
        layout.Children.Add(grid);
        Content = layout;
        Sync();
    }

    public event EventHandler? Changed;

    public PageMargins Margins => new(_top.Read(), _bottom.Read(), _left.Read(), _right.Read()) { Mirrored = _mirrored };

    public ThumbnailGallery<MarginPreset> Gallery => _gallery;

    public void Apply(PageMargins margins)
    {
        ArgumentNullException.ThrowIfNull(margins);
        _loading = true;
        _top.Value = margins.Top;
        _bottom.Value = margins.Bottom;
        _left.Value = margins.Left;
        _right.Value = margins.Right;
        _mirrored = margins.Mirrored;
        _loading = false;
        Sync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Edited()
    {
        if (_loading) return;
        Sync();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Sync()
    {
        if (_catalog.Matching(_presets, Margins) is { } preset) _gallery.Select(preset);
        else _gallery.Select(-1);
    }

    private void Place(Grid grid, string label, Control field, int row, int column, DesignTokens.Metrics metrics)
    {
        var caption = new TextBlock { Text = label, FontSize = metrics.FontSize, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(caption, row);
        Grid.SetColumn(caption, column);
        Grid.SetRow(field, row);
        Grid.SetColumn(field, column + 1);
        grid.Children.Add(caption);
        grid.Children.Add(field);
    }
}

public sealed record PageMarginsField(string Key, string Label, PageMargins Initial, string Unit, bool OfferMirrored, IPageMarginsEditors Editors)
    : CustomFormField(Key, Label)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = Editors.Create(Initial, Unit, OfferMirrored, metrics);
        return new FormFieldEditor(editor, () => editor.Margins, Labelled: false) { Observe = changed => editor.Changed += (_, _) => changed() };
    }
}
