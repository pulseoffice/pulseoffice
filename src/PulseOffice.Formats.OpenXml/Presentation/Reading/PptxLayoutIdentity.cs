using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.OpenXml;

internal sealed record PptxLayoutEntry(SlideLayoutPart Part, SlideLayout Kind, string? CustomId);

internal sealed class PptxLayoutIdentity(ISlideLayoutRegistry layouts)
{
    public const string CustomPrefix = "layout.custom.";

    public IReadOnlyList<PptxLayoutEntry> Entries(SlideMasterPart master)
    {
        ArgumentNullException.ThrowIfNull(master);
        var claimed = new HashSet<SlideLayout>();
        var entries = new List<PptxLayoutEntry>();
        var ordinal = 0;
        foreach (var part in master.SlideLayoutParts)
        {
            ordinal++;
            var kind = KindOf(part);
            entries.Add(new PptxLayoutEntry(part, kind, claimed.Add(kind) ? null : $"{CustomPrefix}{ordinal}"));
        }
        return entries;
    }

    public SlideLayout KindOf(SlideLayoutPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return layouts.FromOoxml(part.SlideLayout?.Type?.InnerText ?? string.Empty, part.SlideLayout?.CommonSlideData?.Name?.Value);
    }

    public string? IdOf(SlideLayoutPart? part) =>
        part?.SlideMasterPart is { } master
            ? Entries(master).FirstOrDefault(entry => ReferenceEquals(entry.Part, part))?.CustomId
            : null;
}
