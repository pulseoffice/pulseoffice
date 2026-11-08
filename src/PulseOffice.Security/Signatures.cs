using System.Security.Cryptography.X509Certificates;

namespace PulseOffice.Security;

public enum SignatureStatus
{
    Valid,
    Broken,
    UntrustedCertificate,
    ExpiredCertificate,
    UnverifiableInTime,
    Unknown,
}

public sealed record SignerIdentity(string Name, string Issuer, string Thumbprint, DateTimeOffset NotAfter)
{
    public static SignerIdentity From(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new SignerIdentity(
            Readable(certificate.Subject),
            Readable(certificate.Issuer),
            certificate.Thumbprint,
            certificate.NotAfter);
    }

    private static string Readable(string distinguishedName)
    {
        foreach (var part in distinguishedName.Split(',', StringSplitOptions.TrimEntries))
            if (part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                return part[3..];

        return distinguishedName;
    }
}

public sealed record DocumentSignature(
    SignerIdentity Signer,
    DateTimeOffset SignedOn,
    SignatureStatus Status,
    IReadOnlyList<string> SignedParts)
{
    public bool IsTrusted => Status == SignatureStatus.Valid;
}

public interface ICertificateSource
{
    IReadOnlyList<X509Certificate2> Personal();

    X509Certificate2? FromFile(string path, string? password);
}

public sealed class CertificateSource : ICertificateSource
{
    public IReadOnlyList<X509Certificate2> Personal()
    {
        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            return [.. store.Certificates
                .Where(certificate => certificate.HasPrivateKey)
                .Where(certificate => certificate.NotAfter > DateTime.Now)
                .OrderBy(certificate => certificate.Subject, StringComparer.CurrentCulture)];
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or PlatformNotSupportedException)
        {
            return [];
        }
    }

    private readonly IReadOnlyList<X509KeyStorageFlags> _storages = [X509KeyStorageFlags.EphemeralKeySet, X509KeyStorageFlags.DefaultKeySet];

    public X509Certificate2? FromFile(string path, string? password)
    {
        foreach (var storage in _storages)
        {
            try
            {
                return X509CertificateLoader.LoadPkcs12FromFile(path, password, storage);
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or IOException)
            {
                return null;
            }
        }

        return null;
    }
}
