using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Formats.Packaging;

namespace PulseOffice.Formats.Odf;

internal readonly record struct AnchoredFrame(XElement Frame, CellAddress Anchor);

internal sealed class AnchoredFrames
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    private readonly IReadOnlySet<XName> _frames;

    public AnchoredFrames() : this(Draw + "frame") { }

    public AnchoredFrames(params XName[] frames) => _frames = new HashSet<XName>(frames);

    private readonly TableCells _cells = new();

    private readonly CellAddress _page = new(0, 0);

    public IEnumerable<AnchoredFrame> In(XElement table) =>
        _cells.In(table).SelectMany(cell => cell.Element.Elements()
            .Where(child => _frames.Contains(child.Name))
            .Select(frame => new AnchoredFrame(frame, cell.Address)))
        .Concat(table.Elements(OdfPackage.Namespaces.Table + "shapes").Elements()
            .Where(child => _frames.Contains(child.Name))
            .Select(frame => new AnchoredFrame(frame, _page)));
}
