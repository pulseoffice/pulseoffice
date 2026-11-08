using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxVmlShapes(int block)
{
    private int _next = block * 1024 + 1;

    public XNamespace Vml { get; } = "urn:schemas-microsoft-com:vml";

    public XNamespace Office { get; } = "urn:schemas-microsoft-com:office:office";

    public XNamespace Excel { get; } = "urn:schemas-microsoft-com:office:excel";

    public int Block => block;

    public string Next() => Name(NextNumber());

    public int NextNumber() => _next++;

    public string Name(int number) => "_x0000_s" + number.ToString(CultureInfo.InvariantCulture);
}
