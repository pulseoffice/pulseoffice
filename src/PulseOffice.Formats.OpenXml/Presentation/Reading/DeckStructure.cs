using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxThemeReader(IThemeCatalog themes, OoxmlValues values, ColorMapCodec maps)
{
    public Theme Read(PresentationPart presentation) =>
        presentation.SlideMasterParts.FirstOrDefault() is { } master ? Read(master) : Read(presentation.ThemePart, OoxmlColorMap.Standard);

    public Theme Read(SlideMasterPart master) =>
        Read(master.ThemePart, maps.Read(master.SlideMaster?.ColorMap, OoxmlColorMap.Standard));

    public Theme Read(ThemePart? part, OoxmlColorMap map)
    {
        var elements = part?.Theme?.ThemeElements;
        if (elements is null) return themes.Default;

        var scheme = elements.ColorScheme;
        var fonts = elements.FontScheme;
        uint Get(D.Color2Type? colour, uint fallback) =>
            colour?.RgbColorModelHex?.Val?.Value is { } hex ? values.Color(hex)
            : colour?.SystemColor?.LastColor?.Value is { } last ? values.Color(last)
            : fallback;

        var read = new Theme
        {
            Name = (part?.Theme?.Name?.Value ?? (elements.Parent as D.Theme)?.Name?.Value) is { Length: > 0 } name
                ? name
                : elements.ColorScheme?.Name?.Value is { Length: > 0 } schemeName ? schemeName : themes.Default.Name,
            Colors = maps.Logical(new ColorScheme
            {
                Text = Get(scheme?.Dark1Color, 0xFF1A1A1A),
                Background = Get(scheme?.Light1Color, 0xFFFFFFFF),
                Text2 = Get(scheme?.Dark2Color, 0xFF44506A),
                Background2 = Get(scheme?.Light2Color, 0xFFF2F4F7),
                Accent1 = Get(scheme?.Accent1Color, 0xFF2563EB),
                Accent2 = Get(scheme?.Accent2Color, 0xFF16A34A),
                Accent3 = Get(scheme?.Accent3Color, 0xFFDC2626),
                Accent4 = Get(scheme?.Accent4Color, 0xFFEA580C),
                Accent5 = Get(scheme?.Accent5Color, 0xFF7C3AED),
                Accent6 = Get(scheme?.Accent6Color, 0xFF0891B2),
                Hyperlink = Get(scheme?.Hyperlink, 0xFF2563EB),
                FollowedHyperlink = Get(scheme?.FollowedHyperlinkColor, 0xFF7C3AED),
            }, map),
            Fonts = new FontScheme
            {
                Major = fonts?.MajorFont?.LatinFont?.Typeface?.Value ?? "Inter",
                Minor = fonts?.MinorFont?.LatinFont?.Typeface?.Value ?? "Inter",
            },
        };
        return themes.Find(read.Name) is { } known && known.Colors == read.Colors && known.Fonts == read.Fonts
            ? known
            : read;
    }
}

internal sealed class PptxMasterReader(
    IMasterFactory masters, OoxmlValues values, ShapeClassifier shapes, SchemeRoles schemes,
    PlaceholderInheritance inheritance, BodyFrameReader frames, ColorMapCodec maps, PptxDeckShapeMapper decorations, PptxBackgroundFillReader backgrounds, DrawingShapeColourNormalizer normalizer,
    PptxLayoutIdentity identities)
{
    private const int MinimumSlideEmu = 914400;
    private const int MaximumSlideEmu = 51206400;

    public SlideSize Size(P.Presentation presentation) =>
        presentation.SlideSize is { Cx.Value: > 0 and var cx, Cy.Value: > 0 and var cy }
            ? new SlideSize(values.Twips(Math.Clamp(cx, MinimumSlideEmu, MaximumSlideEmu)), values.Twips(Math.Clamp(cy, MinimumSlideEmu, MaximumSlideEmu)))
            : SlideSize.Widescreen;

    public SlideMaster Read(PresentationPart presentation, Theme theme, SlideSize size) =>
        presentation.SlideMasterParts.FirstOrDefault() is { } masterPart ? Read(masterPart, theme, size) : masters.CreateStandard(theme, size);

    public SlideMaster Read(SlideMasterPart masterPart, Theme theme, SlideSize size)
    {
        var standard = masters.CreateStandard(theme, size);
        var masterMap = maps.Read(masterPart.SlideMaster?.ColorMap, OoxmlColorMap.Standard);
        using var scope = schemes.Enter(masterMap, masterMap, theme.Colors);

        var byKind = standard.Layouts.ToDictionary(layout => layout.Kind);
        var custom = new List<SlideLayoutTemplate>();

        foreach (var (layoutPart, kind, customId) in identities.Entries(masterPart))
        {
            if (!byKind.TryGetValue(kind, out var template)) continue;
            if (customId is not null)
                template = standard.Template(kind) with
                {
                    Id = customId,
                    Name = layoutPart.SlideLayout?.CommonSlideData?.Name?.Value is { Length: > 0 } customName ? customName : template.Name,
                };
            using var layoutScope = schemes.Enter(masterMap, maps.Override(masterMap, layoutPart.SlideLayout?.ColorMapOverride));

            var tree = layoutPart.SlideLayout?.CommonSlideData?.ShapeTree;
            normalizer.Normalize(tree, masterPart.ThemePart, () => backgrounds.FillElement(
                layoutPart.SlideLayout?.CommonSlideData?.Background ?? masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart));
            var placeholders = (tree?.Elements<P.Shape>() ?? [])
                .Where(shapes.IsPlaceholder)
                .Select(shape => Placeholder(shape, masterPart, template, theme))
                .ToList();

            var read = template with
            {
                Placeholders = placeholders.Count > 0 ? placeholders : template.Placeholders,
                Background = schemes.Background(layoutPart.SlideLayout?.CommonSlideData?.Background),
                Objects = Decoration(tree, customId is null ? $"deco.{kind}." : $"deco.{customId}."),
                HideMasterObjects = layoutPart.SlideLayout?.ShowMasterShapes?.Value == false,
            };
            if (customId is null) byKind[kind] = read;
            else custom.Add(read);
        }

        normalizer.Normalize(masterPart.SlideMaster?.CommonSlideData?.ShapeTree, masterPart.ThemePart,
            () => backgrounds.FillElement(masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart));
        return standard with
        {
            Objects = Decoration(masterPart.SlideMaster?.CommonSlideData?.ShapeTree, "deco.m"),
            Background = schemes.Background(masterPart.SlideMaster?.CommonSlideData?.Background) ?? standard.Background,
            BackgroundColor = Modified(masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart)
                ?? schemes.BackgroundColor(masterPart.SlideMaster?.CommonSlideData?.Background)
                ?? backgrounds.Solid(masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart),
            BackgroundImage = BackgroundImage(masterPart),
            BackgroundLayout = (masterPart.SlideMaster?.CommonSlideData?.Background?.BackgroundProperties?.GetFirstChild<D.BlipFill>()
                    ?? backgrounds.FillElement(masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart) as D.BlipFill)?.GetFirstChild<D.Tile>() is not null
                ? new BackgroundImageLayout(BackgroundImageMode.Tile, PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero)
                : BackgroundImageLayout.Stretched,
            BackgroundFill = backgrounds.Read(masterPart.SlideMaster?.CommonSlideData?.Background, masterPart.ThemePart),
            Layouts = [.. standard.Layouts.Select(layout => byKind[layout.Kind]), .. custom],
            Name = masterPart.SlideMaster?.CommonSlideData?.Name?.Value is { Length: > 0 } name ? name : standard.Name,
            Elements = ElementsOf(masterPart),
        };
    }

    private uint? Modified(P.Background? background, ThemePart? theme) =>
        background?.BackgroundProperties?.GetFirstChild<D.SolidFill>()?.ChildElements.FirstOrDefault() is { HasChildren: true }
            ? backgrounds.Colour(background, theme)
            : null;

    private PulseOffice.Drawing.Assets.RasterImageObject? BackgroundImage(SlideMasterPart masterPart)
    {
        var background = masterPart.SlideMaster?.CommonSlideData?.Background;
        OpenXmlPart? owner = masterPart;
        var blip = background?.BackgroundProperties?.GetFirstChild<D.BlipFill>();
        if (blip is null && background?.BackgroundProperties is null && backgrounds.FillElement(background, masterPart.ThemePart) is D.BlipFill themed)
        {
            blip = themed;
            owner = masterPart.ThemePart;
        }
        if (blip?.Blip?.Embed?.Value is not { } embed || owner is null
            || !owner.TryGetPartById(embed, out var found) || found is not ImagePart part) return null;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = "master.bg",
            Data = buffer.ToArray(),
            MediaType = part.ContentType,
            Duotone = backgrounds.Duotone(blip.Blip),
        };
    }

    private List<SlideElement> Decoration(P.ShapeTree? tree, string prefix) =>
        [.. (tree?.Elements<P.Shape>() ?? [])
            .Where(shape => !shapes.IsPlaceholder(shape))
            .Select((shape, index) => decorations.ReadShape(shape, $"{prefix}{index + 1}"))
            .OfType<ObjectElement>()
            .Select((element, index) => element with { ZOrder = index })];

    private MasterElements ElementsOf(SlideMasterPart masterPart)
    {
        var types = (masterPart.SlideMaster?.CommonSlideData?.ShapeTree?.Elements<P.Shape>() ?? [])
            .Select(shape => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape?.Type?.Value)
            .OfType<P.PlaceholderValues>()
            .ToList();
        if (types.Count == 0) return MasterElements.All;
        return new MasterElements(
            types.Contains(P.PlaceholderValues.DateAndTime),
            types.Contains(P.PlaceholderValues.Footer),
            types.Contains(P.PlaceholderValues.SlideNumber));
    }

    private PlaceholderTemplate Placeholder(P.Shape shape, SlideMasterPart masterPart, SlideLayoutTemplate template, Theme theme)
    {
        var role = shapes.Role(shape);
        var standard = template.Placeholders.FirstOrDefault(p => p.Role == role);
        var fallback = standard?.Seed ?? new TextParagraph(string.Empty);
        var chain = inheritance.ForLayout(shape, masterPart);
        return new PlaceholderTemplate(role, inheritance.Bounds(chain), Seed(chain.Style.At(0), fallback, theme))
        {
            Frame = frames.Resolve(chain.Shapes.Select(link => link.TextBody?.BodyProperties), TextFrame.Default),
        };
    }

    private TextParagraph Seed(LevelStyle style, TextParagraph fallback, Theme theme)
    {
        var seed = fallback with
        {
            FontSize = style.FontSize ?? fallback.FontSize,
            Bold = style.Bold ?? fallback.Bold,
            Italic = style.Italic ?? fallback.Italic,
            Align = style.Align ?? fallback.Align,
            Bullet = style.Numbered != true && (style.Bullet ?? fallback.Bullet),
            MarginLeft = style.MarginLeft ?? fallback.MarginLeft,
            Indent = style.Indent ?? fallback.Indent,
        };

        if (style.Color is { } argb) seed = seed with { Color = argb, ColorRole = null };
        else if (style.ColorRole is { } role) seed = seed with { ColorRole = role, Color = null };

        if (style.Latin is { } font)
            seed = seed with { MajorFont = IsMajor(font, theme, fallback.MajorFont) };

        return seed;
    }

    private bool IsMajor(string font, Theme theme, bool fallback) => font switch
    {
        "+mj-lt" => true,
        "+mn-lt" => false,
        _ when !string.Equals(theme.Fonts.Major, theme.Fonts.Minor, StringComparison.OrdinalIgnoreCase)
            => string.Equals(font, theme.Fonts.Major, StringComparison.OrdinalIgnoreCase),
        _ => fallback,
    };
}
