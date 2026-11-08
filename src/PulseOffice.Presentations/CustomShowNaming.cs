namespace PulseOffice.Presentations;

public interface ICustomShowNaming
{
    IReadOnlyList<CustomShow> Distinct(IReadOnlyList<CustomShow> shows, string fallback);
}

public sealed class CustomShowNaming : ICustomShowNaming
{
    public IReadOnlyList<CustomShow> Distinct(IReadOnlyList<CustomShow> shows, string fallback)
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CustomShow>(shows.Count);
        foreach (var show in shows)
        {
            var baseName = string.IsNullOrWhiteSpace(show.Name) ? fallback : show.Name.Trim();
            var name = baseName;
            var counter = 1;
            while (!taken.Add(name)) name = $"{baseName} {++counter}";
            result.Add(show with { Name = name });
        }
        return result;
    }
}
