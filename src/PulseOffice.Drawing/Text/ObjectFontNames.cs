using PulseOffice.Document.Embedding;
using PulseOffice.Document.Forms;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Drawing.Text;

public interface IObjectFontNames
{
    IEnumerable<string> Of(EmbeddedObject content);
}

public sealed class ObjectFontNames : IObjectFontNames
{
    public IEnumerable<string> Of(EmbeddedObject content) => content switch
    {
        ShapeObject shape => [shape.Style.Text.Family, .. RunFamilies(shape.Runs)],
        FreeformObject freeform => [freeform.Style.Text.Family],
        FontworkObject fontwork => [fontwork.FontFamily],
        FormControlObject control => [control.Properties.Text.Family],
        ShapeGroupObject group => group.Children.SelectMany(child => Of(child.Content)),
        _ => [],
    };

    private IEnumerable<string> RunFamilies(IReadOnlyList<ShapeTextRun>? runs) =>
        runs?.Select(run => run.Family).OfType<string>().Where(family => family.Length > 0) ?? [];
}
