using System.Runtime.InteropServices;
using PulseOffice.Platform;

namespace PulseOffice.Platform.MacOS;

public sealed partial class MacActiveDisplayProbe : IActiveDisplayProbe
{
    private const string CoreVideo = "/System/Library/Frameworks/CoreVideo.framework/CoreVideo";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public bool HasActiveDisplay()
    {
        var status = CreateWithActiveDisplays(out var displayLink);
        if (displayLink != IntPtr.Zero) Release(displayLink);
        return status == 0 && displayLink != IntPtr.Zero;
    }

    [LibraryImport(CoreVideo, EntryPoint = "CVDisplayLinkCreateWithActiveCGDisplays")]
    private static partial int CreateWithActiveDisplays(out IntPtr displayLink);

    [LibraryImport(CoreFoundation, EntryPoint = "CFRelease")]
    private static partial void Release(IntPtr value);
}
