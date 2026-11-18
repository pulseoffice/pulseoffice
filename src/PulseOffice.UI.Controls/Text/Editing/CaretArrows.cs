using Avalonia;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Editing;

public enum CaretArrow
{
    Up,
    Down,
    Left,
    Right,
}

public interface ICaretArrows
{
    CaretArrow InFrame(Matrix placement, CaretArrow screen);

    CaretMove Move(CaretArrow frame, bool stacked);
}

public sealed class CaretArrows : ICaretArrows
{
    public CaretArrow InFrame(Matrix placement, CaretArrow screen)
    {
        if (placement.IsIdentity || !placement.TryInvert(out var inverse)) return screen;
        var direction = screen switch
        {
            CaretArrow.Up => new Point(0, -1),
            CaretArrow.Down => new Point(0, 1),
            CaretArrow.Left => new Point(-1, 0),
            _ => new Point(1, 0),
        };
        var turned = inverse.Transform(direction) - inverse.Transform(new Point(0, 0));
        return Math.Abs(turned.X) >= Math.Abs(turned.Y)
            ? turned.X < 0 ? CaretArrow.Left : CaretArrow.Right
            : turned.Y < 0 ? CaretArrow.Up : CaretArrow.Down;
    }

    public CaretMove Move(CaretArrow frame, bool stacked) => (frame, stacked) switch
    {
        (CaretArrow.Up, true) => CaretMove.Previous,
        (CaretArrow.Down, true) => CaretMove.Next,
        (CaretArrow.Left, true) => CaretMove.Up,
        (CaretArrow.Right, true) => CaretMove.Down,
        (CaretArrow.Up, false) => CaretMove.Up,
        (CaretArrow.Down, false) => CaretMove.Down,
        (CaretArrow.Left, false) => CaretMove.Previous,
        _ => CaretMove.Next,
    };
}
