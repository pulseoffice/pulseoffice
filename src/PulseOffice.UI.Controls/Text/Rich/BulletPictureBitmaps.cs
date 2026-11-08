using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace PulseOffice.UI.Controls.Text.Rich;

public interface IBulletPictureBitmaps
{
    Bitmap? For(ReadOnlyMemory<byte> data);
}

public sealed class BulletPictureBitmaps : IBulletPictureBitmaps
{
    private sealed record Decoded(Bitmap? Bitmap);

    private readonly ConditionalWeakTable<byte[], Decoded> _decoded = new();

    public Bitmap? For(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty) return null;
        if (!MemoryMarshal.TryGetArray(data, out var segment) || segment.Array is not { } array || segment.Offset != 0 || segment.Count != array.Length)
            return Decode(data);
        return _decoded.GetValue(array, key => new Decoded(Decode(key))).Bitmap;
    }

    private Bitmap? Decode(ReadOnlyMemory<byte> data)
    {
        try
        {
            using var stream = new MemoryStream(data.ToArray(), false);
            return new Bitmap(stream);
        }
        catch (Exception failure) when (failure is ArgumentException or InvalidOperationException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}
