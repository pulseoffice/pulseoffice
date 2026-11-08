using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Encryption.OpenPgp;

internal sealed class OpenPgpManifestKeys
{
    private const string RsaOaep = "http://www.w3.org/2001/04/xmlenc#rsa-oaep-mgf1p";
    private static readonly XNamespace Manifest = OdfPackage.Namespaces.Manifest;

    public bool IsPresent(XDocument manifest) =>
        manifest.Descendants().Any(element => element.Name.LocalName is "keyinfo" or "KeyInfo" or "PGPKeyID" or "encrypted-key");

    public IReadOnlyList<OpenPgpKeySlot> Read(XDocument manifest)
    {
        var slots = new List<OpenPgpKeySlot>();
        foreach (var holder in manifest.Descendants().Where(element => element.Name.LocalName is "encrypted-key" or "KeyInfo"))
        {
            if (Bytes(holder, "CipherValue") is not { Length: > 0 } cipher) continue;
            slots.Add(new OpenPgpKeySlot(Bytes(holder, "PGPKeyID") ?? [], Bytes(holder, "PGPKeyPacket"), cipher));
        }

        return slots;
    }

    public void Remove(XElement root) =>
        root.Elements().Where(element => element.Name.LocalName is "encrypted-key" or "keyinfo").Remove();

    public void Write(XElement root, IReadOnlyList<OpenPgpKeySlot> slots)
    {
        Remove(root);
        root.AddFirst(slots.Select(slot =>
            new XElement(Manifest + "encrypted-key",
                new XElement(Manifest + "encryption-method", new XAttribute(Manifest + "PGPAlgorithm", RsaOaep)),
                new XElement(Manifest + "keyinfo",
                    new XElement(Manifest + "PGPData",
                        new XElement(Manifest + "PGPKeyID", Convert.ToBase64String(slot.KeyId)),
                        slot.KeyPacket is { Length: > 0 } packet ? new XElement(Manifest + "PGPKeyPacket", Convert.ToBase64String(packet)) : null)),
                new XElement(Manifest + "CipherData",
                    new XElement(Manifest + "CipherValue", Convert.ToBase64String(slot.CipherValue))))));
    }

    private byte[]? Bytes(XElement holder, string name)
    {
        if (holder.Descendants().FirstOrDefault(element => element.Name.LocalName == name) is not { } element) return null;
        try
        {
            return Convert.FromBase64String(element.Value.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidDataException("Invalid OpenPGP key information.");
        }
    }
}
