using PulseOffice.Documents.Objects;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxObjectAlignments
{
    public string? Horizontal(ObjectAlignment alignment) => alignment switch
    {
        ObjectAlignment.Start => "left",
        ObjectAlignment.Center => "center",
        ObjectAlignment.End => "right",
        _ => null,
    };

    public string? Vertical(ObjectAlignment alignment) => alignment switch
    {
        ObjectAlignment.Start => "top",
        ObjectAlignment.Center => "center",
        ObjectAlignment.End => "bottom",
        _ => null,
    };

    public ObjectAlignment Horizontal(string? word) => word switch
    {
        "left" or "inside" => ObjectAlignment.Start,
        "center" => ObjectAlignment.Center,
        "right" or "outside" => ObjectAlignment.End,
        _ => ObjectAlignment.None,
    };

    public ObjectAlignment Vertical(string? word) => word switch
    {
        "top" or "inside" => ObjectAlignment.Start,
        "center" => ObjectAlignment.Center,
        "bottom" or "outside" => ObjectAlignment.End,
        _ => ObjectAlignment.None,
    };
}
