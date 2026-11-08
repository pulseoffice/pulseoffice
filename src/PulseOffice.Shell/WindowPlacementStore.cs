using System.Text.Json;
using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Windowing;
using PulseOffice.Settings;

namespace PulseOffice.Shell;

public interface IWindowPlacementStore
{
    WindowPlacement? Load();

    void Save(WindowPlacement placement);
}

public sealed class WindowPlacementStore : IWindowPlacementStore
{
    private readonly string _path;
    private readonly ILogger<WindowPlacementStore> _logger;

    public WindowPlacementStore(ILogger<WindowPlacementStore> logger, string path)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _path = path;
    }

    public WindowPlacement? Load()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            return JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(_path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unreadable window placement: starting from defaults");
            return null;
        }
    }

    public void Save(WindowPlacement placement)
    {
        if (!double.IsFinite(placement.X) || !double.IsFinite(placement.Y)
            || !double.IsFinite(placement.Width) || !double.IsFinite(placement.Height))
        {
            _logger.LogWarning("Window placement not saved: non-finite bounds");
            return;
        }

        var temporary = _path + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            File.WriteAllText(temporary, JsonSerializer.Serialize(
                placement, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not save window placement");
            try { File.Delete(temporary); }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
        }
    }
}
