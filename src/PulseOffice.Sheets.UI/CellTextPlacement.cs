using PulseOffice.Sheets.Styling;

namespace PulseOffice.Sheets.UI;

public readonly record struct CellBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public interface ICellTextPlacement
{
    double Top(CellBox cell, double textHeight, CellVerticalAlignment vertical, double margin);

    (double CentreX, double CentreY) RotatedCentre(
        CellBox cell, double textWidth, double textHeight, double degrees,
        bool alignRight, bool centred, CellVerticalAlignment vertical, double padding);
}

public sealed class CellTextPlacement : ICellTextPlacement
{
    public double Top(CellBox cell, double textHeight, CellVerticalAlignment vertical, double margin) => vertical switch
    {
        CellVerticalAlignment.Top => cell.Y + margin,
        CellVerticalAlignment.Bottom => cell.Bottom - textHeight - margin,
        _ => cell.Y + (cell.Height - textHeight) / 2,
    };

    public (double CentreX, double CentreY) RotatedCentre(
        CellBox cell, double textWidth, double textHeight, double degrees,
        bool alignRight, bool centred, CellVerticalAlignment vertical, double padding)
    {
        var radians = degrees * Math.PI / 180;
        var cos = Math.Abs(Math.Cos(radians));
        var sin = Math.Abs(Math.Sin(radians));
        var width = textWidth * cos + textHeight * sin;
        var height = textWidth * sin + textHeight * cos;

        var x = centred
            ? cell.X + cell.Width / 2
            : alignRight
                ? cell.Right - padding - width / 2
                : cell.X + padding + width / 2;

        var y = vertical switch
        {
            CellVerticalAlignment.Top => cell.Y + 1 + height / 2,
            CellVerticalAlignment.Bottom => cell.Bottom - 1 - height / 2,
            _ => cell.Y + cell.Height / 2,
        };

        return (x, y);
    }
}
