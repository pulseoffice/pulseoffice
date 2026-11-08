using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class RichClipboardFormatsProvider(IHostSystem host) : IRichClipboardFormatsProvider
{
    private readonly Lazy<IRichClipboardFormats> _formats = new(() => Select(host));

    public IRichClipboardFormats Current => _formats.Value;

    private static IRichClipboardFormats Select(IHostSystem host)
    {
        if (host.IsMacOS) return new MacClipboardFormats();
        if (host.IsWindows) return new WindowsClipboardFormats();

        return new LinuxClipboardFormats();
    }
}
