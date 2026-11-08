using System.Runtime.InteropServices;
using PulseOffice.Platform;

namespace PulseOffice.Platform.MacOS;

public sealed partial class MacApplicationVisibility : IApplicationVisibility
{
    private const string ObjectiveC = "/usr/lib/libobjc.A.dylib";

    public bool IsSupported => true;

    public void HideApplication() => Send("hide:");

    public void HideOtherApplications() => Send("hideOtherApplications:");

    public void ShowAllApplications() => Send("unhideAllApplications:");

    private void Send(string selector)
    {
        var application = SendReturningPointer(GetClass("NSApplication"), RegisterName("sharedApplication"));
        if (application == IntPtr.Zero) return;
        SendWithArgument(application, RegisterName(selector), IntPtr.Zero);
    }

    [LibraryImport(ObjectiveC, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr GetClass(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr RegisterName(string name);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial IntPtr SendReturningPointer(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjectiveC, EntryPoint = "objc_msgSend")]
    private static partial void SendWithArgument(IntPtr receiver, IntPtr selector, IntPtr argument);
}
