using System.Text;
using System.Xml.Linq;
using System.Globalization;
using PulseOffice.Document.Forms;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Security;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfMacroMapper
{
    private const string Folder = "Scripts/pulseoffice/";
    private static readonly XNamespace Signed = "urn:pulseoffice:macro-signature";

    public IReadOnlyList<(string Path, string MediaType)> Write(OdfPackage package, IMacroLibrary macros, MacroSignature? signature = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(macros);

        if (macros.All.Count == 0) return [];

        var index = new XElement(Namespaces.Script + "module-index");
        var written = new List<(string, string)>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "index" };

        foreach (var macro in macros.All)
        {
            var stem = Safe(macro.Name);
            var unique = stem;
            for (var counter = 2; !used.Add(unique); counter++) unique = $"{stem}_{counter}";
            var file = $"{Folder}{unique}.{Extension(macro.Language)}";

            package.WriteBytes(file, Encoding.UTF8.GetBytes(macro.Source));
            written.Add((file, "text/plain"));

            index.Add(new XElement(
                Namespaces.Script + "module",
                new XAttribute(Namespaces.Script + "name", macro.Name),
                new XAttribute(Namespaces.Script + "language", macro.Language.ToString()),
                new XAttribute(Namespaces.Xlink + "href", file)));
        }

        var indexPath = $"{Folder}index.xml";

        package.WriteXml(indexPath, new XDocument(
            new XElement(index.Name,
                new XAttribute(XNamespace.Xmlns + "script", Namespaces.Script.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "xlink", Namespaces.Xlink.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "qs", Signed.NamespaceName),
                index.Elements(),
                signature is null ? null : SignatureElement(signature))));

        written.Add((indexPath, "text/xml"));

        return written;
    }

    public MacroSignature? Read(OdfPackage package, IMacroLibrary macros)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(macros);

        if (package.ReadXml($"{Folder}index.xml") is not { } index) return null;

        foreach (var module in index.Descendants(Namespaces.Script + "module"))
        {
            var name = module.Attribute(Namespaces.Script + "name")?.Value;
            var href = module.Attribute(Namespaces.Xlink + "href")?.Value;

            if (name is null || href is null) continue;
            if (package.ReadBytes(href) is not { } bytes) continue;

            var language = module.Attribute(Namespaces.Script + "language")?.Value == "Python"
                ? MacroLanguage.Python
                : MacroLanguage.JavaScript;

            macros.Store(new DocumentMacro(name, language, Encoding.UTF8.GetString(bytes)));
        }

        return index.Root?.Element(Signed + "signature") is { } element ? SignatureFrom(element) : null;
    }

    public Diagnostic? BasicLoss(OdfPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        using var basic = package.OpenEntry("Basic/script-lc.xml");
        return basic is null
            ? null
            : Diagnostic.Warning("ODF041", "The document contains Basic macros: PulseOffice does not run Basic and discards them, together with their signature, so formulas and events calling Basic do nothing or return an error.");
    }

    private XElement SignatureElement(MacroSignature signature) =>
        new(Signed + "signature",
            new XAttribute("signer", signature.Signer.Name),
            new XAttribute("issuer", signature.Signer.Issuer),
            new XAttribute("thumbprint", signature.Signer.Thumbprint),
            new XAttribute("not-after", signature.Signer.NotAfter.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("signed-on", signature.SignedOn.ToString("O", CultureInfo.InvariantCulture)),
            new XAttribute("value", signature.Value),
            signature.Certificate is { } certificate ? new XAttribute("certificate", certificate) : null);

    private MacroSignature? SignatureFrom(XElement element)
    {
        if (element.Attribute("value")?.Value is not { Length: > 0 } value) return null;

        var signer = new SignerIdentity(
            element.Attribute("signer")?.Value ?? string.Empty,
            element.Attribute("issuer")?.Value ?? string.Empty,
            element.Attribute("thumbprint")?.Value ?? string.Empty,
            Moment(element.Attribute("not-after")?.Value));

        if (element.Attribute("certificate")?.Value is { Length: > 0 } raw)
        {
            try
            {
                using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(Convert.FromBase64String(raw));
                signer = SignerIdentity.From(certificate);
            }
            catch (Exception ex) when (ex is FormatException or System.Security.Cryptography.CryptographicException)
            {
            }
        }

        return new MacroSignature(signer, Moment(element.Attribute("signed-on")?.Value), value)
        {
            Certificate = element.Attribute("certificate")?.Value,
        };
    }

    private DateTimeOffset Moment(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : DateTimeOffset.MinValue;

    private string Extension(MacroLanguage language) =>
        language == MacroLanguage.Python ? "py" : "js";

    private string Safe(string name) =>
        new([.. name.Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')]);
}
