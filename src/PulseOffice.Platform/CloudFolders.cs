using System.Runtime.InteropServices;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Platform;

public interface ISyncedFolderLocator
{
    OperationResult<string> ICloudDrive();
}

public sealed class SyncedFolderLocator : ISyncedFolderLocator
{
    private const string ICloudRelativePath = "Library/Mobile Documents/com~apple~CloudDocs";

    public OperationResult<string> ICloudDrive()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OperationResult<string>.Fail(Diagnostic.Error("CLOUD020", "iCloud Drive is reachable as a folder only on macOS."));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folder = Path.Combine(home, ICloudRelativePath);

        return Directory.Exists(folder)
            ? OperationResult<string>.Ok(folder)
            : OperationResult<string>.Fail(Diagnostic.Error("CLOUD021", "iCloud Drive is not set up on this Mac."));
    }
}
