namespace PulseOffice.Tools.CorpusAudit;

public sealed record AuditRow(string File, string Target, string Feature, int Before, int After, string Class, string Codes)
{
    public string ToLine() => string.Join('\t', File, Target, Feature, Before, After, Class, Codes);

    public static AuditRow? Parse(string line)
    {
        var cells = line.Split('\t');
        return cells.Length == 7 && int.TryParse(cells[3], out var before) && int.TryParse(cells[4], out var after)
            ? new AuditRow(cells[0], cells[1], cells[2], before, after, cells[5], cells[6])
            : null;
    }
}

public interface IFileAuditor
{
    IReadOnlyList<AuditRow> Audit(string root, string relative);
}

public interface ICopyKeeper
{
    void Keep(string relative, string target, byte[] package);
}

public sealed class CopyKeeper(string? folder) : ICopyKeeper
{
    public void Keep(string relative, string target, byte[] package)
    {
        if (folder is null) return;
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, Path.GetFileNameWithoutExtension(relative) + target), package);
    }
}

public sealed class FileAuditor(IEnumerable<IAuditFamily> families, IFeatureCounter counter, IFeatureCatalog catalog, ICopyKeeper keeper, IDeclarationMatcher matcher) : IFileAuditor
{
    private const string None = "-";

    public IReadOnlyList<AuditRow> Audit(string root, string relative)
    {
        var extension = Path.GetExtension(relative).ToLowerInvariant();
        var family = families.First(candidate => candidate.Extensions.Contains(extension));
        var package = File.ReadAllBytes(Path.Combine(root, relative));
        var before = counter.Count(package, extension);
        if (before is null) return [new AuditRow(relative, None, None, 0, 0, "NOTZIP", None)];
        var source = family.RoundTrip(extension, package);
        if (source.Status != "OK") return [new AuditRow(relative, None, None, 0, 0, source.Status, Join([.. source.DeclaredCodes.Select(loss => loss.Code).Distinct()]))];
        var rows = new List<AuditRow>();
        foreach (var copy in source.Copies)
        {
            var losses = source.DeclaredCodes.Concat(copy.DeclaredCodes).DistinctBy(loss => loss.Code).OrderBy(loss => loss.Code, StringComparer.Ordinal).ToList();
            var declared = losses.Select(loss => loss.Code).ToList();
            if (copy.Package is null)
            {
                rows.Add(new AuditRow(relative, copy.Target, None, 0, 0, copy.Status, Join(declared)));
                continue;
            }

            keeper.Keep(relative, copy.Target, copy.Package);
            var after = counter.Count(copy.Package, copy.Target) ?? new Dictionary<string, int>();
            rows.Add(new AuditRow(relative, copy.Target, None, 0, 0, copy.Status, Join(declared)));
            foreach (var definition in catalog.Definitions.Where(definition => Comparable(definition.Feature, extension, copy.Target)))
            {
                var had = before.GetValueOrDefault(definition.Feature);
                var has = after.GetValueOrDefault(definition.Feature);
                var lost = definition.Comparison == Comparison.Presence ? had > 0 && has == 0 : has < had;
                if (!lost) continue;
                var relevant = losses.Where(loss => matcher.Covers(definition.Feature, loss)).Select(loss => loss.Code).ToList();
                rows.Add(new AuditRow(relative, copy.Target, definition.Feature, had, has, relevant.Count > 0 ? "DECL" : "LOSS", Join(relevant.Count > 0 ? relevant : declared)));
            }
        }

        return rows;
    }

    private bool Comparable(string feature, string source, string target) =>
        catalog.RulesFor(source).Any(rule => rule.Feature == feature) && catalog.RulesFor(target).Any(rule => rule.Feature == feature);

    private static string Join(IReadOnlyList<string> codes) => codes.Count == 0 ? None : string.Join(',', codes);
}
