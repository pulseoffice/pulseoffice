namespace PulseOffice.Foundation.Network;

public interface INetworkSharePaths
{
    bool IsNetworkShare(string location);
}

public sealed class NetworkSharePaths : INetworkSharePaths
{
    public bool IsNetworkShare(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return false;
        var trimmed = location.Trim();
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith(@"/\", StringComparison.Ordinal) || trimmed.StartsWith(@"\/", StringComparison.Ordinal))
            return true;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || !uri.IsFile) return false;
        return uri.IsUnc || (uri.Host.Length > 0 && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }
}
