using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;

namespace PulseOffice.UI.Controls.Printing;

public enum PreviewLayout
{
    Single,
    Facing,
    Grid,
    Book,
}

public enum PreviewZoom
{
    Fit,
    FitWidth,
    Factor,
}

public interface IPreviewSpreads
{
    IReadOnlyList<int> Slots(PreviewLayout layout, int first, int columns, int rows);

    int Step(PreviewLayout layout, int first, int columns, int rows, int direction, int pageCount);

    int Align(PreviewLayout layout, int page, int columns, int rows);

    (int Columns, int Rows) Shape(PreviewLayout layout, int columns, int rows);
}

public sealed class PreviewSpreads : IPreviewSpreads
{
    public IReadOnlyList<int> Slots(PreviewLayout layout, int first, int columns, int rows) => layout switch
    {
        PreviewLayout.Facing => [first, first + 1],
        PreviewLayout.Grid => [.. Enumerable.Range(first, Math.Max(1, columns * rows))],
        PreviewLayout.Book => first == 0 ? [-1, 0] : [first, first + 1],
        _ => [first],
    };

    public int Step(PreviewLayout layout, int first, int columns, int rows, int direction, int pageCount)
    {
        var next = layout switch
        {
            PreviewLayout.Book when first == 0 => direction > 0 ? 1 : 0,
            PreviewLayout.Book => first + direction * 2,
            PreviewLayout.Facing => first + direction * 2,
            PreviewLayout.Grid => first + direction * Math.Max(1, columns * rows),
            _ => first + direction,
        };
        if (layout == PreviewLayout.Book && next < 1) return 0;
        return next >= pageCount ? first : Math.Max(0, next);
    }

    public int Align(PreviewLayout layout, int page, int columns, int rows) => layout switch
    {
        PreviewLayout.Facing => page - page % 2,
        PreviewLayout.Grid => page - page % Math.Max(1, columns * rows),
        PreviewLayout.Book => page == 0 ? 0 : page - (page + 1) % 2,
        _ => page,
    };

    public (int Columns, int Rows) Shape(PreviewLayout layout, int columns, int rows) => layout switch
    {
        PreviewLayout.Facing or PreviewLayout.Book => (2, 1),
        PreviewLayout.Grid => (Math.Max(1, columns), Math.Max(1, rows)),
        _ => (1, 1),
    };
}

public sealed class PrintPagesView : Control
{
    private const double Gap = 18;
    private const double Shadow = 3;

    private readonly IPreviewSpreads _spreads;
    private readonly IPageCanvasFactory _canvases;

    public PrintPagesView(IPreviewSpreads spreads, IPageCanvasFactory canvases)
    {
        _spreads = spreads;
        _canvases = canvases ?? throw new ArgumentNullException(nameof(canvases));
        ClipToBounds = true;
    }

    public IPrintableDocument? Document
    {
        get;
        set
        {
            field = value;
            Refresh();
        }
    }

    public int First
    {
        get;
        set
        {
            field = Math.Max(0, value);
            Refresh();
        }
    }

    public PreviewLayout Layout
    {
        get;
        set
        {
            field = value;
            Refresh();
        }
    } = PreviewLayout.Single;

    public int Columns { get; set; } = 3;

    public int Rows { get; set; } = 2;

    public PreviewZoom Zoom
    {
        get;
        set
        {
            field = value;
            Refresh();
        }
    } = PreviewZoom.Fit;

    public double Factor
    {
        get;
        set
        {
            field = Math.Clamp(value, 0.1, 6);
            Refresh();
        }
    } = 1;

    public IReadOnlyList<int> Visible => Document is null ? [] : [.. _spreads.Slots(Layout, First, Columns, Rows).Where(page => page >= 0 && page < Document.PageCount)];

    public void Refresh()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private (double Width, double Height) Page()
    {
        if (Document is null) return (0, 0);
        return (Document.Setup.PageWidth.ToPoints(), Document.Setup.PageHeight.ToPoints());
    }

    private (double Width, double Height) Block()
    {
        var (width, height) = Page();
        var (columns, rows) = _spreads.Shape(Layout, Columns, Rows);
        return (columns * width + (columns - 1) * Gap, rows * height + (rows - 1) * Gap);
    }

    private double Scale(Size area)
    {
        var (width, height) = Block();
        if (width <= 0 || height <= 0) return 0;
        var usable = new Size(Math.Max(1, area.Width - Gap * 2), Math.Max(1, area.Height - Gap * 2));
        return Zoom switch
        {
            PreviewZoom.Fit => Math.Min(usable.Width / width, usable.Height / height),
            PreviewZoom.FitWidth => usable.Width / width,
            _ => Factor,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, height) = Block();
        if (width <= 0 || height <= 0 || Zoom == PreviewZoom.Fit) return default;
        var scale = Zoom == PreviewZoom.FitWidth && !double.IsInfinity(availableSize.Width)
            ? Math.Max(0.05, (availableSize.Width - Gap * 2) / width)
            : Factor;
        return new Size(width * scale + Gap * 2, height * scale + Gap * 2);
    }

    public override void Render(DrawingContext context)
    {
        if (Document is not { PageCount: > 0 } document) return;
        var scale = Scale(Bounds.Size);
        if (scale <= 0) return;
        var (pageWidth, pageHeight) = Page();
        var (blockWidth, blockHeight) = Block();
        var (columns, _) = _spreads.Shape(Layout, Columns, Rows);
        var origin = new Point(
            Math.Max(Gap, (Bounds.Width - blockWidth * scale) / 2),
            Math.Max(Gap, (Bounds.Height - blockHeight * scale) / 2));
        var size = new DocumentSize(document.Setup.PageWidth, document.Setup.PageHeight);
        var slots = _spreads.Slots(Layout, First, Columns, Rows);
        var shadow = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0));
        for (var slot = 0; slot < slots.Count; slot++)
        {
            var page = slots[slot];
            if (page < 0 || page >= document.PageCount) continue;
            var column = slot % columns;
            var row = slot / columns;
            var left = origin.X + column * (pageWidth + Gap) * scale;
            var top = origin.Y + row * (pageHeight + Gap) * scale;
            var sheet = new Rect(left, top, pageWidth * scale, pageHeight * scale);
            context.FillRectangle(shadow, sheet.Translate(new Vector(Shadow, Shadow)));
            context.FillRectangle(Brushes.White, sheet);
            using (context.PushClip(sheet))
            using (context.PushTransform(Matrix.CreateTranslation(left, top)))
                document.PaintPage(page, _canvases.Create(context, size, scale));
            context.DrawRectangle(new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 0.5), sheet);
        }
    }
}
