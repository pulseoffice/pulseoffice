using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.UI.GridParts;

internal sealed class GridDisplayOptions
{
    public bool ShowFormulas { get; set; }

    public bool ShowGridLines { get; set; } = true;

    public bool HighlightValues { get; set; }

    public bool HighlightRowAndColumn { get; set; }

    public bool ShowHiddenMarkers { get; set; }

    public bool ShowPageBreaks { get; set; }

    public bool PickingReference { get; set; }

    public bool DesignMode { get; set; }

    public ISheetSpelling? Spelling { get; set; }

    public ShapeObject? LiveShape { get; set; }

    public Func<CellAddress, bool> ChoicesAvailable { get; set; } = _ => false;
}
