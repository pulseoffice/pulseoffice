using System.Text.Json;
using PulseOffice.Settings;
using PulseOffice.Text.Spelling;

namespace PulseOffice.UI.Controls.Services;

public sealed class AutoCorrectProfileStore : IAutoCorrectProfileStore
{
    private readonly IAutoCorrectProfileCodec _codec;
    private readonly string _path;
    private readonly Lock _gate = new();

    public AutoCorrectProfileStore(IAutoCorrectProfileCodec codec, string path)
    {
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _path = path;
    }

    public AutoCorrectProfile Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return AutoCorrectProfile.Default;

            try
            {
                return _codec.Read(File.ReadAllText(_path));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
            {
                SetAside();
                return AutoCorrectProfile.Default;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return AutoCorrectProfile.Default;
            }
        }
    }

    public void Save(AutoCorrectProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var json = _codec.Write(profile);

        lock (_gate)
        {
            var temporary = _path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(temporary, json);
                File.Move(temporary, _path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(temporary);
            }
        }
    }

    private void SetAside()
    {
        try
        {
            File.Copy(_path, _path + ".corrupt", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public interface IAutoCorrectPreferences
{
    AutoCorrectProfile Current { get; }

    void Apply(AutoCorrectProfile profile);
}

public sealed class AutoCorrectPreferences : IAutoCorrectPreferences
{
    private readonly IAutoCorrectProfileStore _store;
    private readonly IAutoCorrectSettings _settings;
    private readonly IAutoCorrectTable _table;

    public AutoCorrectPreferences(IAutoCorrectProfileStore store, IAutoCorrectSettings settings, IAutoCorrectTable table)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _table = table ?? throw new ArgumentNullException(nameof(table));

        Use(store.Load());
    }

    public AutoCorrectProfile Current { get; private set; } = AutoCorrectProfile.Default;

    public void Apply(AutoCorrectProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Use(profile);
        _store.Save(profile);
    }

    private void Use(AutoCorrectProfile profile)
    {
        Current = profile;
        _settings.Enabled = profile.Enabled;
        _settings.Options = profile.Options;
        _table.Customize(profile.Languages);
    }
}
