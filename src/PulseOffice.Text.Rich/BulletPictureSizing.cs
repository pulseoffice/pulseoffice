namespace PulseOffice.Text.Rich;

public interface IBulletPictureSizing
{
    ListBulletPicture Size(ListBulletPicture picture, double fontSize);
}

public sealed class BulletPictureSizing : IBulletPictureSizing
{
    private const double SmallestSide = 1;

    private const double LargestSide = 144;

    public ListBulletPicture Size(ListBulletPicture picture, double fontSize)
    {
        var width = Usable(picture.Width) ? picture.Width : fontSize;
        var height = Usable(picture.Height) ? picture.Height : fontSize;
        if (picture.ScalesWithFont && Usable(fontSize) && Usable(height))
        {
            var scale = fontSize / height;
            width *= scale;
            height = fontSize;
        }
        return picture with { Width = Clamped(width), Height = Clamped(height) };
    }

    private double Clamped(double side) => double.IsFinite(side) ? Math.Clamp(side, SmallestSide, LargestSide) : SmallestSide;

    private bool Usable(double value) => double.IsFinite(value) && value > 0;
}
