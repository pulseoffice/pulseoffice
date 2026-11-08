using DocumentFormat.OpenXml;

namespace PulseOffice.Formats.OpenXml;

public sealed record PptxFlavour(PresentationFormat Format, PresentationDocumentType Type)
{
    public static readonly PptxFlavour Presentation = new(new PresentationFormat
    {
        Id = "pptx",
        DisplayName = "PowerPoint Presentation",
        Extensions = [".pptx"],
        MimeType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        IsNative = false,
    }, PresentationDocumentType.Presentation);

    public static readonly PptxFlavour Template = new(new PresentationFormat
    {
        Id = "potx",
        DisplayName = "PowerPoint Template",
        Extensions = [".potx"],
        MimeType = "application/vnd.openxmlformats-officedocument.presentationml.template",
        IsNative = false,
    }, PresentationDocumentType.Template);
}
