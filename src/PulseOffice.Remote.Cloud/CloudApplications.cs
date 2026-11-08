using System.Text.Json;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Remote.Cloud;

public sealed record CloudApplication
{
    public required RemoteProtocol Protocol { get; init; }

    public required string ClientId { get; init; }

    public string Scopes { get; init; } = string.Empty;
}

public interface ICloudApplications
{
    OperationResult<CloudApplication> For(RemoteProtocol protocol);
}

public sealed class CloudApplications : ICloudApplications
{
    private readonly Dictionary<RemoteProtocol, CloudApplication> _applications = [];

    public CloudApplications(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        FilePath = filePath;
        Load();
    }

    public string FilePath { get; }

    public static IReadOnlyDictionary<RemoteProtocol, string> DefaultScopes =>
        new Dictionary<RemoteProtocol, string>
        {
            [RemoteProtocol.OneDrive] = "offline_access Files.ReadWrite",
            [RemoteProtocol.GoogleDrive] = "https://www.googleapis.com/auth/drive.file",
            [RemoteProtocol.Dropbox] = "files.content.read files.content.write files.metadata.read",
        };

    public OperationResult<CloudApplication> For(RemoteProtocol protocol) =>
        _applications.TryGetValue(protocol, out var application)
            ? OperationResult<CloudApplication>.Ok(application)
            : OperationResult<CloudApplication>.Fail(Diagnostic.Error("CLOUD001", $"No application registration for {protocol}. Declare its client id in {FilePath}.", FilePath).With(protocol, FilePath));

    private void Load()
    {
        if (!File.Exists(FilePath)) return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (document.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (!Enum.TryParse<RemoteProtocol>(entry.Name, ignoreCase: true, out var protocol)) continue;
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;

                var clientId = entry.Value.TryGetProperty("clientId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                if (string.IsNullOrWhiteSpace(clientId)) continue;

                var scopes = entry.Value.TryGetProperty("scopes", out var declared) && declared.ValueKind == JsonValueKind.String
                    ? declared.GetString()
                    : null;

                _applications[protocol] = new CloudApplication
                {
                    Protocol = protocol,
                    ClientId = clientId,
                    Scopes = string.IsNullOrWhiteSpace(scopes)
                        ? DefaultScopes.GetValueOrDefault(protocol, string.Empty)
                        : scopes,
                };
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _applications.Clear();
        }
    }
}
