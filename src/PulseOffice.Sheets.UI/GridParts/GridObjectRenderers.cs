using PulseOffice.Document.Forms;
using PulseOffice.UI.Controls.Drawing;
using PulseOffice.UI.Controls.Forms;
using PulseOffice.UI.Controls.Shapes;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridObjectRenderers(
    IDrawingObjectRenderer shapes,
    IFormControlRenderer forms,
    ISignatureLineRenderer signatures,
    PulseOffice.UI.Controls.Formulas.IFormulaRenderer formulas,
    PulseOffice.UI.Controls.Objects.IPackagedObjectRenderer packaged,
    PulseOffice.Formulas.IFormulaParser formulaParser,
    IFormControlBinding formBinding)
{
    public IDrawingObjectRenderer Shapes => shapes;

    public IFormControlRenderer Forms => forms;

    public ISignatureLineRenderer Signatures => signatures;

    public PulseOffice.UI.Controls.Formulas.IFormulaRenderer Formulas => formulas;

    public PulseOffice.UI.Controls.Objects.IPackagedObjectRenderer Packaged => packaged;

    public PulseOffice.Formulas.IFormulaParser FormulaParser => formulaParser;

    public System.Globalization.CultureInfo Culture => formBinding.Culture;

    public IReadOnlyList<string> ItemsOf(GridViewport viewport, FormControlObject form) =>
        viewport.Sheet is { } sheet ? formBinding.ItemsFor(sheet, form.Properties, viewport.Engine.Workbook) : form.Properties.Items;
}
