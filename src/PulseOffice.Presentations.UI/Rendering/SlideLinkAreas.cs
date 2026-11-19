using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed record SlideLinkArea(DocumentRect Bounds, string Target);

public interface ISlideLinkAreas
{
    IReadOnlyList<SlideLinkArea> For(Slide slide);
}

public sealed class SlideLinkAreas : ISlideLinkAreas
{
    public IReadOnlyList<SlideLinkArea> For(Slide slide)
    {
        ArgumentNullException.ThrowIfNull(slide);
        var visible = slide.Elements.Where(element => !element.Hidden).ToDictionary(element => element.Id, StringComparer.Ordinal);
        var areas = new List<SlideLinkArea>();
        var linked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in slide.Interactions)
        {
            if (action is not { Kind: InteractionKind.Hyperlink, Reference: { Length: > 0 } target }) continue;
            if (!visible.TryGetValue(action.TargetId, out var element) || !linked.Add(element.Id)) continue;
            areas.Add(new SlideLinkArea(element.Bounds, target));
        }
        foreach (var element in visible.Values.OfType<TextElement>())
        {
            if (linked.Contains(element.Id)) continue;
            var runs = element.Text.Paragraphs.SelectMany(paragraph => paragraph.EffectiveRuns()).Where(run => run.Text.Trim().Length > 0).ToList();
            var targets = runs.Select(run => run.Hyperlink).Distinct(StringComparer.Ordinal).ToList();
            if (targets is [{ Length: > 0 } only]) areas.Add(new SlideLinkArea(element.Bounds, only));
        }
        return areas;
    }
}
