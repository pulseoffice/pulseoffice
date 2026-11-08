using System.Text.Json;

namespace PulseOffice.Tools.CorpusAudit;

public interface IDeclarationMatcher
{
    bool Covers(string feature, DeclaredLoss loss);
}

public interface IDeclarationTable
{
    IReadOnlySet<string> FeaturesOf(string code);
}

public sealed class DeclarationTable : IDeclarationTable
{
    public const string AnyFeature = "*";

    private readonly Dictionary<string, IReadOnlySet<string>> _features;

    public DeclarationTable(string path)
    {
        using var stream = File.OpenRead(path);
        var entries = JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream) ?? [];
        _features = entries.ToDictionary(entry => entry.Key, entry => (IReadOnlySet<string>)entry.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    public IReadOnlySet<string> FeaturesOf(string code) => _features.TryGetValue(code, out var features) ? features : new HashSet<string>();
}

public sealed class DeclarationMatcher(IDeclarationTable table) : IDeclarationMatcher
{
    public bool Covers(string feature, DeclaredLoss loss)
    {
        var features = table.FeaturesOf(loss.Code);
        return features.Contains(DeclarationTable.AnyFeature) || features.Contains(feature);
    }
}
