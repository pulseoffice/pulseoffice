using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class MasterEditing
{
    public SlideMaster WithBackground(SlideMaster master, ColorRole background) =>
        master with { Background = background };

    public SlideMaster WithLayoutBackground(SlideMaster master, SlideLayout kind, ColorRole? background) =>
        WithLayoutBackground(master, master.Template(kind).Id, background);

    public SlideMaster WithLayoutBackground(SlideMaster master, string layoutId, ColorRole? background) =>
        ReplaceLayout(master, layoutId, template => template with { Background = background });

    public SlideMaster WithPlaceholderBounds(
        SlideMaster master, SlideLayout kind, PlaceholderRole role, DocumentRect bounds) =>
        WithPlaceholderBounds(master, master.Template(kind).Id, role, bounds);

    public SlideMaster WithPlaceholderBounds(
        SlideMaster master, string layoutId, PlaceholderRole role, DocumentRect bounds) =>
        ReplaceLayout(master, layoutId, template => template with
        {
            Placeholders = [.. template.Placeholders.Select(p => p.Role == role ? p with { Bounds = bounds } : p)],
        });

    public SlideMaster WithPlaceholderSeed(
        SlideMaster master, SlideLayout kind, PlaceholderRole role, TextParagraph seed) =>
        ReplaceLayout(master, master.Template(kind).Id, template => template with
        {
            Placeholders = [.. template.Placeholders.Select(p => p.Role == role ? p with { Seed = seed } : p)],
        });

    private SlideMaster ReplaceLayout(
        SlideMaster master, string layoutId, Func<SlideLayoutTemplate, SlideLayoutTemplate> change) =>
        master with
        {
            Layouts = [.. master.Layouts.Select(layout => string.Equals(layout.Id, layoutId, StringComparison.Ordinal) ? change(layout) : layout)],
        };
}
