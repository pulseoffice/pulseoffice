using Xunit;

namespace PulseOffice.Remote.Tests;

public sealed class RemoteDocumentsLookupTests
{
    [Fact]
    public void Lo_stesso_file_remoto_gia_scaricato_si_ritrova_dalla_connessione_e_dal_percorso()
    {
        var documents = new RemoteDocuments();
        var connection = new RemoteConnection { Name = "Ufficio", Url = "https://dav.example.org/", User = "anna" };
        documents.Follow(new RemoteDocument { LocalPath = "/tmp/a/conti.ods", Connection = connection, RemotePath = "/team/conti.ods" });

        Assert.Equal("/tmp/a/conti.ods", documents.Following(connection with { Name = "Altro nome" }, "/team/conti.ods")?.LocalPath);
        Assert.Null(documents.Following(connection, "/team/altro.ods"));
        Assert.Null(documents.Following(connection with { User = "bruno" }, "/team/conti.ods"));
    }
}
