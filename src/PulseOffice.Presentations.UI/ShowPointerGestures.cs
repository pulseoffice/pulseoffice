using Avalonia.Input;

namespace PulseOffice.Presentations.UI;

public enum ShowMove
{
    None,
    Forward,
    Backward,
}

public sealed class ShowPointerGestures
{
    public ShowMove ForPress(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed => ShowMove.Forward,
        PointerUpdateKind.RightButtonPressed => ShowMove.Backward,
        _ => ShowMove.None,
    };

    public ShowMove ForWheel(double deltaY) => deltaY switch
    {
        < 0 => ShowMove.Forward,
        > 0 => ShowMove.Backward,
        _ => ShowMove.None,
    };
}
