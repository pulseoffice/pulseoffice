using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed record OdpReadContext(
    OdpStyleSheet Styles,
    OdfValues Values,
    Func<string, byte[]?> ReadBytes,
    Func<string, XDocument?> ReadXml)
{
    public HashSet<string> CommentIds { get; } = new(StringComparer.Ordinal);
}
