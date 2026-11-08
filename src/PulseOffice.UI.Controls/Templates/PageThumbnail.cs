using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Printing;

namespace PulseOffice.UI.Controls.Templates;

public sealed class PageThumbnail : Control
{
    private const double Inset = 4;

    private readonly Func<IPrintableDocument?> _source;
    private readonly IPageCanvasFactory _canvases;
    private readonly Color _accent;
    private readonly string _initial;
    private IPrintableDocument? _document;
    private bool _loaded;
    private int _page;

    public PageThumbnail(Func<IPrintableDocument?> source, Color accent, string initial, IPageCanvasFactory canvases)
    {
        _canvases = canvases ?? throw new ArgumentNullException(nameof(canvases));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _accent = accent;
        _initial = initial;
        ClipToBounds = true;
    }

    public IPrintableDocument? Document
    {
        get
        {
            if (_loaded) return _document;
            _loaded = true;
            _document = _source();
            return _document;
        }
    }

    public int PageCount => Document?.PageCount ?? 0;

    public int Page
    {
        get => _page;
        set
        {
            _page = Math.Clamp(value, 0, Math.Max(0, PageCount - 1));
            InvalidateVisual();
        }
    }

    public bool ShowsPlaceholder => Document is null;

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size).Deflate(Inset);
        if (area.Width <= 0 || area.Height <= 0) return;

        var (width, height) = Document is { } document
            ? (document.Setup.PageWidth.ToPoints(), document.Setup.PageHeight.ToPoints())
            : (210.0, 297.0);
        var scale = Math.Min(area.Width / width, area.Height / height);
        var sheet = new Rect(
            area.X + (area.Width - width * scale) / 2, area.Y + (area.Height - height * scale) / 2, width * scale, height * scale);

        context.FillRectangle(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), sheet.Translate(new Vector(1.5, 2)));
        context.FillRectangle(Brushes.White, sheet);

        if (Document is { } shown)
        {
            using (context.PushClip(sheet))
            using (context.PushTransform(Matrix.CreateTranslation(sheet.X, sheet.Y)))
                shown.PaintPage(_page, _canvases.Create(context, new PulseOffice.Foundation.Units.DocumentSize(shown.Setup.PageWidth, shown.Setup.PageHeight), scale));
        }
        else
        {
            Placeholder(context, sheet);
        }

        context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 0.5), sheet);
    }

    private void Placeholder(DrawingContext context, Rect sheet)
    {
        var band = new Rect(sheet.X, sheet.Y, sheet.Width, Math.Max(4, sheet.Height * 0.08));
        context.FillRectangle(new SolidColorBrush(_accent), band);

        var text = new FormattedText(
            _initial, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), Math.Max(8, sheet.Height * 0.22),
            new SolidColorBrush(Color.FromArgb(70, _accent.R, _accent.G, _accent.B)));
        context.DrawText(text, new Point(sheet.Center.X - text.Width / 2, sheet.Center.Y - text.Height / 2));

        var lines = new Pen(new SolidColorBrush(Color.FromRgb(0xDA, 0xDD, 0xE2)), Math.Max(0.75, sheet.Height / 120));
        for (var index = 0; index < 4; index++)
        {
            var y = sheet.Bottom - sheet.Height * (0.22 - index * 0.045);
            var right = sheet.Right - sheet.Width * (index == 3 ? 0.45 : 0.14);
            context.DrawLine(lines, new Point(sheet.X + sheet.Width * 0.14, y), new Point(right, y));
        }
    }
}
