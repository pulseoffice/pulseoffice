using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Settings.Tests;

public sealed class Round217SettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-r217-" + Guid.NewGuid().ToString("N"));

    private string File_ => Path.Combine(_directory, "settings.json");

    private JsonSettingsStore Store() =>
        new(SettingScope.User, File_, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public void Two_instances_keep_each_others_changes()
    {
        using var first = Store();
        using var second = Store();

        first.Set("interface.language", "de");
        first.Flush();
        second.Set("appearance.theme", "dark");
        second.Flush();

        using var reread = Store();
        Assert.True(reread.TryGet("interface.language", out var language));
        Assert.Equal("de", language);
        Assert.True(reread.TryGet("appearance.theme", out var theme));
        Assert.Equal("dark", theme);
    }

    [Fact]
    public void A_removal_reaches_the_file_while_other_keys_survive()
    {
        using (var seed = Store())
        {
            seed.Set("a", 1L);
            seed.Set("b", 2L);
        }

        using var store = Store();
        store.Remove("a");
        store.Flush();

        using var reread = Store();
        Assert.False(reread.TryGet("a", out _));
        Assert.True(reread.TryGet("b", out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
