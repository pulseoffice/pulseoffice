using System.Text.Json;

namespace PulseOffice.Remote;

public sealed class FileRemoteConnectionDirectory : IRemoteConnectionDirectory
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    private readonly string _path;
    private readonly List<RemoteConnection> _connections;

    public FileRemoteConnectionDirectory(string path)
    {
        _path = path;
        _connections = Read(path);
    }

    public IReadOnlyList<RemoteConnection> All => _connections;

    public RemoteConnection? Find(string name) =>
        _connections.FirstOrDefault(connection =>
            string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Save(RemoteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var index = _connections.FindIndex(known =>
            string.Equals(known.Name, connection.Name, StringComparison.OrdinalIgnoreCase));

        if (index < 0) _connections.Add(connection);
        else _connections[index] = connection;

        Write();
    }

    public bool Remove(string name)
    {
        var removed = _connections.RemoveAll(connection =>
            string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;

        if (removed) Write();

        return removed;
    }

    private static List<RemoteConnection> Read(string path)
    {
        if (!File.Exists(path)) return [];

        try
        {
            var stored = JsonSerializer.Deserialize<List<RemoteConnection?>>(File.ReadAllText(path)) ?? [];

            return [.. stored
                .Where(connection => connection is not null
                    && !string.IsNullOrWhiteSpace(connection.Name)
                    && Enum.IsDefined(connection.Protocol))
                .Select(connection => connection! with
                {
                    Url = connection.Url ?? string.Empty,
                    User = connection.User ?? string.Empty,
                    BasePath = connection.BasePath ?? "/",
                })];
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var temporary = _path + ".tmp";

            File.WriteAllText(temporary, JsonSerializer.Serialize(_connections, Format));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
