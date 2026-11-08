namespace PulseOffice.Drawing.Assets;

public interface IVectorRasterizer
{
    byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight);
}

public sealed class NoVectorRasterizer : IVectorRasterizer
{
    public byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight) => null;
}
