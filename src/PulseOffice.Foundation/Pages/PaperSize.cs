using PulseOffice.Foundation.Units;

namespace PulseOffice.Foundation.Pages;

public sealed record PaperSize(string Id, DocumentSize Size)
{
    public Twip Width => Size.Width;

    public Twip Height => Size.Height;
}

public interface IPaperCatalog
{
    IReadOnlyList<PaperSize> All { get; }

    PaperSize Default { get; }

    PaperSize? Find(string id);
}

public sealed class PaperCatalog : IPaperCatalog
{
    private static PaperSize Millimeters(string id, double width, double height) =>
        new(id, new DocumentSize(Twip.FromMillimeters(width), Twip.FromMillimeters(height)));

    private static PaperSize Inches(string id, double width, double height) =>
        new(id, new DocumentSize(Twip.FromInches(width), Twip.FromInches(height)));

    public IReadOnlyList<PaperSize> All { get; } =
    [
        Millimeters("a3", 297, 420),
        Millimeters("a4", 210, 297),
        Millimeters("a5", 148, 210),
        Millimeters("b5", 176, 250),
        Inches("letter", 8.5, 11),
        Inches("legal", 8.5, 14),
        Inches("tabloid", 11, 17),
        Inches("executive", 7.25, 10.5),
    ];

    public PaperSize Default => Find("a4") ?? All[0];

    public PaperSize? Find(string id) =>
        All.FirstOrDefault(paper => string.Equals(paper.Id, id, StringComparison.OrdinalIgnoreCase));
}
