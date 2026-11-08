using System.Globalization;
using System.Text.Json;
using PulseOffice.Settings;

namespace PulseOffice.Updates;

public static class UpdateSettingKeys
{
    public const string Automatic = "updates.automatic";
}

public sealed class UpdateSettings : ISettingsProvider
{
    public IEnumerable<SettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = UpdateSettingKeys.Automatic,
            Kind = SettingKind.Boolean,
            DefaultValue = true,
            Category = "updates",
            Keywords = ["update", "version", "release"],
        },
    ];
}

public sealed record UpdateState(DateTimeOffset? LastCheck, string? SkippedVersion);

public interface IUpdateStateStore
{
    UpdateState Load();

    void Save(UpdateState state);
}

public sealed class JsonUpdateStateStore(IUserDataPaths paths) : IUpdateStateStore
{
    private const string FileName = "update-state.json";

    public UpdateState Load()
    {
        var path = paths.Configuration(FileName);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return new UpdateState(null, null);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            DateTimeOffset? last = root.TryGetProperty("lastCheck", out var check) && check.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(check.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed : null;
            var skipped = root.TryGetProperty("skippedVersion", out var skip) && skip.ValueKind == JsonValueKind.String ? skip.GetString() : null;
            return new UpdateState(last, skipped);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UpdateState(null, null);
        }
    }

    public void Save(UpdateState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var path = paths.Configuration(FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["lastCheck"] = state.LastCheck?.ToString("O", CultureInfo.InvariantCulture),
            ["skippedVersion"] = state.SkippedVersion,
        });
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, path, overwrite: true);
    }
}
