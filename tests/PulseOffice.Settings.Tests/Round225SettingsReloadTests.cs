using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Settings.Tests;

public sealed class Round225SettingsReloadTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-r225-" + Guid.NewGuid().ToString("N"));

    private string File_ => Path.Combine(_directory, "settings.json");

    private JsonSettingsStore Store() =>
        new(SettingScope.User, File_, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public void Values_another_instance_wrote_are_reloaded()
    {
        using var first = Store();
        using var second = Store();
        IReadOnlyList<string>? raised = null;
        first.Reloaded += (_, args) => raised = args.Keys;

        second.Set("interface.language", "de");
        second.Flush();

        Assert.Equal(["interface.language"], first.Reload());
        Assert.Equal(["interface.language"], raised);
        Assert.True(first.TryGet("interface.language", out var language));
        Assert.Equal("de", language);
    }

    [Fact]
    public void Local_unsaved_changes_are_not_clobbered()
    {
        using var first = Store();
        using var second = Store();
        first.Set("appearance.theme", "light");

        second.Set("appearance.theme", "dark");
        second.Set("interface.language", "fr");
        second.Flush();

        Assert.Equal(["interface.language"], first.Reload());
        Assert.True(first.TryGet("appearance.theme", out var theme));
        Assert.Equal("light", theme);
    }

    [Fact]
    public void A_key_removed_elsewhere_is_removed_here()
    {
        using (var seed = Store()) seed.Set("a", 1L);
        using var first = Store();
        using var second = Store();

        second.Remove("a");
        second.Flush();

        Assert.Equal(["a"], first.Reload());
        Assert.False(first.TryGet("a", out _));
    }

    [Fact]
    public void The_service_notifies_through_the_dispatch()
    {
        using var first = Store();
        using var second = Store();
        var dispatch = new RecordingDispatch();
        var service = new SettingsService(
            [new Provider()],
            [first],
            new PulseOffice.Localization.TextCatalog(PulseOffice.Localization.UiLanguage.English),
            NullLogger<SettingsService>.Instance,
            dispatch);
        List<string> seen = [];
        service.Changed += (_, args) => seen.Add(args.Key);

        second.Set("t.key", "x");
        second.Flush();
        first.Reload();

        Assert.Empty(seen);
        dispatch.Run();
        Assert.Equal(["t.key"], seen);
        Assert.Equal("x", service.Get<string>("t.key"));
    }

    private sealed class RecordingDispatch : ISettingsChangeDispatch
    {
        private readonly List<Action> _pending = [];

        public void Post(Action notify) => _pending.Add(notify);

        public void Run()
        {
            foreach (var action in _pending) action();
            _pending.Clear();
        }
    }

    private sealed class Provider : ISettingsProvider
    {
        public IEnumerable<SettingDefinition> GetSettings() =>
            [new SettingDefinition { Key = "t.key", Kind = SettingKind.Text, DefaultValue = "a", Category = "t" }];
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
