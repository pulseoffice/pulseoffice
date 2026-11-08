namespace PulseOffice.Presentations;

public enum CanvasPressTarget { Empty, Element, Editor }

public enum CanvasMode { Normal, Drawing, Points, Glue, Tool }

public sealed record CanvasPress(CanvasPressTarget Target, CanvasMode Mode);

public interface ICanvasContextChooser
{
    string? RootFor(CanvasPress press);
}

public sealed class CanvasContextChooser : ICanvasContextChooser
{
    public const string Object = "object";
    public const string Text = "text";
    public const string Canvas = "canvas";
    public const string Points = "points";

    public string? RootFor(CanvasPress press)
    {
        ArgumentNullException.ThrowIfNull(press);

        return press.Mode switch
        {
            CanvasMode.Points => Points,
            CanvasMode.Normal => press.Target switch
            {
                CanvasPressTarget.Editor => Text,
                CanvasPressTarget.Element => Object,
                _ => Canvas,
            },
            _ => null,
        };
    }
}
