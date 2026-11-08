using System.Globalization;

namespace PulseOffice.Data;

public interface IConnectionIds
{
    string NewId(string displayName, IEnumerable<string> taken);
}

public sealed class ConnectionIds : IConnectionIds
{
    public string NewId(string displayName, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(taken);

        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var slug = new string([.. displayName.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-')]).Trim('-');
        if (slug.Length == 0) slug = "connection";

        if (!used.Contains(slug)) return slug;
        for (var index = 2; ; index++)
        {
            var candidate = slug + "-" + index.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
