namespace PulseOffice.Formats.Odf;

internal sealed class OdfHrefs
{
    private const string Package = "vnd.sun.star.Package:";

    public string Normalize(string href)
    {
        var value = href.StartsWith(Package, StringComparison.Ordinal) ? href[Package.Length..] : href;
        if (value.StartsWith("./", StringComparison.Ordinal)) value = value[2..];
        return value.TrimStart('/');
    }
}
