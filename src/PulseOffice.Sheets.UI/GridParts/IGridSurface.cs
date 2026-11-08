using Avalonia.Input;

namespace PulseOffice.Sheets.UI.GridParts;

internal interface IGridSurface
{
    double Zoom { get; set; }

    void Invalidate();

    void Capture(IPointer pointer);

    void ShowCursor(Cursor cursor);

    void ShowTip(string? text);
}
