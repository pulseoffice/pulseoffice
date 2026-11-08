namespace PulseOffice.UI.Controls.Zoom;

public interface IZoomLadder
{
    double StepIn(double factor);

    double StepOut(double factor);
}

public sealed class ZoomLadder : IZoomLadder
{
    private readonly double[] _steps = [0.1, 0.25, 0.33, 0.5, 0.67, 0.75, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5];

    public double StepIn(double factor) => _steps.FirstOrDefault(step => step > factor + 0.001, _steps[^1]);

    public double StepOut(double factor) => _steps.LastOrDefault(step => step < factor - 0.001, _steps[0]);
}
