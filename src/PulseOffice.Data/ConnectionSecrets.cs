using System.Data.Common;

namespace PulseOffice.Data;

public sealed record SplitConnection(string ConnectionString, string? Secret);

public interface IConnectionSecretSplitter
{
    SplitConnection Split(DatabaseProviderKind provider, string connectionString);
}

public sealed class ConnectionSecretSplitter : IConnectionSecretSplitter
{
    private static readonly string[] SecretKeys = ["Password", "Pwd"];

    public SplitConnection Split(DatabaseProviderKind provider, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var builder = new DbConnectionStringBuilder(useOdbcRules: provider == DatabaseProviderKind.Odbc);

        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            return new SplitConnection(connectionString, null);
        }

        string? secret = null;

        foreach (var key in SecretKeys)
        {
            if (!builder.TryGetValue(key, out var value)) continue;

            secret ??= Unbraced(provider, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
            builder.Remove(key);
        }

        return secret is null
            ? new SplitConnection(connectionString, null)
            : new SplitConnection(builder.ConnectionString, secret.Length > 0 ? secret : null);
    }

    private static string? Unbraced(DatabaseProviderKind provider, string? value) =>
        provider == DatabaseProviderKind.Odbc && value is { Length: >= 2 } && value[0] == '{' && value[^1] == '}'
            ? value[1..^1].Replace("}}", "}", StringComparison.Ordinal)
            : value;
}
