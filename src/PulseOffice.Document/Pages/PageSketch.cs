namespace PulseOffice.Document.Pages;

public enum PageSketchOrientation
{
    Portrait,
    Landscape,
}

public enum PageSketchBody
{
    TextLines,
    Cells,
    Blank,
}

public sealed record PageSketchMargins(double Left, double Top, double Right, double Bottom)
{
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;
}

public sealed record PageSketchBand(double Height, double Spacing = 0, string? Left = null, string? Center = null, string? Right = null)
{
    public bool HasText => !string.IsNullOrEmpty(Left) || !string.IsNullOrEmpty(Center) || !string.IsNullOrEmpty(Right);
}

public sealed record PageSketch(double Width, double Height)
{
    public PageSketchOrientation Orientation { get; init; } = PageSketchOrientation.Portrait;

    public PageSketchMargins Margins { get; init; } = new(0, 0, 0, 0);

    public PageSketchBand? Header { get; init; }

    public PageSketchBand? Footer { get; init; }

    public int Columns { get; init; } = 1;

    public double ColumnGap { get; init; }

    public PageSketchBody Body { get; init; } = PageSketchBody.TextLines;

    public uint Paper { get; init; } = 0xFFFFFFFF;

    public string? Watermark { get; init; }
}

public readonly record struct SketchRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed record PageSketchGeometry(
    double Scale,
    SketchRect Page,
    SketchRect Content,
    SketchRect? Header,
    SketchRect? Footer,
    SketchRect Body,
    IReadOnlyList<SketchRect> Columns);
