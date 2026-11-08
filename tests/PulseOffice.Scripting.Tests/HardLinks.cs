using Xunit;

namespace PulseOffice.Scripting.Tests;

internal sealed partial class HardLinks
{
    public void Create(string link, string existing)
    {
        var created = OperatingSystem.IsWindows() ? CreateHardLinkW(link, existing, IntPtr.Zero) : UnixLink(existing, link) == 0;
        Assert.True(created, "The hard link could not be created.");
    }

    [System.Runtime.InteropServices.LibraryImport("libc", EntryPoint = "link", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf8)]
    private static partial int UnixLink(string existing, string link);

    [System.Runtime.InteropServices.LibraryImport("kernel32", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool CreateHardLinkW(string link, string existing, IntPtr security);
}
