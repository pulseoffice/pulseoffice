using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public readonly record struct ListSampleRow(int Level, string Label, double LabelX, double TextX)
{
    public ListBulletPicture? Picture { get; init; }
}

public interface IListSampleRows
{
    IReadOnlyList<ListSampleRow> Rows(IReadOnlyList<ListLevelFormat> levels, IReadOnlyList<int> pattern);

    IReadOnlyList<int> Pattern(ListSampleMode mode, int levelCount);
}

public sealed class ListSampleRows(IListLabeler labeler) : IListSampleRows
{
    private readonly int[] _items = [0, 1, 1, 2, 0, 1, 0];
    private readonly int[] _headings = [0, 1, 2, 2, 1, 0, 1];

    public IReadOnlyList<int> Pattern(ListSampleMode mode, int levelCount) =>
        [.. (mode == ListSampleMode.Headings ? _headings : _items).Select(level => Math.Min(level, Math.Max(0, levelCount - 1)))];

    public IReadOnlyList<ListSampleRow> Rows(IReadOnlyList<ListLevelFormat> levels, IReadOnlyList<int> pattern)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(pattern);
        if (levels.Count == 0) return [];
        var counters = levels.Select(level => level.Start - 1).ToArray();
        var rows = new List<ListSampleRow>();
        foreach (var level in pattern)
        {
            counters[level]++;
            for (var deeper = level + 1; deeper < counters.Length; deeper++) counters[deeper] = levels[deeper].Start - 1;
            var format = levels[level];
            var text = Math.Max(format.Indent, 0);
            var label = Math.Max(0, text - format.Hanging);
            rows.Add(new ListSampleRow(level, labeler.Label(levels, level, counters), label, text) { Picture = format.Numbering == ListNumbering.Bullet ? format.Picture : null });
        }
        return rows;
    }
}

public sealed class ListSampleView : Control
{
    private readonly IListSampleRows _rows;
    private readonly IBrush _bar = new SolidColorBrush(Color.FromRgb(0xC3, 0xC9, 0xD2));
    private readonly IBrush _strong = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
    private readonly IBrush _ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
    private IReadOnlyList<ListLevelFormat> _levels = [];
    private IReadOnlyList<int> _pattern = [];
    private int _highlight = -1;
    private ListSampleMode _mode;
    private readonly Dictionary<ListBulletPicture, Avalonia.Media.Imaging.Bitmap?> _pictures = [];

    public ListSampleView(IListSampleRows rows, double width, double height)
    {
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Width = width;
        Height = height;
    }

    public IReadOnlyList<ListSampleRow> Rows => _rows.Rows(_levels, _pattern);

    public void Show(IReadOnlyList<ListLevelFormat> levels, IReadOnlyList<int> pattern, ListSampleMode mode, int highlight = -1)
    {
        _levels = levels ?? throw new ArgumentNullException(nameof(levels));
        _pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        _mode = mode;
        _highlight = highlight;
        foreach (var unused in _pictures.Keys.Where(picture => !levels.Any(level => level.Picture == picture)).ToList())
        {
            _pictures[unused]?.Dispose();
            _pictures.Remove(unused);
        }
        InvalidateVisual();
    }

    private Avalonia.Media.Imaging.Bitmap? Picture(ListBulletPicture picture)
    {
        if (_pictures.TryGetValue(picture, out var known)) return known;
        Avalonia.Media.Imaging.Bitmap? decoded;
        try
        {
            using var stream = new MemoryStream(picture.Data.ToArray(), false);
            decoded = new Avalonia.Media.Imaging.Bitmap(stream);
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            decoded = null;
        }
        _pictures[picture] = decoded;
        return decoded;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        var rows = Rows;
        if (rows.Count == 0) return;
        var pitch = Math.Max(8, (Bounds.Height - 8) / rows.Count);
        var fontSize = Math.Clamp(pitch * 0.62, 6, 13);
        var widest = rows.Max(row => row.TextX) + 60;
        var scale = Math.Min(1.2, (Bounds.Width - 12) / Math.Max(widest + 40, 1));
        var y = 4 + pitch / 2;
        foreach (var row in rows)
        {
            var emphasised = _highlight < 0 || row.Level == _highlight;
            var brush = emphasised ? _ink : _strong;
            var labelX = 6 + row.LabelX * scale;
            var textX = 6 + row.TextX * scale;
            if (row.Picture is { } picture && Picture(picture) is { } bitmap)
            {
                var height = fontSize;
                var width = height * Math.Clamp(picture.Height > 0 ? picture.Width / picture.Height : 1, 0.25, 4);
                context.DrawImage(bitmap, new Rect(labelX, y - height / 2, width, height));
                textX = Math.Max(textX, labelX + width + 3);
            }
            else if (row.Label.Length > 0)
            {
                var label = new FormattedText(row.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, fontSize, brush);
                context.DrawText(label, new Point(labelX, y - label.Height / 2));
                textX = Math.Max(textX, labelX + label.WidthIncludingTrailingWhitespace + 3);
            }
            var bar = _mode == ListSampleMode.Headings ? Math.Max(30, (Bounds.Width - textX) * (0.5 - row.Level * 0.05)) : Bounds.Width - textX - 8;
            context.FillRectangle(emphasised ? _strong : _bar, new Rect(textX, y - 1.5, Math.Max(6, bar), _mode == ListSampleMode.Headings && row.Level == 0 ? 4 : 3), 1.5f);
            y += pitch;
        }
    }
}

public sealed class ListPresetPainter(IListSampleRows rows) : IThumbnailPainter<ListPreset>
{
    private readonly int[] _pattern = [0, 0, 0];

    public Control Create(ListPreset item, Size size)
    {
        var view = new ListSampleView(rows, size.Width, size.Height);
        Repaint(view, item);
        return view;
    }

    public void Repaint(Control thumbnail, ListPreset item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (thumbnail is ListSampleView view)
            view.Show([item.Level with { Indent = 26, Hanging = 22 }], _pattern, ListSampleMode.Items);
    }
}
