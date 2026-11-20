using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxGridTableStyles(ITableGridLook look)
{
    public const string Name = "PulseOffice Grid";

    private const string DefaultStyleId = "{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}";

    private const string Template =
        "<a:tblStyle xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" styleId=\"{0}\" styleName=\"" + Name + "\">" +
        "<a:wholeTbl><a:tcTxStyle><a:fontRef idx=\"minor\"/><a:srgbClr val=\"{4}\"/></a:tcTxStyle><a:tcStyle><a:tcBdr>" +
        "<a:left>{3}</a:left><a:right>{3}</a:right><a:top>{3}</a:top><a:bottom>{3}</a:bottom><a:insideH>{3}</a:insideH><a:insideV>{3}</a:insideV>" +
        "</a:tcBdr><a:fill><a:noFill/></a:fill></a:tcStyle></a:wholeTbl>" +
        "<a:band2H><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:srgbClr val=\"{2}\"/></a:solidFill></a:fill></a:tcStyle></a:band2H>" +
        "<a:firstRow><a:tcTxStyle b=\"on\"><a:fontRef idx=\"minor\"/><a:srgbClr val=\"{5}\"/></a:tcTxStyle><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:srgbClr val=\"{1}\"/></a:solidFill></a:fill></a:tcStyle></a:firstRow>" +
        "</a:tblStyle>";

    public string Ensure(SlidePart slide, DataTableStyle style)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(style);
        var accent = look.Accent(style);
        var id = Id(accent);
        if (slide.GetParentParts().OfType<PresentationPart>().FirstOrDefault() is not { } presentation) return id;
        var part = presentation.TableStylesPart ?? presentation.AddNewPart<TableStylesPart>();
        part.TableStyleList ??= new D.TableStyleList { Default = DefaultStyleId };
        if (!part.TableStyleList.Elements<D.TableStyleEntry>().Any(entry => string.Equals(entry.StyleId?.Value, id, StringComparison.OrdinalIgnoreCase)))
            part.TableStyleList.Append(Entry(id, style));
        return id;
    }

    public D.TableStyleEntry Entry(string id, DataTableStyle style)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"<a:ln w=\"{(long)Math.Round(look.LineWidthPoints * PulseOffice.Foundation.Units.Emu.PerPoint)}\"><a:solidFill><a:srgbClr val=\"{Bare(look.LineColour)}\"/></a:solidFill></a:ln>");
        return new D.TableStyleEntry(string.Format(CultureInfo.InvariantCulture, Template,
            id, Bare(look.HeaderFill(style)), Bare(look.BandFill), line, Bare(look.BodyInk), Bare(look.Accent(style))));
    }

    public bool IsOurs(DocumentFormat.OpenXml.OpenXmlElement? style) => style is D.TableStyleEntry { StyleName.Value: Name };

    public string? AccentOf(DocumentFormat.OpenXml.OpenXmlElement style) =>
        style.Descendants<D.FirstRow>().FirstOrDefault()?.Descendants<D.RgbColorModelHex>().FirstOrDefault()?.Val?.Value is { Length: 6 } hex
            ? "#" + hex.ToUpperInvariant()
            : null;

    private string Id(string accent)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Name + accent));
        return "{" + new Guid(hash.AsSpan(0, 16)).ToString().ToUpperInvariant() + "}";
    }

    private string Bare(string hex) => hex.TrimStart('#').ToUpperInvariant();
}
