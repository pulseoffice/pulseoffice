namespace PulseOffice.Platform;

public interface IAppImageLocation
{
    string? Current { get; }

    void MovedTo(string path);
}

public sealed class EnvironmentAppImageLocation(IEnvironmentVariables environment) : IAppImageLocation
{
    private string? _moved;

    public string? Current
    {
        get
        {
            var path = _moved ?? environment.Get("APPIMAGE");
            return path is { Length: > 0 } && File.Exists(path) ? Path.GetFullPath(path) : null;
        }
    }

    public void MovedTo(string path) => _moved = path;
}

public interface IDesktopIntegration
{
    bool IsAvailable { get; }

    bool IsIntegrated { get; }

    string? CurrentImage { get; }

    string? RegisteredImage { get; }

    string ApplicationsFolder { get; }

    bool IsInApplicationsFolder { get; }

    void Integrate();

    void Remove();

    void RegisterCurrentLocation();

    string MoveToApplicationsFolder();
}

public sealed class UnavailableDesktopIntegration : IDesktopIntegration
{
    public bool IsAvailable => false;

    public bool IsIntegrated => false;

    public string? CurrentImage => null;

    public string? RegisteredImage => null;

    public string ApplicationsFolder => string.Empty;

    public bool IsInApplicationsFolder => false;

    public void Integrate() => throw new NotSupportedException("Desktop integration needs an AppImage on Linux.");

    public void Remove() => throw new NotSupportedException("Desktop integration needs an AppImage on Linux.");

    public void RegisterCurrentLocation() => throw new NotSupportedException("Desktop integration needs an AppImage on Linux.");

    public string MoveToApplicationsFolder() => throw new NotSupportedException("Desktop integration needs an AppImage on Linux.");
}
