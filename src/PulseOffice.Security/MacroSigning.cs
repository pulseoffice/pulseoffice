using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace PulseOffice.Security;

public sealed record MacroSignature(SignerIdentity Signer, DateTimeOffset SignedOn, string Value)
{
    public string? Certificate { get; init; }
}

public interface IMacroSigner
{
    string Fingerprint(IEnumerable<(string Name, string Language, string Source)> macros);

    MacroSignature Sign(IEnumerable<(string Name, string Language, string Source)> macros, X509Certificate2 certificate);

    SignatureStatus Verify(IEnumerable<(string Name, string Language, string Source)> macros, MacroSignature? signature);
}

public sealed class MacroSigner(CertificateJudge judge) : IMacroSigner
{
    public MacroSigner() : this(new CertificateJudge())
    {
    }

    public string Fingerprint(IEnumerable<(string Name, string Language, string Source)> macros)
    {
        ArgumentNullException.ThrowIfNull(macros);

        var builder = new StringBuilder();

        foreach (var (name, language, source) in macros.OrderBy(macro => macro.Name, StringComparer.Ordinal).ThenBy(macro => macro.Language, StringComparer.Ordinal).ThenBy(macro => macro.Source, StringComparer.Ordinal))
            foreach (var part in new[] { name, language, source })
                builder.Append(part.Length).Append(':').Append(part);

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public MacroSignature Sign(
        IEnumerable<(string Name, string Language, string Source)> macros, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (certificate.GetRSAPrivateKey() is not { } key)
            throw new InvalidOperationException("The certificate has no usable private key.");

        var fingerprint = Fingerprint(macros);

        var value = key.SignData(
            Encoding.UTF8.GetBytes(fingerprint), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return new MacroSignature(
            SignerIdentity.From(certificate), DateTimeOffset.Now, Convert.ToBase64String(value))
        {
            Certificate = Convert.ToBase64String(certificate.RawData),
        };
    }

    public SignatureStatus Verify(
        IEnumerable<(string Name, string Language, string Source)> macros, MacroSignature? signature)
    {
        if (signature is null) return SignatureStatus.Unknown;
        if (signature.Certificate is not { Length: > 0 } raw) return SignatureStatus.Unknown;

        X509Certificate2 certificate;

        try
        {
            certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(raw));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return SignatureStatus.Unknown;
        }

        if (certificate.GetRSAPublicKey() is not { } key) return SignatureStatus.Unknown;

        bool signed;

        try
        {
            signed = key.VerifyData(
                Encoding.UTF8.GetBytes(Fingerprint(macros)),
                Convert.FromBase64String(signature.Value),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return SignatureStatus.Unknown;
        }

        return signed ? judge.Judge(certificate) : SignatureStatus.Broken;
    }
}

public interface IMacroSignatureHolder
{
    MacroSignature? MacroSignature { get; set; }

    IReadOnlyList<(string Name, string Language, string Source)> SignableMacros { get; }
}
