using Avalonia;
using Avalonia.Input;

namespace PulseOffice.Sheets.UI.GridParts.Gestures;

internal enum GestureStage
{
    BeforeDrawing,
    BeforeCursor,
    AfterCursor,
}

internal abstract class GridGesture
{
    public virtual GestureStage Stage => GestureStage.BeforeCursor;

    public virtual bool EndsWithReferencePick => true;

    public abstract void Move(Point point, KeyModifiers modifiers);

    public virtual void Release()
    {
    }
}
