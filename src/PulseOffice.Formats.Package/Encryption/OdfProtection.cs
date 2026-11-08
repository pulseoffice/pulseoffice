using System.Security.Cryptography;
using System.Xml.Linq;
using PulseOffice.Formats.Packaging.Encryption.OpenPgp;

namespace PulseOffice.Formats.Packaging.Encryption;

public abstract record OdfProtection
{
    internal abstract IEntryKeying Keying();
}

public sealed record PasswordProtection(string Password) : OdfProtection
{
    internal override IEntryKeying Keying() => new PasswordKeying(Password);
}

public sealed record OpenPgpProtection(byte[] SessionKey, IReadOnlyList<OpenPgpKeySlot> Slots) : OdfProtection
{
    internal override IEntryKeying Keying() => new OpenPgpKeying(SessionKey, Slots);
}

internal interface IEntryKeying
{
    bool UsesSalt { get; }

    byte[] KeyFor(byte[] salt);

    IEnumerable<XElement> Describe(byte[] salt);

    void Decorate(XElement root);
}

internal sealed class PasswordKeying(string password) : IEntryKeying
{
    private const int Iterations = 100_000;
    private const int KeySize = 32;
    private static readonly XNamespace Manifest = OdfPackage.Namespaces.Manifest;
    private readonly byte[] _startKey = HashOf(password);

    public bool UsesSalt => true;

    private static byte[] HashOf(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public byte[] KeyFor(byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(_startKey, salt, Iterations, HashAlgorithmName.SHA1, KeySize);

    public IEnumerable<XElement> Describe(byte[] salt) =>
    [
        new XElement(Manifest + "start-key-generation",
            new XAttribute(Manifest + "start-key-generation-name", OdfCryptoNames.Sha256StartKey),
            new XAttribute(Manifest + "key-size", KeySize)),
        new XElement(Manifest + "key-derivation",
            new XAttribute(Manifest + "key-derivation-name", "PBKDF2"),
            new XAttribute(Manifest + "key-size", KeySize),
            new XAttribute(Manifest + "iteration-count", Iterations),
            new XAttribute(Manifest + "salt", Convert.ToBase64String(salt))),
    ];

    public void Decorate(XElement root) => new OpenPgpManifestKeys().Remove(root);
}

internal sealed class OpenPgpKeying(byte[] sessionKey, IReadOnlyList<OpenPgpKeySlot> slots) : IEntryKeying
{
    private static readonly XNamespace Manifest = OdfPackage.Namespaces.Manifest;

    public bool UsesSalt => false;

    public byte[] KeyFor(byte[] salt) => sessionKey;

    public IEnumerable<XElement> Describe(byte[] salt) =>
    [
        new XElement(Manifest + "key-derivation",
            new XAttribute(Manifest + "key-derivation-name", OdfCryptoNames.OpenPgpDerivation),
            new XAttribute(Manifest + "key-size", sessionKey.Length)),
    ];

    public void Decorate(XElement root) => new OpenPgpManifestKeys().Write(root, slots);
}
