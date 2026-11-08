using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PulseOffice.Platform;

public interface IPrivateFiles
{
    void CreateFolder(string folder);

    FileStream Open(string path, FileMode mode, FileShare share = FileShare.None);

    void Restrict(string path);
}

public interface IPrivateFilesFactory
{
    IPrivateFiles Create();
}

public sealed class PrivateFilesFactory : IPrivateFilesFactory
{
    public IPrivateFiles Create() => OperatingSystem.IsWindows() ? new WindowsPrivateFiles() : new UnixPrivateFiles();
}

[UnsupportedOSPlatform("windows")]
public sealed class UnixPrivateFiles : IPrivateFiles
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerFolder = OwnerFile | UnixFileMode.UserExecute;

    public void CreateFolder(string folder)
    {
        var existed = Directory.Exists(folder);
        Directory.CreateDirectory(folder, OwnerFolder);
        if (!existed) File.SetUnixFileMode(folder, OwnerFolder);
    }

    public FileStream Open(string path, FileMode mode, FileShare share = FileShare.None)
    {
        if (File.Exists(path)) Restrict(path);
        var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = mode,
            Access = FileAccess.Write,
            Share = share,
            UnixCreateMode = OwnerFile,
        });
        Restrict(path);
        return stream;
    }

    public void Restrict(string path) => File.SetUnixFileMode(path, OwnerFile);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsPrivateFiles : IPrivateFiles
{
    public void CreateFolder(string folder)
    {
        var existed = Directory.Exists(folder);
        var created = Directory.CreateDirectory(folder);
        if (existed) return;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(Owner(), FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        created.SetAccessControl(security);
    }

    public FileStream Open(string path, FileMode mode, FileShare share = FileShare.None)
    {
        var stream = new FileStream(path, new FileStreamOptions { Mode = mode, Access = FileAccess.Write, Share = share });
        Restrict(path);
        return stream;
    }

    public void Restrict(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(Owner(), FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private SecurityIdentifier Owner()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
    }
}
