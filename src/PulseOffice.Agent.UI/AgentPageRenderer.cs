using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Agent.Hosting;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Printing;

namespace PulseOffice.Agent.UI;

public interface IAgentPageRenderer
{
    byte[] Render(IPrintableDocument document, int pageIndex, int width);
}

public sealed class AgentPageRenderer(IPageCanvasFactory canvases, IAgentPictureFrame frame) : IAgentPageRenderer
{
    public byte[] Render(IPrintableDocument document, int pageIndex, int width)
    {
        ArgumentNullException.ThrowIfNull(document);
        var picture = frame.Fit(document.Setup.PageWidth.ToPoints(), document.Setup.PageHeight.ToPoints(), width);

        using var bitmap = new RenderTargetBitmap(new PixelSize(picture.Width, picture.Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, picture.Width, picture.Height));
            document.PaintPage(pageIndex, canvases.Create(context, new DocumentSize(document.Setup.PageWidth, document.Setup.PageHeight), picture.Scale));
        }

        using var buffer = new MemoryStream();
        bitmap.Save(buffer, PngBitmapEncoderOptions.Default);
        return buffer.ToArray();
    }
}
