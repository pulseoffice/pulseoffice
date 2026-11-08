using System.Xml.Linq;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfMasterPages
{
    public const string PrimaryName = "Default";

    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    private static readonly XNamespace Pulse = "urn:pulseoffice:presentation-master";

    private readonly OdfUnits _units = new();
    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();

    private readonly OdfPrintMasters _print = new();

    private readonly OdfDeckShapeMapper _shapes = new();
    private readonly OdpVectorFrame _vectors = new(new OdfDeckShapeMapper());

    private readonly OdfLength _length = new();

    private readonly OdfGraphicStyles _graphicStyles = new();

    private readonly OdfPageFill _fills = new(new OdfShapeStyleReader());

    public string NameOf(PulseOffice.Presentations.Presentation deck, Slide slide)
    {
        var master = deck.MasterOf(slide);
        var name = BaseName(deck, master);
        var layout = master.Template(slide.Layout);
        return layout.Kind == slide.Layout && HasOwnPage(layout) ? LayoutPageName(name, slide.Layout) : name;
    }

    public IEnumerable<XElement> NotesStyles(PulseOffice.Presentations.Presentation deck) =>
        _print.NotesStyles(deck.Masters.Select(master => master.Name)
            .Concat(deck.Masters.SelectMany(master => master.Layouts.Where(HasOwnPage).Select(layout => LayoutDisplayName(master, layout.Kind))))
            .Distinct(StringComparer.Ordinal), deck.NotesMaster);

    private string BaseName(PulseOffice.Presentations.Presentation deck, SlideMaster master) =>
        ReferenceEquals(master, deck.Master) ? PrimaryName : master.Id;

    private bool HasOwnPage(SlideLayoutTemplate layout) => layout.HideMasterObjects || layout.Objects.Count > 0 || layout.Background is not null;

    private string LayoutPageName(string master, SlideLayout kind) => $"{master}-{kind}";

    private string LayoutDisplayName(SlideMaster master, SlideLayout kind) => $"{master.Name} {kind}";

    public XElement MasterStyles(PulseOffice.Presentations.Presentation deck, List<XElement> automaticStyles, OdpWriteSession session)
    {
        var styles = new XElement(OdfPackage.Namespaces.Office + "master-styles",
            new XAttribute(XNamespace.Xmlns + "qm", Pulse.NamespaceName));
        automaticStyles.Add(new XElement(Style + "style",
            new XAttribute(Style + "name", "Mpr1"),
            new XAttribute(Style + "family", "presentation"),
            new XElement(Style + "graphic-properties",
                new XAttribute(Draw + "fill", "none"),
                new XAttribute(Draw + "stroke", "none"))));
        var masterPages = new List<XElement>();
        for (var index = 0; index < deck.Masters.Count; index++)
        {
            var master = deck.Masters[index];
            var theme = index == 0 ? deck.Theme : master.Theme;
            var styleName = $"Mdp{index + 1}";
            var pageProperties = new XElement(Style + "drawing-page-properties",
                new XAttribute(Draw + "fill", "solid"),
                new XAttribute(Draw + "fill-color", _units.Hex(master.BackgroundColor ?? theme.Colors.Resolve(master.Background))));
            if (master.BackgroundImage is null && master.BackgroundFill is { } backgroundFill)
                session.ShapeStyles.WriteFill(Without(pageProperties), $"masterbg{index + 1}", backgroundFill);
            if (master.BackgroundImage is { } backgroundImage)
            {
                var path = $"Pictures/master_bg_{index + 1}.{_units.Images.Extension(backgroundImage.MediaType)}";
                session.AddMedia(path, backgroundImage.Data.ToArray(), backgroundImage.MediaType);
                pageProperties.SetAttributeValue(Draw + "fill", "bitmap");
                pageProperties.Add(new XAttribute(Draw + "fill-image-name", session.AddFillImage($"masterbg{index + 1}", path)), new XAttribute(Style + "repeat", master.BackgroundLayout.Mode switch
                {
                    BackgroundImageMode.Tile => "repeat",
                    BackgroundImageMode.Natural => "no-repeat",
                    _ => "stretch",
                }));
                if (master.BackgroundLayout.Width.Value > 0) pageProperties.Add(new XAttribute(Draw + "fill-image-width", _units.Cm(master.BackgroundLayout.Width)));
                if (master.BackgroundLayout.Height.Value > 0) pageProperties.Add(new XAttribute(Draw + "fill-image-height", _units.Cm(master.BackgroundLayout.Height)));
            }
            masterPages.Add(pageProperties);
            automaticStyles.Add(new XElement(Style + "style",
                new XAttribute(Style + "name", styleName),
                new XAttribute(Style + "family", "drawing-page"),
                pageProperties));

            var page = new XElement(Style + "master-page",
                new XAttribute(Style + "name", index == 0 ? PrimaryName : master.Id),
                new XAttribute(Style + "display-name", master.Name),
                new XAttribute(Style + "page-layout-name", "PL1"),
                new XAttribute(Draw + "style-name", styleName),
                new XAttribute(Pulse + "theme", theme.Name),
                LoextTheme(theme));
            if (master.Elements.Date) page.Add(Frame("date-time", 1.27, 17.4));
            if (master.Elements.Footer) page.Add(Frame("footer", 11.6, 17.4));
            if (master.Elements.SlideNumber) page.Add(Frame("page-number", 24.0, 17.4));
            Decorate(page, master.Objects, automaticStyles, session);
            page.Add(_print.Notes(deck.NotesMaster));
            styles.Add(page);
        }
        for (var index = 0; index < deck.Masters.Count; index++)
        {
            var master = deck.Masters[index];
            var theme = index == 0 ? deck.Theme : master.Theme;
            var baseName = index == 0 ? PrimaryName : master.Id;
            foreach (var layout in master.Layouts.Where(HasOwnPage))
            {
                var styleName = $"Mdp{index + 1}{layout.Kind}";
                automaticStyles.Add(new XElement(Style + "style",
                    new XAttribute(Style + "name", styleName),
                    new XAttribute(Style + "family", "drawing-page"),
                    layout.Background is { } own
                        ? new XElement(Style + "drawing-page-properties",
                            new XAttribute(Draw + "fill", "solid"),
                            new XAttribute(Draw + "fill-color", _units.Hex(theme.Colors.Resolve(own))))
                        : new XElement(masterPages[index])));
                var page = new XElement(Style + "master-page",
                    new XAttribute(Style + "name", LayoutPageName(baseName, layout.Kind)),
                    new XAttribute(Style + "display-name", LayoutDisplayName(master, layout.Kind)),
                    new XAttribute(Style + "page-layout-name", "PL1"),
                    new XAttribute(Draw + "style-name", styleName),
                    new XAttribute(Pulse + "theme", theme.Name),
                    new XAttribute(Pulse + "master", baseName),
                    new XAttribute(Pulse + "layout", layout.Kind.ToString()),
                    new XAttribute(Pulse + "hide-master-objects", layout.HideMasterObjects ? "true" : "false"),
                    LoextTheme(theme));
                if (layout.Background is { } role) page.Add(new XAttribute(Pulse + "background", role.ToString()));
                if (master.Elements.Date) page.Add(Frame("date-time", 1.27, 17.4));
                if (master.Elements.Footer) page.Add(Frame("footer", 11.6, 17.4));
                if (master.Elements.SlideNumber) page.Add(Frame("page-number", 24.0, 17.4));
                Decorate(page, layout.HideMasterObjects ? layout.Objects : master.Objects.Concat(layout.Objects), automaticStyles, session);
                page.Add(_print.Notes(deck.NotesMaster));
                styles.Add(page);
            }
        }
        styles.AddFirst(_print.Handout(deck.Handout));
        automaticStyles.Add(_print.PageLayout());
        return styles;
    }

    private void Decorate(XElement page, IEnumerable<SlideElement> objects, List<XElement> automaticStyles, OdpWriteSession session)
    {
        foreach (var element in objects.OfType<ObjectElement>().OrderBy(element => element.ZOrder))
        {
            if (element.Content is PulseOffice.Drawing.Assets.RasterImageObject image)
            {
                var path = session.AddOwnMedia($"Pictures/{_units.FileSafe(element.Id)}.{_units.Images.Extension(image.MediaType)}", image.Data.ToArray(), image.MediaType);
                var frame = _shapes.BuildImage(element, path);
                frame.Add(new XAttribute(Draw + "layer", "backgroundobjects"));
                page.Add(frame);
                continue;
            }
            if (element.Content is PulseOffice.Drawing.Assets.VectorGraphicObject vector)
            {
                var frame = _vectors.Build(element, vector, session, automaticStyles.Add);
                frame.Add(new XAttribute(Draw + "layer", "backgroundobjects"));
                page.Add(frame);
                continue;
            }
            if (element.Content is not PulseOffice.Drawing.Shapes.ShapeObject shape) continue;
            var style = session.NextStyle("Mgr");
            var built = session.ShapeStyles.Build(style, shape.Style);
            if (_graphicStyles.Encode(session.Deck.GraphicStyles, shape.StyleName) is { } parent)
                built.Add(new XAttribute(Style + "parent-style-name", parent));
            automaticStyles.Add(built);
            var node = _shapes.BuildShape(element, shape, style, new OdfTextStyleSink(() => session.NextStyle("MST"), automaticStyles.Add));
            node.Add(new XAttribute(Draw + "layer", "backgroundobjects"));
            page.Add(node);
        }
    }

    private List<SlideElement> Decoration(XElement page, IReadOnlyDictionary<string, XElement> definitions, string prefix, Func<string, byte[]?> readBytes) =>
        [.. page.Elements()
            .Where(IsDecoration)
            .Select((node, index) => DecorationElement(node, $"{prefix}{index + 1}", definitions, readBytes))
            .OfType<ObjectElement>()
            .Select((element, index) => element with { ZOrder = index })];

    private bool IsDecoration(XElement node) =>
        node.Name == Draw + "custom-shape"
            || (node.Name == Draw + "frame" && node.Attribute(Presentation + "class") is null && (node.Element(Draw + "image") is not null || TextBox(node) is not null));

    private XElement? TextBox(XElement frame) =>
        frame.Element(Draw + "text-box") is { } box && box.Descendants().Any() ? box : null;

    private ObjectElement? DecorationElement(XElement node, string id, IReadOnlyDictionary<string, XElement> definitions, Func<string, byte[]?> readBytes) =>
        node.Name == Draw + "custom-shape" ? _shapes.ReadShape(node, id, definitions)
        : TextBox(node) is { } box ? _shapes.ReadShape(TextShape(node, box), id, definitions)
        : _shapes.ReadImage(node, id, readBytes, definitions);

    private XElement TextShape(XElement frame, XElement box) =>
        new(Draw + "custom-shape",
            frame.Attributes().Where(attribute => attribute.Name.Namespace == OdfPackage.Namespaces.Svg || attribute.Name == Draw + "style-name"
                || attribute.Name == Draw + "transform" || attribute.Name == Draw + "name" || attribute.Name == Draw + "text-style-name"),
            box.Elements(),
            new XElement(Draw + "enhanced-geometry", new XAttribute(Draw + "type", "rectangle"), new XAttribute(OdfPackage.Namespaces.Svg + "viewBox", "0 0 21600 21600")));

    private sealed record LayoutPage(XElement Page, string Base, SlideLayout Kind, bool? Hides);

    private ColorRole? LayoutBackground(XElement page, XElement? automatic, SlideMaster master, Theme theme)
    {
        if (Enum.TryParse<ColorRole>((string?)page.Attribute(Pulse + "background"), out var marked)) return marked;
        if (page.Attribute(Pulse + "master") is not null) return null;
        return BackgroundOf(page, automatic, theme) is { } role && (master.BackgroundColor is not null || role != master.Background) ? role : null;
    }

    private Dictionary<XElement, LayoutPage> LayoutPages(IReadOnlyList<XElement> all)
    {
        var result = new Dictionary<XElement, LayoutPage>();
        foreach (var page in all.Skip(1))
        {
            if ((string?)page.Attribute(Pulse + "master") is { } marked
                && Enum.TryParse<SlideLayout>((string?)page.Attribute(Pulse + "layout"), out var markedKind))
            {
                result[page] = new LayoutPage(page, marked, markedKind,
                    page.Attribute(Pulse + "hide-master-objects") is { } hides ? (string?)hides == "true" : null);
                continue;
            }

            var display = (string?)page.Attribute(Style + "display-name") ?? (string?)page.Attribute(Style + "name") ?? string.Empty;
            foreach (var kind in Enum.GetValues<SlideLayout>())
            {
                var suffix = " " + kind;
                if (!display.EndsWith(suffix, StringComparison.Ordinal)) continue;
                var prefix = display[..^suffix.Length];
                if (all.FirstOrDefault(other => other != page && ((string?)other.Attribute(Style + "display-name") ?? (string?)other.Attribute(Style + "name")) == prefix) is { } owner
                    && (string?)owner.Attribute(Style + "name") is { } ownerName)
                    result[page] = new LayoutPage(page, ownerName, kind, null);
                break;
            }
        }

        return result;
    }

    private bool StartsWith(IReadOnlyList<SlideElement> objects, IReadOnlyList<SlideElement> master) =>
        objects.Count >= master.Count && master.Count > 0
            && master.Select((element, index) => element.Bounds == objects[index].Bounds).All(same => same);

    public Theme PrimaryTheme(XDocument? styles, IThemeCatalog themes)
    {
        ArgumentNullException.ThrowIfNull(themes);
        var first = styles?.Root?.Element(OdfPackage.Namespaces.Office + "master-styles")?.Element(Style + "master-page");
        if (first is null) return themes.Default;
        return ThemeOf(first, styles?.Root?.Element(OdfPackage.Namespaces.Office + "automatic-styles"), themes) ?? themes.Default;
    }

    private Theme? ThemeOf(XElement page, XElement? automatic, IThemeCatalog themes)
    {
        var loext = OdfPackage.Namespaces.Loext;
        foreach (var name in new[] { (string?)page.Attribute(Pulse + "theme"), (string?)page.Element(loext + "theme")?.Attribute(loext + "name"), (string?)page.Attribute(Style + "display-name"), (string?)page.Attribute(Style + "name") })
            if (name is { Length: > 0 } && themes.Find(name) is { } named) return named;

        var color = FillOf(page, automatic);
        return color is null ? null : themes.Themes.FirstOrDefault(theme => (theme.Colors.Background & 0x00FFFFFFu) == color);
    }

    private const uint NoFill = 0x00FFFFFF;

    private uint? FillOf(XElement page, XElement? automatic)
    {
        var styleName = (string?)page.Attribute(Draw + "style-name");
        var properties = automatic?.Elements(Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Style + "name") == styleName)?
            .Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") == "none") return NoFill;
        var color = properties?.Attribute(Draw + "fill-color")?.Value;
        return color is { Length: 7 } && uint.TryParse(color[1..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var rgb) ? rgb : null;
    }

    private BackgroundImageLayout BackgroundLayout(XElement page, XElement? automatic)
    {
        var styleName = (string?)page.Attribute(Draw + "style-name");
        var properties = automatic?.Elements(Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Style + "name") == styleName)?
            .Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") != "bitmap") return BackgroundImageLayout.Stretched;
        var mode = (string?)properties!.Attribute(Style + "repeat") switch
        {
            "stretch" => BackgroundImageMode.Stretch,
            "no-repeat" => BackgroundImageMode.Natural,
            _ => BackgroundImageMode.Tile,
        };
        return new BackgroundImageLayout(mode,
            _length.Parse((string?)properties.Attribute(Draw + "fill-image-width"), PulseOffice.Foundation.Units.Twip.Zero),
            _length.Parse((string?)properties.Attribute(Draw + "fill-image-height"), PulseOffice.Foundation.Units.Twip.Zero));
    }

    private XElement Without(XElement properties)
    {
        properties.Attribute(Draw + "fill")?.Remove();
        properties.Attribute(Draw + "fill-color")?.Remove();
        return properties;
    }

    private PulseOffice.Drawing.Shapes.ShapeFill? BackgroundFill(XElement page, XElement? automatic, IReadOnlyDictionary<string, XElement> definitions)
    {
        var styleName = (string?)page.Attribute(Draw + "style-name");
        return _fills.Read(automatic?.Elements(Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Style + "name") == styleName)?
            .Element(Style + "drawing-page-properties"), definitions);
    }

    private PulseOffice.Drawing.Assets.RasterImageObject? BackgroundImage(XElement page, XElement? automatic, IReadOnlyDictionary<string, XElement> definitions, string id)
    {
        var styleName = (string?)page.Attribute(Draw + "style-name");
        var properties = automatic?.Elements(Style + "style")
            .FirstOrDefault(style => (string?)style.Attribute(Style + "name") == styleName)?
            .Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") != "bitmap" || (string?)properties.Attribute(Draw + "fill-image-name") is not { } name) return null;
        if (!definitions.TryGetValue(name, out var image) || image.Element(OdfPackage.Namespaces.Office + "binary-data")?.Value is not { Length: > 0 } encoded) return null;
        byte[] data;
        try
        {
            data = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
        return new PulseOffice.Drawing.Assets.RasterImageObject
        {
            Id = $"{id}.bg",
            Data = data,
            MediaType = _header.MediaType(data) ?? "image/png",
            NaturalSize = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.Zero, PulseOffice.Foundation.Units.Twip.Zero),
        };
    }

    public void Read(XDocument? styles, XElement? body, PulseOffice.Presentations.Presentation deck, IThemeCatalog themes, Func<Theme, string, SlideMaster> masters, Func<string, byte[]?> readBytes)
    {
        var masterStyles = styles?.Root?.Element(OdfPackage.Namespaces.Office + "master-styles");
        _print.Read(masterStyles, deck, styles?.Root?.Element(OdfPackage.Namespaces.Office + "styles"));
        var all = masterStyles?.Elements(Style + "master-page").ToList() ?? [];
        var layoutPages = LayoutPages(all);
        var pages = all.Where(page => !layoutPages.ContainsKey(page)).ToList();
        if (pages.Count == 0) return;

        var automatic = styles?.Root?.Element(OdfPackage.Namespaces.Office + "automatic-styles");
        var definitions = new DrawingDefinitionTable().Read(styles!.Root!, styles.Root);
        var primaryName = (string?)pages[0].Attribute(Style + "name") ?? PrimaryName;
        deck.Master = deck.Master with
        {
            Elements = ElementsOf(pages[0]),
            Background = BackgroundOf(pages[0], automatic, deck.Theme) ?? deck.Master.Background,
            BackgroundColor = ExplicitBackground(pages[0], automatic, deck.Theme),
            BackgroundImage = BackgroundImage(pages[0], automatic, definitions, primaryName),
            BackgroundFill = BackgroundFill(pages[0], automatic, definitions),
            BackgroundLayout = BackgroundLayout(pages[0], automatic),
            Objects = Decoration(pages[0], definitions, "deco.m", readBytes),
        };

        foreach (var page in pages.Skip(1))
        {
            if ((string?)page.Attribute(Style + "name") is not { } name || name == primaryName || deck.Masters.Any(master => master.Id == name)) continue;
            var theme = ThemeOf(page, automatic, themes) ?? deck.Theme;
            var standard = masters(theme, name);
            deck.AddMaster(standard with
            {
                Id = name,
                Name = (string?)page.Attribute(Style + "display-name") ?? name,
                Elements = ElementsOf(page),
                Background = BackgroundOf(page, automatic, theme) ?? standard.Background,
                BackgroundColor = ExplicitBackground(page, automatic, theme),
                BackgroundImage = BackgroundImage(page, automatic, definitions, name),
                BackgroundFill = BackgroundFill(page, automatic, definitions),
                BackgroundLayout = BackgroundLayout(page, automatic),
                Objects = Decoration(page, definitions, "deco.m", readBytes),
            });
        }

        var bases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (page, baseName, kind, declared) in layoutPages.Values)
        {
            if ((string?)page.Attribute(Style + "name") is not { } pageName) continue;
            var master = baseName == primaryName ? deck.Master : deck.Masters.FirstOrDefault(candidate => candidate.Id == baseName);
            if (master is null) continue;
            bases[pageName] = baseName;
            var objects = Decoration(page, definitions, $"deco.{kind}.", readBytes);
            var hides = declared ?? !StartsWith(objects, master.Objects);
            var updated = master with
            {
                Layouts = [.. master.Layouts.Select(layout => layout.Kind == kind
                    ? layout with
                    {
                        Objects = hides ? objects : objects.Skip(master.Objects.Count).ToList(),
                        HideMasterObjects = hides,
                        Background = LayoutBackground(page, automatic, master, ReferenceEquals(master, deck.Master) ? deck.Theme : master.Theme),
                    }
                    : layout)],
            };
            deck.ReplaceMaster(updated);
        }

        var drawPages = body?.Elements(Draw + "page").ToList() ?? [];
        for (var index = 0; index < drawPages.Count && index < deck.Slides.Count; index++)
        {
            var name = (string?)drawPages[index].Attribute(Draw + "master-page-name");
            if (name is not null && bases.TryGetValue(name, out var baseName)) name = baseName;
            if (name is not null && name != primaryName && deck.Masters.Any(master => master.Id == name))
                deck.Slides[index].MasterId = name;
        }
    }

    private uint? ExplicitBackground(XElement page, XElement? automatic, Theme theme) =>
        FillOf(page, automatic) is { } rgb && BackgroundOf(page, automatic, theme) is null ? 0xFF000000u | rgb : null;

    private ColorRole? BackgroundOf(XElement page, XElement? automatic, Theme theme)
    {
        if (FillOf(page, automatic) is not { } rgb) return null;

        return Enum.GetValues<ColorRole>().Cast<ColorRole?>()
            .FirstOrDefault(role => (theme.Colors.Resolve(role!.Value) & 0x00FFFFFFu) == rgb);
    }

    private MasterElements ElementsOf(XElement page)
    {
        var classes = page.Elements(Draw + "frame").Select(frame => (string?)frame.Attribute(Presentation + "class")).ToHashSet();
        return classes.Count == 0
            ? MasterElements.All
            : new MasterElements(classes.Contains("date-time"), classes.Contains("footer"), classes.Contains("page-number"));
    }

    private XElement LoextTheme(Theme theme)
    {
        var loext = OdfPackage.Namespaces.Loext;
        var colors = theme.Colors;
        (string Name, uint Value)[] entries =
        [
            ("dark1", colors.Text), ("light1", colors.Background), ("dark2", colors.Text2), ("light2", colors.Background2),
            ("accent1", colors.Accent1), ("accent2", colors.Accent2), ("accent3", colors.Accent3),
            ("accent4", colors.Accent4), ("accent5", colors.Accent5), ("accent6", colors.Accent6),
            ("hyperlink", colors.Hyperlink), ("followed-hyperlink", colors.FollowedHyperlink),
        ];
        return new XElement(loext + "theme",
            new XAttribute(loext + "name", theme.Name),
            new XElement(loext + "theme-colors",
                new XAttribute(loext + "name", theme.Name),
                entries.Select(entry => new XElement(loext + "color",
                    new XAttribute(loext + "name", entry.Name),
                    new XAttribute(loext + "color", _units.Hex(entry.Value).ToLowerInvariant())))));
    }

    private XElement Frame(string kind, double x, double y) =>
        new(Draw + "frame",
            new XAttribute(Presentation + "style-name", "Mpr1"),
            new XAttribute(Draw + "layer", "backgroundobjects"),
            new XAttribute(Presentation + "placeholder", "true"),
            new XAttribute(Presentation + "class", kind),
            new XAttribute(Svg + "x", $"{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}cm"),
            new XAttribute(Svg + "y", $"{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}cm"),
            new XAttribute(Svg + "width", "8.5cm"),
            new XAttribute(Svg + "height", "1.2cm"),
            new XElement(Draw + "text-box", new OdfHeaderFooter().FieldFor(kind)));
}
