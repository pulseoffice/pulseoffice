using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class PageStyleApplier
{
    private readonly OdfHrefs _hrefs = new();

    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public void Apply(XElement page, Slide slide, OdpReadContext context)
    {
        if ((string?)page.Attribute(Draw + "style-name") is not { } name) return;

        var styles = context.Styles;
        if (styles.PageBackgrounds.TryGetValue(name, out var background)) slide.Background = background;
        if (styles.PageTransitions.TryGetValue(name, out var transition)) slide.Transition = transition;
        if (styles.HiddenPages.TryGetValue(name, out var flags))
        {
            slide.Hidden = flags.Contains("hidden", StringComparison.Ordinal);
            slide.ShowMasterBackground = !flags.Contains("noBackground", StringComparison.Ordinal);
            slide.ShowMasterObjects = !flags.Contains("noObjects", StringComparison.Ordinal);
        }

        if (styles.PageFills.TryGetValue(name, out var fill)) slide.BackgroundFill = fill;
        if (styles.PageImageLayouts.TryGetValue(name, out var layout)) slide.BackgroundLayout = layout;
        if (styles.PageImages.TryGetValue(name, out var href)
            && context.ReadBytes(_hrefs.Normalize(href)) is { } data)
            slide.BackgroundImage = new PulseOffice.Drawing.Assets.RasterImageObject
            {
                Id = $"{slide.Id}.bg",
                Data = data,
                MediaType = context.Values.MediaType(href),
                NaturalSize = new DocumentSize(Twip.Zero, Twip.Zero),
            };
    }
}

internal sealed class OdpSlideIds(IEnumerable<string> taken)
{
    private readonly HashSet<string> _taken = new(taken, StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nextSuffix = new(StringComparer.Ordinal);

    public string Claim(string declaredName)
    {
        var id = declaredName;
        var suffix = _nextSuffix.GetValueOrDefault(declaredName, 2);
        if (_taken.Contains(id))
            for (id = Suffixed(declaredName, suffix); _taken.Contains(id); id = Suffixed(declaredName, ++suffix)) { }
        _nextSuffix[declaredName] = suffix;
        _taken.Add(id);
        return id;
    }

    private string Suffixed(string name, int suffix) => $"{name} ({suffix})";
}

internal sealed class OdpSlideReader(
    ISlideLayoutRegistry layouts,
    PlaceholderRoles roles,
    TextFrameReader textFrames,
    PageStyleApplier pageStyle,
    IReadOnlyList<IOdpPageReader> contents)
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;

    private readonly OdpSectionXml _sections = new();

    public Slide Read(XElement page, OdpReadContext context, OdpSlideIds ids)
    {
        var declaredName = (string?)page.Attribute(Draw + "name") ?? "slide";
        var id = ids.Claim(declaredName);
        var assigned = roles.Assign(textFrames.Frames(page));

        var declared = (string?)page.Attribute(Presentation + "presentation-page-layout-name");
        var layout = declared is not null && layouts.TryFromOdf(declared, out var named) ? named : roles.Infer(assigned);

        var slide = new Slide(id, layout) { Name = SlideNames.NameFromPage(declaredName) };
        pageStyle.Apply(page, slide, context);
        _sections.Read(page, slide);

        var assembly = new SlideAssembly<string>(slide, assigned);
        textFrames.Read(page, assembly, context);
        foreach (var reader in contents) reader.Read(page, assembly, context);

        return slide;
    }
}

internal sealed class OdpMasterReader(IMasterFactory masters, ISlideLayoutRegistry layouts, OdfValues values, OdfTextFrameReader frames)
{
    private readonly OdfFontSizeBounds _sizes = new();
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public SlideMaster Read(XDocument? styles, Theme theme, SlideSize size) => Read(styles, theme, size, null);

    public SlideMaster Read(XDocument? styles, Theme theme, SlideSize size, string? masterName)
    {
        var standard = masters.CreateStandard(theme, size);
        if (styles?.Root?.Element(Office + "styles") is not { } root) return standard;

        var names = new OdfSeedNames();
        var prefix = masterName is null ? string.Empty : names.MasterPrefix(masterName);
        var inherited = MasterFrames(styles.Root, masterName);
        standard = standard with
        {
            Layouts = [.. standard.Layouts.Select(layout => layout with
            {
                Placeholders = [.. layout.Placeholders.Select(placeholder =>
                    inherited.TryGetValue(placeholder.Role, out var layer) ? placeholder with { Frame = layer.ToFrame(placeholder.Frame) } : placeholder)],
            })],
        };

        var byKind = standard.Layouts.ToDictionary(layout => layout.Kind);
        var seeds = root.Elements(Style + "style")
            .Where(s => (string?)s.Attribute(Style + "family") == "presentation" && s.Attribute(Style + "name") is not null)
            .GroupBy(s => (string)s.Attribute(Style + "name")!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var layout in root.Elements(Style + "presentation-page-layout").Concat(root.Elements(Presentation + "presentation-page-layout")))
        {
            var name = (string?)layout.Attribute(Style + "name");
            if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal)
                || !layouts.TryFromOdf(name[prefix.Length..], out var kind) || !byKind.TryGetValue(kind, out var template)) continue;

            var seen = new Dictionary<PlaceholderRole, int>();
            var placeholders = layout.Elements(Presentation + "placeholder")
                .Select(placeholder => Placeholder(placeholder, prefix, kind, template, seeds, theme, seen))
                .ToList();
            if (placeholders.Count > 0) byKind[kind] = template with { Placeholders = placeholders };
        }

        return standard with { Layouts = [.. standard.Layouts.Select(layout => byKind[layout.Kind])] };
    }

    public SlideSize Size(XDocument? styles)
    {
        if (styles?.Root is not { } root) return SlideSize.Widescreen;

        var pageLayouts = root.Element(Office + "automatic-styles")?.Elements(Style + "page-layout").ToList() ?? [];
        if (pageLayouts.Count == 0) return SlideSize.Widescreen;

        var masterLayout = root.Element(Office + "master-styles")?.Elements(Style + "master-page")
            .Select(m => (string?)m.Attribute(Style + "page-layout-name"))
            .FirstOrDefault(n => n is not null);

        var chosen = pageLayouts.FirstOrDefault(l => (string?)l.Attribute(Style + "name") == masterLayout)
            ?? pageLayouts.FirstOrDefault(IsLandscape) ?? pageLayouts[0];
        var (width, height) = Dimensions(chosen);

        return width.Value > 0 && height.Value > 0 ? new SlideSize(width, height) : SlideSize.Widescreen;
    }

    private bool IsLandscape(XElement pageLayout)
    {
        var (width, height) = Dimensions(pageLayout);
        return width.Value > height.Value;
    }

    private (Twip Width, Twip Height) Dimensions(XElement pageLayout)
    {
        var properties = pageLayout.Element(Style + "page-layout-properties");
        return (values.Centimeters(properties?.Attribute(Fo + "page-width")), values.Centimeters(properties?.Attribute(Fo + "page-height")));
    }

    private PlaceholderTemplate Placeholder(
        XElement placeholder, string prefix, SlideLayout kind, SlideLayoutTemplate template,
        IReadOnlyDictionary<string, XElement> seeds, Theme theme, Dictionary<PlaceholderRole, int> seen)
    {
        var role = RoleOf((string?)placeholder.Attribute(Presentation + "object"));
        var occurrence = seen[role] = seen.GetValueOrDefault(role) + 1;
        var fallback = template.Placeholders.Where(p => p.Role == role).Skip(occurrence - 1).FirstOrDefault()?.Seed
            ?? template.Placeholders.FirstOrDefault(p => p.Role == role)?.Seed ?? new TextParagraph(string.Empty);
        var name = new OdfSeedNames().Of(prefix, kind, role, occurrence);
        var standardFrame = template.Placeholders.Where(p => p.Role == role).Skip(occurrence - 1).FirstOrDefault()?.Frame
            ?? template.Placeholders.FirstOrDefault(p => p.Role == role)?.Frame ?? TextFrame.Default;
        var found = seeds.TryGetValue(name, out var style);
        var seed = found ? Seed(style!, fallback, theme) : fallback;
        return new PlaceholderTemplate(role, values.Bounds(placeholder), seed)
        {
            Frame = found ? frames.Read(style).ToFrame(standardFrame) : standardFrame,
        };
    }

    private Dictionary<PlaceholderRole, TextFrameLayer> MasterFrames(XElement root, string? masterName)
    {
        var styles = new OdfTextFrameStylesBuilder(frames).Build(null, root);
        var result = new Dictionary<PlaceholderRole, TextFrameLayer>();
        var pages = root.Element(Office + "master-styles")?.Elements(Style + "master-page").ToList() ?? [];
        var page = masterName is null
            ? pages.FirstOrDefault()
            : pages.FirstOrDefault(candidate => (string?)candidate.Attribute(Style + "name") == masterName);
        foreach (var frame in page?.Elements(Draw + "frame") ?? [])
        {
            var role = (string?)frame.Attribute(Presentation + "class") switch
            {
                "title" => PlaceholderRole.Title,
                "subtitle" => PlaceholderRole.Subtitle,
                "outline" => PlaceholderRole.Body,
                _ => (PlaceholderRole?)null,
            };
            if (role is { } known && !result.ContainsKey(known)) result[known] = styles.Of(frame);
        }
        return result;
    }

    private TextParagraph Seed(XElement style, TextParagraph fallback, Theme theme)
    {
        var text = style.Element(Style + "text-properties");
        var paragraph = style.Element(Style + "paragraph-properties");

        var seed = fallback with
        {
            FontSize = (string?)text?.Attribute(Fo + "font-size") is { } size && !size.EndsWith('%') ? _sizes.Bounded(values.Points(size)) ?? fallback.FontSize : fallback.FontSize,
            Bold = (string?)text?.Attribute(Fo + "font-weight") is { } weight ? weight == "bold" : fallback.Bold,
            Italic = (string?)text?.Attribute(Fo + "font-style") is { } slant ? slant == "italic" : fallback.Italic,
            Align = values.Align((string?)paragraph?.Attribute(Fo + "text-align"), fallback.Align),
            Bullet = paragraph?.Attribute(Style + "list-style-name") is not null,
            MarginLeft = frames.Indents(paragraph).Margin ?? fallback.MarginLeft,
            Indent = frames.Indents(paragraph).Indent ?? fallback.Indent,
        };

        if (values.Color((string?)text?.Attribute(Fo + "color")) is { } color)
            seed = seed with { Color = color, ColorRole = null };

        if ((string?)text?.Attribute(Fo + "font-family") is { } font
            && !string.Equals(theme.Fonts.Major, theme.Fonts.Minor, StringComparison.OrdinalIgnoreCase))
            seed = seed with { MajorFont = string.Equals(font, theme.Fonts.Major, StringComparison.OrdinalIgnoreCase) };

        return seed;
    }

    private PlaceholderRole RoleOf(string? value) => value switch
    {
        "title" => PlaceholderRole.Title,
        "subtitle" => PlaceholderRole.Subtitle,
        "outline" => PlaceholderRole.Body,
        "graphic" => PlaceholderRole.Picture,
        _ => PlaceholderRole.Text,
    };
}

internal sealed class OdpSettingsReader
{
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private readonly OdpShowPagesXml _pages = new();

    public void Read(XElement? settings, PulseOffice.Presentations.Presentation deck)
    {
        if (settings is null) return;

        var byPage = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var slide in deck.Slides) byPage.TryAdd(slide.Id, slide.Id);
        foreach (var slide in deck.Slides) byPage.TryAdd(SlideNames.PageName(slide), slide.Id);
        foreach (var show in settings.Elements(Presentation + "show"))
        {
            var name = (string?)show.Attribute(Presentation + "name");
            if (string.IsNullOrEmpty(name)) continue;

            var ids = _pages.Own(show) is { } own
                ? [.. own.Where(byPage.ContainsKey).Select(page => byPage[page])]
                : ((string?)show.Attribute(Presentation + "pages") ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .SelectMany(page => byPage.TryGetValue(page, out var whole) ? [whole]
                        : page.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(byPage.ContainsKey).Select(part => byPage[part]))
                    .ToList();
            if (ids.Count > 0) deck.AddCustomShow(new CustomShow(name, ids));
        }

        var start = (string?)settings.Attribute(Presentation + "start-page");
        var from = start is null ? 0 : deck.Slides.ToList().FindIndex(s => s.Id == start);

        deck.Setup = new ShowSetup
        {
            Loop = (string?)settings.Attribute(Presentation + "endless") == "true",
            Advance = (string?)settings.Attribute(Presentation + "force-manual") == "true"
                ? ShowAdvanceMode.Manual
                : ShowAdvanceMode.UseTimings,
            WithoutAnimation = (string?)settings.Attribute(Presentation + "animations") == "disabled",
            FromSlide = from > 0 ? from + 1 : 0,
        };

        if (new HeaderFooterXml().Parse(new HeaderFooterXml().Find(settings)) is { } headerFooter)
            deck.HeaderFooter = headerFooter;
        new OdpGridXml().Read(settings, deck.Grid);
    }
}
