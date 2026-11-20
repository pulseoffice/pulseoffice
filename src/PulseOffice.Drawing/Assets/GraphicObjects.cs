using PulseOffice.Drawing;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Assets;

public sealed record VectorGraphicObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitVector(this);

    public required VectorAsset Asset { get; init; }

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public string? RecolorHex { get; init; }

    public double RotationDegrees { get; init; }
    public bool FlipHorizontal { get; init; }
    public bool FlipVertical { get; init; }

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
    [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}

public enum ImageFilter { None, Grayscale, Sepia, Invert, Mosaic, Charcoal, PopArt, Posterize, Relief, RemoveNoise, Sharpen, Smooth, Solarize }

public sealed record ImageDuotone(string DarkHex, string LightHex);

public sealed record ImageTone(double Brightness = 0, double Contrast = 0, double Transparency = 0)
{
    public static ImageTone Neutral { get; } = new();

    public bool ChangesColours => Math.Abs(Brightness) > 1e-6 || Math.Abs(Contrast) > 1e-6;

    public bool IsNeutral => !ChangesColours && Math.Abs(Transparency) <= 1e-6;

    public double Opacity => 1 - Math.Clamp(double.IsFinite(Transparency) ? Transparency : 0, 0, 100) / 100;
}

public sealed record RasterImageObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitRaster(this);

    public required ReadOnlyMemory<byte> Data { get; init; }
    public required string MediaType { get; init; }

    public int PixelWidth { get; init; }
    public int PixelHeight { get; init; }

    public (double Left, double Top, double Right, double Bottom) Crop { get; init; }

    public ImageFilter Filter { get; init; } = ImageFilter.None;

    public ImageDuotone? Duotone { get; init; }

    public ImageTone Tone { get; init; } = ImageTone.Neutral;

    public Document.Embedding.ImageMap Map { get; init; } = new();

    public string? LinkPath { get; init; }

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Image;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}
