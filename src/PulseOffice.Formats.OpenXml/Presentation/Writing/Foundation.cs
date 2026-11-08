using PulseOffice.Foundation.Colors;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlWriteUnits(SchemeRoles schemes)
{

    public long Emu(Twip value) => value.Value * PulseOffice.Foundation.Units.Emu.PerTwip;

    public string Hex(uint argb) => new ArgbColor(argb).ToRgbDigits();

    public D.TextAlignmentTypeValues Align(TextAlign align) => align switch
    {
        TextAlign.Center => D.TextAlignmentTypeValues.Center,
        TextAlign.Right => D.TextAlignmentTypeValues.Right,
        TextAlign.Justify => D.TextAlignmentTypeValues.Justified,
        _ => D.TextAlignmentTypeValues.Left,
    };

    public D.SchemeColorValues Scheme(ColorRole role) => schemes.SchemeOf(role);

    public P.Background Background(uint argb) =>
        new(new P.BackgroundProperties(new D.SolidFill(new D.RgbColorModelHex { Val = new ArgbColor(argb).ToRgbDigits() }), new D.EffectList()));

    public P.Background Background(ColorRole role) =>
        new(new P.BackgroundProperties(new D.SolidFill(new D.SchemeColor { Val = Scheme(role) }), new D.EffectList()));

    public P.PlaceholderValues Placeholder(PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title => P.PlaceholderValues.Title,
        PlaceholderRole.Subtitle => P.PlaceholderValues.SubTitle,
        PlaceholderRole.Picture => P.PlaceholderValues.Picture,
        _ => P.PlaceholderValues.Body,
    };

    public D.Transform2D Transform(DocumentRect bounds) =>
        new(new D.Offset { X = Emu(bounds.Left), Y = Emu(bounds.Top) },
            new D.Extents { Cx = Emu(bounds.Width), Cy = Emu(bounds.Height) });
}

internal sealed class ShapeTreeFactory
{
    public P.ShapeTree Empty() =>
        new(new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new D.TransformGroup()));

    public P.Shape Placeholder(uint id, PlaceholderRole role, P.PlaceholderValues type, D.Transform2D transform, P.TextBody body) =>
        new(new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = role.ToString() },
                new P.NonVisualShapeDrawingProperties(new D.ShapeLocks { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape { Type = type })),
            new P.ShapeProperties(transform, new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }),
            body);

    public P.Shape TextBox(uint id, D.Transform2D transform, P.TextBody body) =>
        new(new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "TextBox " + id },
                new P.NonVisualShapeDrawingProperties { TextBox = true },
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(transform, new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle }, new D.NoFill()),
            body);
}

internal sealed class PptxWriteSession(PresentationDocument document, PresentationPart part, PulseOffice.Presentations.Presentation deck)
{
    public PresentationDocument Document => document;

    public PresentationPart Part => part;

    public PulseOffice.Presentations.Presentation Deck => deck;

    public Dictionary<string, string> RelationshipBySlide { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, uint> NumberBySlide { get; } = new(StringComparer.Ordinal);

    public List<(SlidePart Source, D.HyperlinkOnClick Link, string Target)> PendingSlideLinks { get; } = [];

    public Dictionary<string, MediaDataPart> SoundParts { get; } = new(StringComparer.Ordinal);
}
