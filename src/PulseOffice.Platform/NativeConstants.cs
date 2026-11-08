using System.Runtime.InteropServices;

namespace PulseOffice.Platform.Mac;

public sealed class NativeConstants
{
    private const int RtldNow = 2;

    public static readonly NativeConstants CoreFoundation =
        new("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation");

    public static readonly NativeConstants VideoToolbox =
        new("/System/Library/Frameworks/VideoToolbox.framework/VideoToolbox");

    private readonly IntPtr _handle;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IntPtr> _cache = new(StringComparer.Ordinal);

    private NativeConstants(string path)
    {
        try { _handle = dlopen(path, RtldNow); }
        catch (DllNotFoundException) { _handle = IntPtr.Zero; }
    }

    public IntPtr? Constant(string symbol)
    {
        if (_handle == IntPtr.Zero) return null;
        var value = _cache.GetOrAdd(symbol, Resolve);

        return value == IntPtr.Zero ? null : value;
    }

    private IntPtr Resolve(string symbol)
    {
        var address = dlsym(_handle, symbol);
        return address == IntPtr.Zero ? IntPtr.Zero : Marshal.ReadIntPtr(address);
    }

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);
}
