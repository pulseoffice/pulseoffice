using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Formulas;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfFormulaMapper
{
    private readonly IFormulaParser _parser;
    private readonly IMathMlSyntax _mathml;

    public OdfFormulaMapper(IMathMlSyntax? mathml = null, IFormulaParser? parser = null)
    {
        _mathml = mathml ?? new MathMlSyntax();
        _parser = parser ?? new FormulaParser();
    }

    private readonly OdfDrawUnits _units = new();
    private readonly AnchoredFrames _frames = new();

    public IReadOnlyList<(string Path, XDocument Document)> Documents(Worksheet sheet, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var documents = new List<(string, XDocument)>();
        var index = startIndex;

        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not FormulaObject formula) continue;

            var math = _mathml.Write(_parser.Parse(formula.Expression));

            documents.Add(($"Formula {index++}", new XDocument(math)));
        }

        return documents;
    }

    public IReadOnlyDictionary<CellAddress, List<XElement>> BuildFrames(Worksheet sheet, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var frames = new Dictionary<CellAddress, List<XElement>>();
        var index = startIndex;

        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not FormulaObject formula) continue;

            var frame = new XElement(
                Namespaces.Draw + "frame",
                new XAttribute(Namespaces.Draw + "name", placed.Id),
                new XAttribute(Namespaces.Draw + "z-index", placed.ZOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Svg + "x", _units.Length(placed.OffsetX)),
                new XAttribute(Namespaces.Svg + "y", _units.Length(placed.OffsetY)),
                new XAttribute(Namespaces.Svg + "width", _units.Length(placed.Size.Width)),
                new XAttribute(Namespaces.Svg + "height", _units.Length(placed.Size.Height)),
                new XElement(
                    Namespaces.Draw + "object",
                    new XAttribute(Namespaces.Xlink + "href", $"./Formula {index++}"),
                    new XAttribute(Namespaces.Xlink + "type", "simple"),
                    new XAttribute(Namespaces.Xlink + "show", "embed"),
                    new XAttribute(Namespaces.Xlink + "actuate", "onLoad")),
                new XElement(
                    Namespaces.Draw + "image",
                    new XAttribute(Namespaces.Draw + "mime-type", "application/vnd.oasis.opendocument.formula"),
                    new XElement(Namespaces.Text + "p", formula.Expression)));

            if (!frames.TryGetValue(placed.Anchor, out var list)) frames[placed.Anchor] = list = [];

            list.Add(frame);
        }

        return frames;
    }

    public IReadOnlyList<PlacedObject> Read(XElement table, OdfPackage package)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(package);

        var placed = new List<PlacedObject>();
        foreach (var (frame, anchor) in _frames.In(table))
        {
            var reference = frame.Elements(Namespaces.Draw + "object").Select(node => (string?)node.Attribute(Namespaces.Xlink + "href")).FirstOrDefault();
            if (reference is null) continue;
            if (package.ReadXml($"{reference.TrimStart('.', '/')}/content.xml")?.Root is not { } root || root.Name.LocalName != "math") continue;

            var formula = _mathml.Read(root);
            var source = formula.Source.Length > 0 ? formula.Source : frame.Descendants(Namespaces.Text + "p").FirstOrDefault()?.Value ?? string.Empty;
            var id = (string?)frame.Attribute(Namespaces.Draw + "name") ?? Guid.NewGuid().ToString("N");

            placed.Add(new PlacedObject
            {
                Id = id,
                Anchor = anchor,
                OffsetX = _units.Parse((string?)frame.Attribute(Namespaces.Svg + "x")),
                OffsetY = _units.Parse((string?)frame.Attribute(Namespaces.Svg + "y")),
                ZOrder = _units.Order(frame) is var order and >= 0 ? order : 0,
                Size = new DocumentSize(_units.Parse((string?)frame.Attribute(Namespaces.Svg + "width")), _units.Parse((string?)frame.Attribute(Namespaces.Svg + "height"))),
                Content = new FormulaObject { Id = id, Expression = source },
            });
        }
        return placed;
    }
}
