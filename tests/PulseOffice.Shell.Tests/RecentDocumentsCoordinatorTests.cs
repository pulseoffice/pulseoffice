using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Shell;
using PulseOffice.UI.Commands;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class RecentDocumentsCoordinatorTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recentc-" + Guid.NewGuid().ToString("N"));

    private sealed class Prompt(MissingRecentAnswer answer) : IMissingRecentPrompt
    {
        public List<string> Asked { get; } = [];

        public Task<MissingRecentAnswer> AskAsync(RecentDocument document, CancellationToken ct = default)
        {
            Asked.Add(document.Path);
            return Task.FromResult(answer);
        }
    }

    private sealed class Files(bool exists) : IRecentFileProbe
    {
        public bool Exists(string path) => exists;
    }

    private RecentDocuments Recent(params string[] names)
    {
        Directory.CreateDirectory(_directory);
        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, Path.Combine(_directory, "recent.json"));
        foreach (var name in names) recent.Remember(Path.Combine(_directory, name), name);
        return recent;
    }

    [Theory]
    [InlineData(MissingRecentAnswer.Remove, 0)]
    [InlineData(MissingRecentAnswer.Keep, 1)]
    public async Task A_missing_entry_asks_before_removing_and_never_opens(MissingRecentAnswer answer, int kept)
    {
        var recent = Recent("Conti.ods");
        var prompt = new Prompt(answer);
        var opened = new List<string>();
        var registry = new CommandRegistry(new FakeTextCatalog());
        using var coordinator = new RecentDocumentsCoordinator(recent, registry, path =>
        {
            opened.Add(path);
            return Task.CompletedTask;
        }, prompt, new Files(false), () => null);

        await registry.Find(new CommandId("shell.file.recent.0"))!.Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.Empty(opened);
        Assert.Single(prompt.Asked);
        Assert.Equal(kept, recent.Items.Count);
    }

    [Fact]
    public async Task Pin_current_toggles_the_entry_of_the_active_document()
    {
        var recent = Recent("Conti.ods");
        var path = recent.Items[0].Path;
        var registry = new CommandRegistry(new FakeTextCatalog());
        using var coordinator = new RecentDocumentsCoordinator(recent, registry, _ => Task.CompletedTask,
            new Prompt(MissingRecentAnswer.Keep), new Files(true), () => path);

        var pin = registry.Find(RecentDocumentsCoordinator.PinCurrentId)!;
        Assert.True(pin.IsEnabled(CommandContext.Empty));
        await pin.Execute(CommandContext.Empty, TestContext.Current.CancellationToken);

        Assert.True(recent.Items[0].Pinned);
        Assert.True(registry.Find(RecentDocumentsCoordinator.PinCurrentId)!.CheckedWhen!(CommandContext.Empty));
    }

    [Fact]
    public void Pin_current_is_disabled_without_a_saved_document()
    {
        var registry = new CommandRegistry(new FakeTextCatalog());
        using var coordinator = new RecentDocumentsCoordinator(Recent(), registry, _ => Task.CompletedTask,
            new Prompt(MissingRecentAnswer.Keep), new Files(true), () => null);

        Assert.False(registry.Find(RecentDocumentsCoordinator.PinCurrentId)!.IsEnabled(CommandContext.Empty));
    }

    [Fact]
    public void A_missing_unpinned_entry_survives_a_restart()
    {
        Recent("Gone.ods");
        var reloaded = new RecentDocuments(NullLogger<RecentDocuments>.Instance, Path.Combine(_directory, "recent.json"));
        Assert.Single(reloaded.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
