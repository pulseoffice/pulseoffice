using PulseOffice.Drawing.Freeform;
using PulseOffice.UI.Controls.Drawing;

namespace PulseOffice.Sheets.UI;

public sealed record FreeformCompletedEventArgs(
    FreeformPath Path,
    PulseOffice.Sheets.Addressing.CellAddress Anchor,
    PulseOffice.Foundation.Units.Twip OffsetX,
    PulseOffice.Foundation.Units.Twip OffsetY,
    PulseOffice.Foundation.Units.DocumentSize Size,
    bool Filled);

public sealed record FreeformEditedEventArgs(PlacedObject Placed, FreeformPath Path);

public sealed record ShapeTextRequest(PlacedObject Placed, string? Typed);

public sealed record ShapeAdjustedEventArgs(PlacedObject Placed, PulseOffice.Drawing.Shapes.ShapeObject Shape);
