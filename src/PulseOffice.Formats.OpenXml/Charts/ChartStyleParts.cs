using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;

namespace PulseOffice.Formats.OpenXml;

internal sealed class ChartStyleParts
{
    private const string Namespace = "http://schemas.microsoft.com/office/drawing/2012/chartStyle";
    private const int LargestPart = 1 << 20;

    private readonly SafeXmlLoader _loader = new();

    public void Write(ChartPart chart, ChartStyleReference style)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(style);
        if (Valid(style.ChartStyle, "chartStyle") is { } chartStyle) Save(chart.AddNewPart<ChartStylePart>(), chartStyle);
        if (Valid(style.ColorStyle, "colorStyle") is { } colorStyle) Save(chart.AddNewPart<ChartColorStylePart>(), colorStyle);
    }

    public ChartStyleReference Read(ChartPart chart, ChartStyleReference style)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(style);
        return style with
        {
            ChartStyle = Text(chart.ChartStyleParts.FirstOrDefault(), "chartStyle"),
            ColorStyle = Text(chart.ChartColorStyleParts.FirstOrDefault(), "colorStyle"),
        };
    }

    private string? Text(OpenXmlPart? part, string root)
    {
        if (part is null) return null;
        try
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            if (stream.CanSeek && stream.Length > LargestPart) return null;
            var element = _loader.Element(stream);
            return element.Name == XName.Get(root, Namespace) ? element.ToString(SaveOptions.DisableFormatting) : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private XElement? Valid(string? text, string root)
    {
        if (string.IsNullOrEmpty(text) || text.Length > LargestPart) return null;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var element = _loader.Element(stream);
            return element.Name == XName.Get(root, Namespace) ? element : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private void Save(OpenXmlPart part, XElement element)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), element).Save(stream);
    }
}
