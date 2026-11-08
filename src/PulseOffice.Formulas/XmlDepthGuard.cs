using System.Xml.Linq;

namespace PulseOffice.Formulas;

internal sealed class XmlDepthGuard(int limit)
{
    public bool Exceeds(XElement root)
    {
        var pending = new Stack<(XElement Element, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (element, depth) = pending.Pop();
            if (depth > limit) return true;
            foreach (var child in element.Elements()) pending.Push((child, depth + 1));
        }
        return false;
    }
}
