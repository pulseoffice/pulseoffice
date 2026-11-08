using System.Diagnostics;
using System.Runtime.InteropServices;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public interface IShellOpener
{
    OperationResult<bool> OpenUrl(string url);

    OperationResult<bool> OpenFile(string path);
}

public interface IShellLaunch
{
    OperationResult<bool> Launch(string target);
}

public sealed class ShellOpener(IShellLaunch launch) : IShellOpener
{
    public OperationResult<bool> OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var address)
            || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
            return OperationResult<bool>.Fail(Diagnostic.Error("OPEN003", "Only http and https can be opened."));

        return launch.Launch(address.ToString());
    }

    public OperationResult<bool> OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var fullPath = Path.GetFullPath(path);

        return File.Exists(fullPath)
            ? launch.Launch(fullPath)
            : OperationResult<bool>.Fail(Diagnostic.Error("OPEN002", "The file does not exist."));
    }

}

public sealed class SystemShellLaunch : IShellLaunch
{
    public OperationResult<bool> Launch(string target)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var shell = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                return OperationResult<bool>.Ok(true);
            }

            var executable = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";

            var info = new ProcessStartInfo(executable) { UseShellExecute = false };
            info.ArgumentList.Add(target);

            using var process = Process.Start(info);

            return process is null
                ? OperationResult<bool>.Fail(Diagnostic.Error("OPEN001", executable).With(executable))
                : OperationResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("OPEN001", ex.Message).With(ex.Message));
        }
    }
}
