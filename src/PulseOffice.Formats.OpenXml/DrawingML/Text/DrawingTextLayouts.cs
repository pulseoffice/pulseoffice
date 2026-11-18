using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingTextLayouts
{

    private readonly IReadOnlyList<(string Token, TextFlow Flow)> _flows =
    [
        ("horz", TextFlow.Horizontal),
        ("vert", TextFlow.Vertical),
        ("eaVert", TextFlow.Vertical),
        ("mongolianVert", TextFlow.Vertical),
        ("vert270", TextFlow.Vertical270),
        ("wordArtVert", TextFlow.Stacked),
        ("wordArtVertRtl", TextFlow.Stacked),
    ];

    public TextLayout? Read(OpenXmlElement? body)
    {
        if (body is null) return null;
        var vertical = Attribute(body, "vert");
        var upright = Attribute(body, "upright");
        var columns = Attribute(body, "numCol");
        var spacing = Attribute(body, "spcCol");
        if (vertical is null && upright is null && columns is null && spacing is null) return null;

        return new TextLayout
        {
            Flow = _flows.FirstOrDefault(entry => entry.Token == vertical) is { Token: not null } found ? found.Flow : TextFlow.Horizontal,
            Upright = upright is "1" or "true",
            Columns = int.TryParse(columns, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ? Math.Clamp(count, 1, 16) : 1,
            ColumnSpacing = long.TryParse(spacing, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu) ? Math.Max(0, emu / (double)PulseOffice.Foundation.Units.Emu.PerPoint) : 0,
        };
    }

    public double? Rotation(OpenXmlElement? body) =>
        body is not null && int.TryParse(Attribute(body, "rot"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var angle) ? angle / 60000.0 : null;

    public void Write(D.BodyProperties body, TextLayout layout, double rotation)
    {
        if (Math.Abs(rotation) > 0.001) body.Rotation = (int)Math.Round(rotation * 60000);
        if (layout.Flow != TextFlow.Horizontal)
            body.Vertical = new D.TextVerticalValues(layout.Flow switch
            {
                TextFlow.Vertical270 => "vert270",
                TextFlow.Stacked => "wordArtVert",
                _ => "vert",
            });
        if (layout.Upright) body.UpRight = true;
        if (layout.Columns > 1) body.ColumnCount = layout.Columns;
        if (layout.ColumnSpacing > 0) body.ColumnSpacing = (int)Math.Round(layout.ColumnSpacing * PulseOffice.Foundation.Units.Emu.PerPoint);
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value;
}
