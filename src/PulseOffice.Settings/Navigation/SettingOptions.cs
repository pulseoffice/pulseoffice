namespace PulseOffice.Settings.Navigation;

public sealed record SettingOption(string Value, string Label);

public interface ISettingOptionSource
{
    string Name { get; }

    Task<IReadOnlyList<SettingOption>> LoadAsync(CancellationToken ct = default);
}

public interface ISettingOptionSources
{
    ISettingOptionSource? Find(string? name);
}

public sealed class SettingOptionSources(IEnumerable<ISettingOptionSource> sources) : ISettingOptionSources
{
    private readonly IReadOnlyDictionary<string, ISettingOptionSource> _sources =
        (sources ?? throw new ArgumentNullException(nameof(sources))).ToDictionary(source => source.Name, StringComparer.Ordinal);

    public ISettingOptionSource? Find(string? name) =>
        name is not null && _sources.TryGetValue(name, out var source) ? source : null;
}
