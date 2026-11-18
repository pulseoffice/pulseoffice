namespace PulseOffice.Platform;

public interface IPrivateSpool
{
    Stream Create();
}

public sealed class PrivateFileSpool(ITemporaryFiles temporary, IPrivateFilesFactory files) : IPrivateSpool
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly IPrivateFiles _files = files.Create();
    private readonly Lock _gate = new();
    private string? _folder;

    public Stream Create()
    {
        var path = Path.Combine(Folder(), $"{Guid.NewGuid():N}.spool");
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose,
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = OwnerFile;

        var stream = new FileStream(path, options);
        try
        {
            _files.Restrict(path);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private string Folder()
    {
        lock (_gate)
        {
            if (_folder is null)
            {
                _folder = temporary.NewFolder("spool-");
                _files.CreateFolder(_folder);
            }

            return _folder;
        }
    }
}
