using System.Globalization;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.OpenXml;

internal sealed record OoxmlProtectionKey(string? Algorithm, uint? SpinCount, string? Salt, string? Hash, string? LegacyPassword);

internal sealed class OoxmlProtectionKeys
{
    public string? Encode(OoxmlProtectionKey key)
    {
        if (key.Hash is { Length: > 0 } hash)
            return string.Create(CultureInfo.InvariantCulture, $"{PasswordKey.OoxmlPrefix}{key.Algorithm}:{key.SpinCount ?? 0}:{key.Salt}:{hash}");
        return key.LegacyPassword is { Length: > 0 } legacy ? PasswordKey.OoxmlLegacyPrefix + legacy : null;
    }

    public OoxmlProtectionKey? Decode(string? token)
    {
        if (token is null) return null;
        if (token.StartsWith(PasswordKey.OoxmlLegacyPrefix, StringComparison.Ordinal))
            return new OoxmlProtectionKey(null, null, null, null, token[PasswordKey.OoxmlLegacyPrefix.Length..]);
        if (!token.StartsWith(PasswordKey.OoxmlPrefix, StringComparison.Ordinal)) return null;

        var parts = token[PasswordKey.OoxmlPrefix.Length..].Split(':');
        if (parts.Length != 4 || !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var spin)) return null;
        return new OoxmlProtectionKey(parts[0], spin, parts[2], parts[3], null);
    }

    public bool IsWritable(string? token) => token is null || Decode(token) is not null;
}
