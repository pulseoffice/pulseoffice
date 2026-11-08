using System.Diagnostics;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Desktop.Composition;
using PulseOffice.Foundation.Modules;
using PulseOffice.Settings;
using PulseOffice.Shell;
using PulseOffice.Testing;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Fonts;
using Xunit;

namespace PulseOffice.Desktop.Tests;

[Collection(HeadlessDesktopCollection.Name)]
public sealed class ChromeCostTests : IDisposable
{
    private readonly UiThreadSession _session;
    private readonly string _dataHome = Path.Combine(Path.GetTempPath(), "pulseoffice-chrome-" + Guid.NewGuid().ToString("N"));

    public ChromeCostTests(HeadlessDesktopSession headless)
    {
        _session = headless.Session;
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, _dataHome);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UserDataPaths.DataHomeVariable, null);
        if (Directory.Exists(_dataHome)) Directory.Delete(_dataHome, recursive: true);
    }

    private static void Record(string line) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "pulseoffice-chrome-cost.txt"), $"{DateTimeOffset.Now:O} {line}\n");

    [Theory]
    [InlineData(ModuleKind.Documents, SelectionKind.TextCaret)]
    [InlineData(ModuleKind.Sheets, SelectionKind.Cell)]
    [InlineData(ModuleKind.Presentations, SelectionKind.None)]
    public Task Ribbon_projection_for_one_caret_move_stays_within_a_frame(ModuleKind module, SelectionKind selection) => _session.Dispatch(async () =>
    {
        await using var provider = ServiceConfiguration.Register(Application.Current!, NullLoggerFactory.Instance, new FontFamilyMappingTable())
            .BuildServiceProvider(ServiceConfiguration.ProviderOptions);
        var registry = new ShellComposition(provider).Compose();
        var projection = new RibbonProjection(registry);
        var context = new CommandContext(module, selection, DocumentState.DocumentOpen);
        _ = projection.Build(context);

        const int moves = 50;
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < moves; i++) _ = projection.Build(context);
        var perMove = clock.Elapsed / moves;
        clock.Restart();
        for (var i = 0; i < moves; i++) _ = registry.BuildRibbon(context);
        var tree = clock.Elapsed / moves;
        clock.Restart();
        for (var i = 0; i < moves; i++) _ = registry.Present(context, CommandSurface.Ribbon).Count();
        var presence = clock.Elapsed / moves;
        Record($"module={module} tree={tree.TotalMilliseconds:0.00} presence={presence.TotalMilliseconds:0.00}");
        Record($"module={module} commands={registry.All.Count} ribbon build per move={perMove.TotalMilliseconds:0.00}");

        Assert.True(registry.All.Count > 1000, $"{registry.All.Count} commands");
        Assert.True(perMove < TimeSpan.FromMilliseconds(2), $"{perMove.TotalMilliseconds:0.00} ms per caret move");
    }, CancellationToken.None);
}
