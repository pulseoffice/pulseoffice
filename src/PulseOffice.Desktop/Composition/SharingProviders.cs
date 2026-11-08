using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.MacOS;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class MailComposerProvider : IMailComposerProvider
{
    private readonly Lazy<IMailComposer> _composer;

    public MailComposerProvider(IExecutableLocator executables, IExternalProcess processes, IHostSystem host)
    {
        _composer = new(() => Select(executables, processes, host));
    }

    public IMailComposer Current => _composer.Value;

    private static IMailComposer Select(IExecutableLocator executables, IExternalProcess processes, IHostSystem host)
    {
        if (host.IsWindows)
        {
            var mapi = new MapiMailComposer();
            return mapi.IsAvailable ? mapi : new ShellMailComposer();
        }

        IMailComposer composer = host.IsMacOS
            ? new MailAppComposer(executables, processes)
            : new XdgMailComposer(executables, processes);

        return composer.IsAvailable ? composer : new UnavailableMailComposer();
    }
}

public sealed class FileSharingProvider : IFileSharingProvider
{
    private readonly Lazy<IFileSharing> _sharing;

    public FileSharingProvider(ISharingProcessRunner processes, IExecutableLocator executables, IHostSystem host)
    {
        _sharing = new(() => Select(processes, executables, host));
    }

    public IFileSharing Current => _sharing.Value;

    private static IFileSharing Select(ISharingProcessRunner processes, IExecutableLocator executables, IHostSystem host)
    {
        IFileSharing sharing = host.IsMacOS
            ? new BluetoothExchangeSharing(processes)
            : host.IsWindows
                ? new FsquirtSharing(processes)
                : new BluetoothSendToSharing(processes, executables);

        return sharing.IsAvailable ? sharing : new UnavailableFileSharing();
    }
}
