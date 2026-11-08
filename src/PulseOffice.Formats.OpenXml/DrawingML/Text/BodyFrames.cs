using DocumentFormat.OpenXml;
using D = DocumentFormat.OpenXml.Drawing;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingTextAnchors
{
    private readonly IReadOnlyList<(string Token, TextAnchor Anchor)> _table =
    [
        ("t", TextAnchor.Top),
        ("ctr", TextAnchor.Middle),
        ("b", TextAnchor.Bottom),
        ("just", TextAnchor.Justified),
        ("dist", TextAnchor.Justified),
    ];

    public TextAnchor? Of(string? token) =>
        _table.FirstOrDefault(entry => entry.Token == token) is { Token: not null } found ? found.Anchor : null;

    public D.TextAnchoringTypeValues Value(TextAnchor anchor) => anchor switch
    {
        TextAnchor.Middle => D.TextAnchoringTypeValues.Center,
        TextAnchor.Bottom => D.TextAnchoringTypeValues.Bottom,
        TextAnchor.Justified => new D.TextAnchoringTypeValues("just"),
        _ => D.TextAnchoringTypeValues.Top,
    };
}

internal sealed class BodyFrameReader(DrawingTextAnchors anchors)
{

    public TextFrameLayer Read(D.BodyProperties? body)
    {
        if (body is null) return TextFrameLayer.Empty;

        var normal = body.GetFirstChild<D.NormalAutoFit>();
        TextAutofit? autofit = normal is not null ? TextAutofit.ShrinkText
            : body.GetFirstChild<D.ShapeAutoFit>() is not null ? TextAutofit.ResizeShape
            : body.GetFirstChild<D.NoAutoFit>() is not null ? TextAutofit.None
            : null;

        return new TextFrameLayer
        {
            Anchor = anchors.Of(Attribute(body, "anchor")),
            Left = Points(Attribute(body, "lIns")),
            Top = Points(Attribute(body, "tIns")),
            Right = Points(Attribute(body, "rIns")),
            Bottom = Points(Attribute(body, "bIns")),
            Autofit = autofit,
            FontScale = normal is null ? null : Fraction(Attribute(normal, "fontScale")),
            LineReduction = normal is null ? null : Fraction(Attribute(normal, "lnSpcReduction")),
            Wrap = Attribute(body, "wrap") switch { "none" => false, "square" => true, _ => null },
        };
    }

    public TextFrame Resolve(IEnumerable<OpenXmlElement?> bodies, TextFrame fallback) =>
        bodies.Select(body => Read(body as D.BodyProperties))
            .Aggregate(TextFrameLayer.Empty, (style, lower) => style.Over(lower))
            .ToFrame(fallback);

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(a => a.LocalName == name && string.IsNullOrEmpty(a.NamespaceUri)).Value;

    private double? Fraction(string? text)
    {
        if (text is null) return null;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        double? value = text.EndsWith('%')
            ? double.TryParse(text[..^1], System.Globalization.NumberStyles.Float, invariant, out var percent) ? percent / 100.0 : null
            : double.TryParse(text, System.Globalization.NumberStyles.Float, invariant, out var thousandths) ? thousandths / 100000.0 : null;
        return value is { } fraction && double.IsFinite(fraction) ? Math.Clamp(fraction, 0.0, 1.0) : null;
    }

    private double? Points(string? emu) =>
        long.TryParse(emu, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value / ((double)PulseOffice.Foundation.Units.Emu.PerPoint) : null;
}

internal sealed class BodyPropertiesBuilder(DrawingTextAnchors anchors)
{

    public D.BodyProperties Build(TextFrame frame)
    {
        var body = new D.BodyProperties
        {
            LeftInset = Emu(frame.Insets.Left),
            TopInset = Emu(frame.Insets.Top),
            RightInset = Emu(frame.Insets.Right),
            BottomInset = Emu(frame.Insets.Bottom),
            Anchor = anchors.Value(frame.Anchor),
            Wrap = frame.WordWrap ? D.TextWrappingValues.Square : D.TextWrappingValues.None,
        };

        body.Append(frame.Autofit switch
        {
            TextAutofit.ShrinkText => Shrink(frame),
            TextAutofit.ResizeShape => new D.ShapeAutoFit(),
            _ => new D.NoAutoFit(),
        });
        return body;
    }

    private D.NormalAutoFit Shrink(TextFrame frame)
    {
        var shrink = new D.NormalAutoFit();
        if (frame.FontScale < 0.9999) shrink.FontScale = (int)Math.Round(frame.FontScale * 100000);
        if (frame.LineReduction > 0.0001) shrink.LineSpaceReduction = (int)Math.Round(frame.LineReduction * 100000);
        return shrink;
    }

    private int Emu(double points) => (int)Math.Round(points * ((double)PulseOffice.Foundation.Units.Emu.PerPoint));
}
