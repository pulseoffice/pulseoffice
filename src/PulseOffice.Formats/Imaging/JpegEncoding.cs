namespace PulseOffice.Formats.Imaging;

public interface IJpegEncoder
{
    byte[]? Encode(int width, int height, byte[] rgb, int quality);
}

public sealed class UnavailableJpegEncoder : IJpegEncoder
{
    public byte[]? Encode(int width, int height, byte[] rgb, int quality) => null;
}
