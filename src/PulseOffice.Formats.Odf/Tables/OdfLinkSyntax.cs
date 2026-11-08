namespace PulseOffice.Formats.Odf;

internal sealed class OdfLinkSyntax
{
    private const string PackageParent = "../";

    public string ToOdf(string link)
    {
        if (link.StartsWith('#') || HasScheme(link) || Path.IsPathRooted(link)) return link;
        return PackageParent + link.Replace('\\', '/');
    }

    public string FromOdf(string href)
    {
        if (href.StartsWith('#') || HasScheme(href)) return href;
        return href.StartsWith(PackageParent, StringComparison.Ordinal) ? href[PackageParent.Length..] : href;
    }

    private bool HasScheme(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme.Length > 1 && !link.StartsWith('/');
}
