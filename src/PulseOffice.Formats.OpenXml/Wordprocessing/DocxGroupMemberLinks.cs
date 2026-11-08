using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using WPG = DocumentFormat.OpenXml.Office2010.Word.DrawingGroup;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxGroupMemberLinks
{
    public string? Read(OpenXmlElement member) =>
        Properties(member)?.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value is { Length: > 0 } relationship ? relationship : null;

    public void Write(OpenXmlElement member, string relationship)
    {
        var properties = Properties(member) ?? Added(member);
        if (properties is null) return;
        properties.GetFirstChild<A.HyperlinkOnClick>()?.Remove();
        properties.PrependChild(new A.HyperlinkOnClick { Id = relationship });
    }

    private OpenXmlCompositeElement? Properties(OpenXmlElement member) => member switch
    {
        WPS.WordprocessingShape shape => shape.GetFirstChild<WPS.NonVisualDrawingProperties>(),
        PIC.Picture picture => picture.NonVisualPictureProperties?.NonVisualDrawingProperties,
        WPG.GraphicFrame frame => frame.NonVisualDrawingProperties,
        _ => null,
    };

    private OpenXmlCompositeElement? Added(OpenXmlElement member)
    {
        if (member is not WPS.WordprocessingShape shape) return null;
        var properties = new WPS.NonVisualDrawingProperties { Id = 0U, Name = string.Empty };
        shape.PrependChild(properties);
        return properties;
    }
}
