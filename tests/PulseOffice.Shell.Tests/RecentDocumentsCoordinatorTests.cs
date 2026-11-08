using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class RecentDocumentsCoordinatorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recentc-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UnFileSparitoSiTogliePrimaDiAprirlo()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "Conti.ods");
        File.WriteAllText(file, "x");

        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, Path.Combine(_directory, "recent.json"));
        recent.Remember(file, "Conti");

        var registry = new CommandRegistry(new FakeTextCatalog());
        var opened = new List<string>();
        using var coordinator = new RecentDocumentsCoordinator(recent, registry, path =>
        {
            opened.Add(path);
            return Task.CompletedTask;
        });

        File.Delete(file);
        var command = registry.Find(new CommandId("shell.file.recent.0"));
        Assert.NotNull(command);
        await command.Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Empty(opened);
        Assert.Empty(recent.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
