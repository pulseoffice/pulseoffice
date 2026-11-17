using DocumentFormat.OpenXml;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

public interface IDrawingPlainText
{
    string Paragraph(D.Paragraph paragraph);

    string Body(OpenXmlElement? body);
}

public sealed class DrawingPlainText : IDrawingPlainText
{
    public string Paragraph(D.Paragraph paragraph)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        return string.Concat(paragraph.ChildElements.Select(child => child switch
        {
            D.Run run => run.Text?.Text ?? string.Empty,
            D.Break => "\n",
            D.Field field => field.Text?.Text ?? string.Empty,
            _ => string.Concat(child.Descendants<D.Text>().Select(text => text.Text)),
        }));
    }

    public string Body(OpenXmlElement? body) =>
        body is null ? string.Empty : string.Join("\n", body.Elements<D.Paragraph>().Select(Paragraph));
}
