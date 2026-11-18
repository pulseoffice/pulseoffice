using PulseOffice.Documents.Objects;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxObjectAlignments
{
    public string? Horizontal(ObjectAlignment alignment) => alignment switch
    {
        ObjectAlignment.Start => "left",
        ObjectAlignment.Center => "center",
        ObjectAlignment.End => "right",
        ObjectAlignment.Inside => "inside",
        ObjectAlignment.Outside => "outside",
        _ => null,
    };

    public string? Vertical(ObjectAlignment alignment) => alignment switch
    {
        ObjectAlignment.Start => "top",
        ObjectAlignment.Center => "center",
        ObjectAlignment.End => "bottom",
        ObjectAlignment.Inside => "inside",
        ObjectAlignment.Outside => "outside",
        _ => null,
    };

    public ObjectAlignment Horizontal(string? word) => word switch
    {
        "left" => ObjectAlignment.Start,
        "center" => ObjectAlignment.Center,
        "right" => ObjectAlignment.End,
        "inside" => ObjectAlignment.Inside,
        "outside" => ObjectAlignment.Outside,
        _ => ObjectAlignment.None,
    };

    public ObjectAlignment Vertical(string? word) => word switch
    {
        "top" => ObjectAlignment.Start,
        "center" => ObjectAlignment.Center,
        "bottom" => ObjectAlignment.End,
        "inside" => ObjectAlignment.Inside,
        "outside" => ObjectAlignment.Outside,
        _ => ObjectAlignment.None,
    };
}
