using PulseOffice.Document.Forms;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public interface IControlTabOrder
{
    IReadOnlyList<SlideElement> Apply(IReadOnlyList<ObjectElement> controls, IReadOnlyList<string> names);
}

public sealed class ControlTabOrder : IControlTabOrder
{
    public IReadOnlyList<SlideElement> Apply(IReadOnlyList<ObjectElement> controls, IReadOnlyList<string> names)
    {
        var positions = names
            .Select((name, index) => (name, index))
            .GroupBy(entry => entry.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new Queue<int>(group.Select(entry => entry.index)), StringComparer.Ordinal);
        var updated = new List<SlideElement>();
        foreach (var element in controls)
        {
            if (element.Content is not FormControlObject control) continue;
            if (!positions.TryGetValue(control.Properties.Name, out var queue) || queue.Count == 0) continue;
            updated.Add(element with { Content = control with { Properties = control.Properties with { TabIndex = queue.Dequeue() } } });
        }
        return updated;
    }
}
