using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class CloudReadersTests
{
    [Fact]
    public void OneDriveDistingueCartelleEFile()
    {
        var json =
            """
            {"value":[
              {"name":"Conti","folder":{"childCount":2},"lastModifiedDateTime":"2026-01-02T10:00:00Z"},
              {"name":"bilancio.ods","file":{},"size":2048,"lastModifiedDateTime":"2026-01-03T09:30:00Z"}
            ]}
            """;

        var entries = new OneDriveReader().ReadListing(json, "/Documenti");

        Assert.Equal(2, entries.Count);
        Assert.True(entries[0].IsFolder);
        Assert.Equal("/Documenti/Conti", entries[0].Path);
        Assert.False(entries[1].IsFolder);
        Assert.Equal(2048, entries[1].Size);
    }

    [Fact]
    public void OneDriveIndirizzaLaRadiceSenzaDuePunti()
    {
        Assert.EndsWith("/root/children", OneDriveStorage.Children("/"), StringComparison.Ordinal);
        Assert.Contains("root:/Conti:/children", OneDriveStorage.Children("/Conti"), StringComparison.Ordinal);
    }

    [Fact]
    public void OneDriveCodificaGliSpaziNelPercorso()
    {
        Assert.Contains("bilancio%202026.ods", OneDriveStorage.Content("/Conti/bilancio 2026.ods"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DropboxLeggeIlPercorsoCompleto()
    {
        var json =
            """
            {"entries":[
              {".tag":"folder","name":"Conti","path_display":"/Conti"},
              {".tag":"file","name":"bilancio.ods","path_display":"/Conti/bilancio.ods","size":10,
               "server_modified":"2026-02-01T08:00:00Z"}
            ]}
            """;

        var entries = new DropboxReader().ReadListing(json);

        Assert.True(entries[0].IsFolder);
        Assert.Equal("/Conti/bilancio.ods", entries[1].Path);
        Assert.Equal(10, entries[1].Size);
    }

    [Fact]
    public void DropboxChiedeLaRadiceConLaStringaVuota()
    {
        Assert.Equal(string.Empty, DropboxStorage.Folder("/"));
        Assert.Equal("/Conti", DropboxStorage.Folder("/Conti/"));
        Assert.Contains("\"mode\": \"overwrite\"", DropboxStorage.UploadArgument("/a.ods", null),
            StringComparison.Ordinal);
    }

    [Fact]
    public void GoogleLeggeLaDimensioneCheArrivaComeTesto()
    {
        var json =
            """
            {"files":[
              {"id":"1","name":"Conti","mimeType":"application/vnd.google-apps.folder"},
              {"id":"2","name":"bilancio.ods","mimeType":"application/octet-stream","size":"4096",
               "modifiedTime":"2026-03-04T12:00:00Z"}
            ]}
            """;

        var items = new GoogleDriveReader().ReadListing(json);

        Assert.True(items[0].IsFolder);
        Assert.Equal(4096, items[1].Size);
    }

    [Fact]
    public void GoogleChiedeSoloIFigliNonCestinati()
    {
        var query = GoogleDriveStorage.Query("abc");

        Assert.Contains(Uri.EscapeDataString("'abc' in parents and trashed = false"), query,
            StringComparison.Ordinal);
    }

    [Fact]
    public void UnJsonRottoNonFaCadereLElenco()
    {
        Assert.Empty(new OneDriveReader().ReadListing("{", "/"));
        Assert.Empty(new DropboxReader().ReadListing("non json"));
        Assert.Empty(new GoogleDriveReader().ReadListing("[]"));
    }

    [Fact]
    public void ISegmentiDelPercorsoSiComponengonoSenzaDoppieBarre()
    {
        Assert.Equal("/Conti/bilancio.ods", CloudPaths.Join("/Conti/", "bilancio.ods"));
        Assert.Equal("/bilancio.ods", CloudPaths.Join("/", "bilancio.ods"));
        Assert.Equal("/Conti", CloudPaths.ParentOf("/Conti/bilancio.ods"));
        Assert.Equal("bilancio.ods", CloudPaths.NameOf("/Conti/bilancio.ods"));
    }
}
