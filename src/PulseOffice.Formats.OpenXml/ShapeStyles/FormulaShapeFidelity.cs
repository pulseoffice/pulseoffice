using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Shapes.Geometry;

namespace PulseOffice.Formats.OpenXml;

public sealed class FormulaShapeFidelity
{
    public bool LosesFormulas(EmbeddedObject? content) => content switch
    {
        ShapeObject { Computed: FormulaGeometry } => true,
        ShapeGroupObject group => group.Children.Any(child => LosesFormulas(child.Content)),
        _ => false,
    };
}
