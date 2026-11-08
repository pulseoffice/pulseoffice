using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal interface IDrawingRunLinks
{
    string? Read(D.HyperlinkOnClick? link);

    D.HyperlinkOnClick? Build(string target);
}
