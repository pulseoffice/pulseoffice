using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml;

internal sealed class OoxmlValues
{

    public Twip Twips(long emu) => new((int)Math.Clamp(emu / PulseOffice.Foundation.Units.Emu.PerTwip, int.MinValue, int.MaxValue));

    public DocumentRect Bounds(P.Shape shape)
    {
        var transform = shape.ShapeProperties?.Transform2D;
        if (transform?.Offset is not { } offset || transform.Extents is not { } extents) return default;

        return new DocumentRect(
            Twips(offset.X?.Value ?? 0), Twips(offset.Y?.Value ?? 0),
            Twips(extents.Cx?.Value ?? 0), Twips(extents.Cy?.Value ?? 0));
    }

    public uint Color(string hex) =>
        0xFF000000 | (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0u);

    public TextAlign Align(D.TextAlignmentTypeValues? value, TextAlign fallback = TextAlign.Left)
    {
        if (value == D.TextAlignmentTypeValues.Center) return TextAlign.Center;
        if (value == D.TextAlignmentTypeValues.Right) return TextAlign.Right;
        if (value == D.TextAlignmentTypeValues.Justified) return TextAlign.Justify;
        if (value == D.TextAlignmentTypeValues.Left) return TextAlign.Left;
        return fallback;
    }
}

internal sealed class SchemeRoles
{
    private readonly IReadOnlyList<(D.SchemeColorValues[] Schemes, ColorRole Role)> _table =
    [
        ([D.SchemeColorValues.Background1, D.SchemeColorValues.Light1], ColorRole.Background),
        ([D.SchemeColorValues.Text1, D.SchemeColorValues.Dark1], ColorRole.Text),
        ([D.SchemeColorValues.Background2, D.SchemeColorValues.Light2], ColorRole.Background2),
        ([D.SchemeColorValues.Text2, D.SchemeColorValues.Dark2], ColorRole.Text2),
        ([D.SchemeColorValues.Accent1], ColorRole.Accent1),
        ([D.SchemeColorValues.Accent2], ColorRole.Accent2),
        ([D.SchemeColorValues.Accent3], ColorRole.Accent3),
        ([D.SchemeColorValues.Accent4], ColorRole.Accent4),
        ([D.SchemeColorValues.Accent5], ColorRole.Accent5),
        ([D.SchemeColorValues.Accent6], ColorRole.Accent6),
        ([D.SchemeColorValues.Hyperlink], ColorRole.Hyperlink),
        ([D.SchemeColorValues.FollowedHyperlink], ColorRole.FollowedHyperlink),
    ];

    private OoxmlColorMap _master = OoxmlColorMap.Standard;
    private OoxmlColorMap _effective = OoxmlColorMap.Standard;
    private ColorScheme? _colors;

    public IDisposable Enter(OoxmlColorMap master, OoxmlColorMap effective, ColorScheme? colors = null)
    {
        var scope = new MappingScope(this, _master, _effective, _colors);
        (_master, _effective) = (master, effective);
        if (colors is not null) _colors = colors;
        return scope;
    }

    public uint? Absolute(D.SchemeColorValues? scheme) =>
        _colors is { } colors && Of(scheme) is { } role ? colors.Resolve(role) : null;

    public ColorRole? Of(D.SchemeColorValues? scheme)
    {
        if (scheme is not { } value) return null;
        if (value == D.SchemeColorValues.Background1) return _master.RoleOf(_effective.Background1);
        if (value == D.SchemeColorValues.Text1) return _master.RoleOf(_effective.Text1);
        if (value == D.SchemeColorValues.Background2) return _master.RoleOf(_effective.Background2);
        if (value == D.SchemeColorValues.Text2) return _master.RoleOf(_effective.Text2);
        if (value == D.SchemeColorValues.Dark1) return _master.RoleOf(ThemeSlot.Dark1);
        if (value == D.SchemeColorValues.Light1) return _master.RoleOf(ThemeSlot.Light1);
        if (value == D.SchemeColorValues.Dark2) return _master.RoleOf(ThemeSlot.Dark2);
        if (value == D.SchemeColorValues.Light2) return _master.RoleOf(ThemeSlot.Light2);
        return _table.FirstOrDefault(entry => entry.Schemes.Contains(value)) is { Schemes: not null } found ? found.Role : null;
    }

    private sealed class MappingScope(SchemeRoles owner, OoxmlColorMap master, OoxmlColorMap effective, ColorScheme? colors) : IDisposable
    {
        public void Dispose() => (owner._master, owner._effective, owner._colors) = (master, effective, colors);
    }

    public D.SchemeColorValues SchemeOf(ColorRole role) =>
        _table.FirstOrDefault(entry => entry.Role == role) is { Schemes: not null } found
            ? found.Schemes[0]
            : D.SchemeColorValues.Background1;

    public uint? BackgroundColor(P.Background? background) =>
        ArgbColor.FromRgbHex(background?.BackgroundProperties?.GetFirstChild<D.SolidFill>()?.RgbColorModelHex?.Val?.Value)?.Value;

    public ColorRole? Background(P.Background? background) =>
        Of(background?.BackgroundProperties?.GetFirstChild<D.SolidFill>()?.SchemeColor?.Val?.Value);
}

internal enum ShapeClass { Text, Shape, Freeform, Skip }

internal sealed class ShapeClassifier
{
    public ShapeClass Classify(P.Shape shape)
    {
        var name = Name(shape);
        if (name is "DeckShape" or DeckSceneCodec.Name) return ShapeClass.Shape;
        if (name == "DeckFormula") return ShapeClass.Skip;
        if (shape.TextBody?.BodyProperties?.GetFirstChild<D.PresetTextWarp>()?.Preset?.InnerText is { } warp && warp != "textNoShape") return ShapeClass.Shape;
        if (IsChrome(shape)) return ShapeClass.Skip;
        if (IsPlaceholder(shape)) return ShapeClass.Text;

        var preset = shape.ShapeProperties?.GetFirstChild<D.PresetGeometry>()?.Preset?.InnerText;
        if (preset is not null and not "rect") return ShapeClass.Shape;
        if (preset is null && shape.ShapeProperties?.GetFirstChild<D.CustomGeometry>() is not null && !HasText(shape)) return Painted(shape) || shape.ShapeStyle?.LineReference?.Index?.Value is > 0 ? ShapeClass.Freeform : ShapeClass.Skip;
        if (Painted(shape)) return HasText(shape) ? ShapeClass.Text : ShapeClass.Shape;

        return shape.TextBody is not null ? ShapeClass.Text : ShapeClass.Skip;
    }

    private bool HasText(P.Shape shape) =>
        shape.TextBody?.Descendants<D.Text>().Any(text => !string.IsNullOrWhiteSpace(text.Text)) == true;

    private bool Painted(P.Shape shape)
    {
        var properties = shape.ShapeProperties;
        if (properties is not null
            && (properties.GetFirstChild<D.SolidFill>() is not null
                || properties.GetFirstChild<D.GradientFill>() is not null
                || properties.GetFirstChild<D.PatternFill>() is not null
                || properties.GetFirstChild<D.BlipFill>() is not null))
            return true;

        if (properties?.GetFirstChild<D.Outline>() is { } outline && outline.GetFirstChild<D.NoFill>() is null && outline.HasChildren)
            return true;

        return shape.ShapeStyle?.FillReference?.Index?.Value is > 0;
    }

    public string? Name(P.Shape shape) => shape.NonVisualShapeProperties?.NonVisualDrawingProperties?.Name?.Value;

    public bool IsChrome(P.Shape shape) =>
        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>()?.Type?.Value is { } type
        && (type == P.PlaceholderValues.DateAndTime || type == P.PlaceholderValues.Footer || type == P.PlaceholderValues.SlideNumber);

    public bool IsPlaceholder(P.Shape shape) =>
        shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.GetFirstChild<P.PlaceholderShape>() is not null;

    public PlaceholderRole Role(P.Shape shape)
    {
        var type = shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?
            .GetFirstChild<P.PlaceholderShape>()?.Type?.Value;

        if (type == P.PlaceholderValues.Title || type == P.PlaceholderValues.CenteredTitle) return PlaceholderRole.Title;
        if (type == P.PlaceholderValues.SubTitle) return PlaceholderRole.Subtitle;
        if (type == P.PlaceholderValues.Picture) return PlaceholderRole.Picture;
        return IsPlaceholder(shape) ? PlaceholderRole.Body : PlaceholderRole.Free;
    }

    public IReadOnlyList<PlaceholderRole> Roles(IReadOnlyList<P.Shape> shapes) => [.. shapes.Select(Role)];

    public SlideLayout Infer(IReadOnlyList<PlaceholderRole> roles)
    {
        var bodies = roles.Count(r => r == PlaceholderRole.Body);
        if (bodies >= 2) return SlideLayout.TwoContent;
        if (bodies == 1) return SlideLayout.TitleAndContent;
        return roles.Any(role => role != PlaceholderRole.Free) ? SlideLayout.TitleSlide : SlideLayout.Blank;
    }
}
