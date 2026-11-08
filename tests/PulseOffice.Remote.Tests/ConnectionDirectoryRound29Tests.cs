using PulseOffice.Remote;
using Xunit;

namespace PulseOffice.Remote.Tests;

public sealed class ConnectionDirectoryRound29Tests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-remote-").FullName;

    [Fact]
    public void ACorruptConnectionsFileKeepsOnlyWellFormedEntries()
    {
        var path = Path.Combine(_folder, "connections.json");
        File.WriteAllText(path, """
            [null, {"Name": null}, {"Name": "  "}, {"Name": "odd", "Protocol": 99},
             {"Name": "nulls", "Url": null, "User": null, "BasePath": null},
             {"Name": "ok", "Url": "https://dav.example.test/"}]
            """);

        var directory = new FileRemoteConnectionDirectory(path);

        Assert.Equal(["nulls", "ok"], directory.All.Select(connection => connection.Name));
        Assert.Null(directory.Find("missing"));
        Assert.Equal(string.Empty, directory.Find("nulls")!.Url);
        Assert.Equal(string.Empty, directory.Find("nulls")!.User);
        Assert.Equal("/", directory.Find("nulls")!.BasePath);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
