using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class RecentDocumentsTests : IDisposable
{
    private readonly List<string> _files = [];

    private string TemporaryFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-" + Guid.NewGuid().ToString("N") + ".ods");
        File.WriteAllText(path, "prova");
        _files.Add(path);
        return path;
    }

    private RecentDocuments Build(int capacity = 12)
    {
        var store = Path.Combine(Path.GetTempPath(), "pulseoffice-recenti-" + Guid.NewGuid().ToString("N") + ".json");
        _files.Add(store);

        return new RecentDocuments(NullLogger<RecentDocuments>.Instance, store, capacity);
    }

    [Fact]
    public void ConConfrontoSensibileAlleMaiuscoleDuePercorsiDiversiRestanoDistinti()
    {
        var store = Path.Combine(Path.GetTempPath(), "pulseoffice-recenti-" + Guid.NewGuid().ToString("N") + ".json");
        _files.Add(store);
        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, store, 12, StringComparison.Ordinal);

        recent.Remember("/tmp/Documento.ods", "Documento.ods");
        recent.Remember("/tmp/documento.ods", "documento.ods");

        Assert.Equal(2, recent.Items.Count);
    }

    [Fact]
    public void IlPiuRecenteStaInCima()
    {
        var recent = Build();
        var first = TemporaryFile();
        var second = TemporaryFile();

        recent.Remember(first, "primo.ods");
        recent.Remember(second, "secondo.ods");

        Assert.Equal("secondo.ods", recent.Items[0].Name);
        Assert.Equal("primo.ods", recent.Items[1].Name);
    }

    [Fact]
    public void RiaprireUnDocumentoLoFaRisalireInvecediDuplicarlo()
    {
        var recent = Build();
        var first = TemporaryFile();
        var second = TemporaryFile();

        recent.Remember(first, "primo.ods");
        recent.Remember(second, "secondo.ods");
        recent.Remember(first, "primo.ods");

        Assert.Equal(2, recent.Items.Count);
        Assert.Equal("primo.ods", recent.Items[0].Name);
    }

    [Fact]
    public void LElencoNonSuperaLaCapienza()
    {
        var recent = Build(capacity: 3);

        for (var i = 0; i < 6; i++) recent.Remember(TemporaryFile(), $"documento{i}.ods");

        Assert.Equal(3, recent.Items.Count);
        Assert.Equal("documento5.ods", recent.Items[0].Name);
    }

    [Fact]
    public void DimenticareToglieLaVoce()
    {
        var recent = Build();
        var path = TemporaryFile();

        recent.Remember(path, "documento.ods");
        recent.Forget(path);

        Assert.Empty(recent.Items);
    }

    [Fact]
    public void OgniModificaSegnalaIlCambiamento()
    {
        var recent = Build();
        var notified = 0;
        recent.Changed += (_, _) => notified++;

        recent.Remember(TemporaryFile(), "a.ods");
        recent.Clear();

        Assert.Equal(2, notified);
    }

    public void Dispose()
    {
        foreach (var file in _files.Where(File.Exists)) File.Delete(file);
        foreach (var link in _links.Where(Directory.Exists)) Directory.Delete(link);
    }

    private readonly List<string> _links = [];

    [Fact]
    public void LoStessoFileRaggiuntoAttraversoUnCollegamentoSimbolicoCompareUnaVolta()
    {
        var recent = Build();
        var file = TemporaryFile();
        var link = Path.Combine(Path.GetTempPath(), "pulseoffice-collegamento-" + Guid.NewGuid().ToString("N"));
        Directory.CreateSymbolicLink(link, Path.GetDirectoryName(file)!);
        _links.Add(link);

        recent.Remember(file, "a.ods");
        recent.Remember(Path.Combine(link, Path.GetFileName(file)), "a.ods");

        Assert.Single(recent.Items);
    }

    [Fact]
    public void UnElencoSalvatoConLoStessoFileDueVolteSiRileggeConUnaVoceSola()
    {
        var file = TemporaryFile();
        var link = Path.Combine(Path.GetTempPath(), "pulseoffice-collegamento-" + Guid.NewGuid().ToString("N"));
        Directory.CreateSymbolicLink(link, Path.GetDirectoryName(file)!);
        _links.Add(link);
        var store = Path.Combine(Path.GetTempPath(), "pulseoffice-recenti-" + Guid.NewGuid().ToString("N") + ".json");
        _files.Add(store);
        File.WriteAllText(store, System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new RecentDocument(file, "a.ods", DateTimeOffset.Now),
            new RecentDocument(Path.Combine(link, Path.GetFileName(file)), "a.ods", DateTimeOffset.Now),
        }));

        Assert.Single(new RecentDocuments(NullLogger<RecentDocuments>.Instance, store).Items);
    }
}
