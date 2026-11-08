namespace PulseOffice.Agent.Hosting;

public readonly record struct AgentPicture(int Width, int Height, double Scale);

public interface IAgentPictureFrame
{
    AgentPicture Fit(double pageWidth, double pageHeight, int width);
}

public sealed class AgentPictureFrame : IAgentPictureFrame
{
    public const int MaximumHeight = 6000;

    public AgentPicture Fit(double pageWidth, double pageHeight, int width)
    {
        var requested = Math.Max(1, width);
        if (!(double.IsFinite(pageWidth) && pageWidth > 0 && double.IsFinite(pageHeight) && pageHeight > 0))
            return new AgentPicture(requested, requested, 1);

        var scale = Math.Min(requested / pageWidth, MaximumHeight / pageHeight);
        var fittedWidth = Math.Clamp((int)Math.Round(pageWidth * scale), 1, requested);
        var fittedHeight = Math.Clamp((int)Math.Ceiling(pageHeight * scale), 1, MaximumHeight);
        return new AgentPicture(fittedWidth, fittedHeight, scale);
    }
}
