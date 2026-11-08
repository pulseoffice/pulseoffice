namespace PulseOffice.Formats.OpenXml;

internal sealed class ExternalTargetUris
{
    public Uri Of(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return Uri.TryCreate(target, UriKind.RelativeOrAbsolute, out var parsed) ? parsed : new Uri(Uri.EscapeDataString(target), UriKind.Relative);
    }
}
