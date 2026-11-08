using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Settings.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-prove-" + Guid.NewGuid().ToString("N"));

    private string Path_ => System.IO.Path.Combine(_directory, "settings.json");

    private JsonSettingsStore Store() =>
        new(SettingScope.User, Path_, TimeProvider.System, NullLogger<JsonSettingsStore>.Instance);

    [Fact]
    public void A_scheduled_write_that_fires_after_disposal_touches_nothing()
    {
        var time = new HeldTimers();
        var store = new JsonSettingsStore(SettingScope.User, Path_, time, NullLogger<JsonSettingsStore>.Instance);
        store.Set("appearance.theme", "dark");
        store.Dispose();
        Directory.Delete(_directory, recursive: true);

        time.FireAll();

        Assert.False(Directory.Exists(_directory));
    }

    private sealed class HeldTimers : TimeProvider
    {
        private readonly List<(TimerCallback Callback, object? State)> _callbacks = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callbacks.Add((callback, state));
            return new InertTimer();
        }

        public void FireAll()
        {
            foreach (var (callback, state) in _callbacks) callback(state);
        }

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void ScrivereDopoLaChiusuraNonFaCadereESalva()
    {
        var store = Store();
        store.Dispose();

        store.Set("appearance.theme", "light");
        store.Remove("documents.autoSaveMinutes");

        using var reopened = Store();
        Assert.True(reopened.TryGet("appearance.theme", out var theme));
        Assert.Equal("light", theme);
    }

    [Fact]
    public void UnFileIllegibileSiConservaPrimaDiEssereSovrascritto()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path_, "{ rotto");

        using (var store = Store())
        {
            store.Set("appearance.theme", "dark");
            store.Flush();
        }

        Assert.Equal("{ rotto", File.ReadAllText(Path_ + ".corrupt"));
    }

    [Fact]
    public void IValoriSopravvivonoAllaChiusura()
    {
        using (var store = Store())
        {
            store.Set("appearance.theme", "dark");
            store.Set("documents.autoSaveMinutes", 15L);
            store.Set("documents.askFormatOnSave", true);
            store.Flush();
        }

        using var reopened = Store();

        Assert.True(reopened.TryGet("appearance.theme", out var theme));
        Assert.Equal("dark", theme);

        Assert.True(reopened.TryGet("documents.autoSaveMinutes", out var minutes));
        Assert.Equal(15L, minutes);

        Assert.True(reopened.TryGet("documents.askFormatOnSave", out var ask));
        Assert.Equal(true, ask);
    }

    [Fact]
    public void ITipiNonSiPerdonoNelPassaggioDaJson()
    {
        using (var store = Store())
        {
            store.Set("numero", 42L);
            store.Set("decimale", 2.5);
            store.Flush();
        }

        using var reopened = Store();

        Assert.True(reopened.TryGet("numero", out var number));
        Assert.IsType<long>(number);

        Assert.True(reopened.TryGet("decimale", out var fraction));
        Assert.IsType<double>(fraction);
    }

    [Fact]
    public void UnFileRovinatoNonImpedisceLAvvio()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path_, "{ questo non è JSON");

        using var store = Store();

        Assert.False(store.TryGet("appearance.theme", out _));
    }

    [Fact]
    public void RimuovereUnValoreLoToglieAncheDalFile()
    {
        using (var store = Store())
        {
            store.Set("a", "1");
            store.Set("b", "2");
            store.Flush();
            store.Remove("a");
            store.Flush();
        }

        using var reopened = Store();

        Assert.False(reopened.TryGet("a", out _));
        Assert.True(reopened.TryGet("b", out _));
    }

    [Fact]
    public void NonRestaNessunFileTemporaneoDopoLaScrittura()
    {
        using var store = Store();
        store.Set("a", "1");
        store.Flush();

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
