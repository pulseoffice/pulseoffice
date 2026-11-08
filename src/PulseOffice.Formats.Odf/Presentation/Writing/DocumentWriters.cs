using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpPageWriter(
    ISlideLayoutRegistry layouts,
    PageStyleBuilder pageStyles,
    TextFrameWriter text,
    MediaFrameWriter media,
    ObjectWriterChain objects,
    OdfAnimationMapper animations,
    FormControlsWriter forms,
    CommentsWriter comments,
    InteractionListenerWriter listeners,
    ISlideGroupDecomposition decomposition) : OdpXml
{
    private readonly OdpSlideGroupWriter _groups = new(decomposition);

    private readonly OdfHeaderFooter _headerFooter = new();

    private readonly OdpSectionXml _sections = new();

    private readonly OdpElementVisibility _visibility = new();

    public XElement Write(Slide slide, int number, OdpWriteSession session)
    {
        var page = new XElement(Draw + "page",
            new XAttribute(Draw + "name", session.PageName(slide.Id)),
            new XAttribute(Xml + "id", PageId(number)),
            new XAttribute(Draw + "id", PageId(number)),
            new XAttribute(Draw + "style-name", pageStyles.Build(slide, number, session)),
            new XAttribute(Draw + "master-page-name", new OdfMasterPages().NameOf(session.Deck, slide)),
            new XAttribute(Presentation + "presentation-page-layout-name", layouts.OdfName(slide.Layout)));
        if (session.Deck.HeaderFooter.Any) _headerFooter.Use(page, session.Deck.HeaderFooter);
        _sections.Write(page, slide);

        var nodes = new SlideGroupNodes<XElement>();
        foreach (var element in slide.InZOrder())
        {
            switch (element)
            {
                case TextElement textElement:
                {
                    var written = _visibility.Write(_rotation.Write(text.Write(textElement, slide, session), textElement), textElement);
                    page.Add(written);
                    nodes.Remember(textElement.Id, written);
                    break;
                }
                case MediaElement mediaElement:
                {
                    var written = _visibility.Write(_rotation.Write(media.Write(mediaElement, session), mediaElement), mediaElement);
                    listeners.Attach(written, slide, mediaElement.Id, session);
                    page.Add(written);
                    nodes.Remember(mediaElement.Id, written);
                    break;
                }
                case ObjectElement objectElement:
                    var attached = false;
                    var decomposed = decomposition.Decompose(objectElement);
                    nodes.Declare(decomposed.Groups);
                    foreach (var piece in decomposed.Leaves)
                        if (objects.Write(piece, session) is { } written)
                        {
                            var frame = _visibility.Write(_rotation.Write(written, piece), objectElement);
                            if (!attached) listeners.Attach(frame, slide, objectElement.Id, session);
                            attached = true;
                            page.Add(frame);
                            nodes.Remember(piece.Id, frame);
                        }
                    break;
            }
        }
        nodes.Declare(slide.Groups);
        _groups.Wrap(nodes);

        forms.Write(page, slide, session);
        page.Add(comments.Write(slide));

        if (animations.Build(slide, PageId(number)) is { } timing)
        {
            animations.Identify(page, slide);
            page.Add(timing);
        }
        if (!string.IsNullOrEmpty(slide.Notes))
        {
            var notesStyle = session.NextStyle("pr");
            session.AddStyle(new XElement(Style + "style",
                new XAttribute(Style + "name", notesStyle),
                new XAttribute(Style + "family", "presentation"),
                new XElement(Style + "graphic-properties",
                    new XAttribute(Draw + "fill", "none"),
                    new XAttribute(Draw + "stroke", "none")),
                new XElement(Style + "text-properties",
                    new XAttribute(Fo + "font-size", session.Deck.NotesMaster.FontSize.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "pt"))));
            page.Add(new OdfPrintMasters().SlideNotes(session.Deck.NotesMaster, slide.Notes, notesStyle));
        }
        return page;
    }

    private readonly OdpElementRotation _rotation = new();

    private string PageId(int number) => $"qo-page{number}";
}

internal sealed class OdpSettingsWriter : OdpXml
{
    private readonly OdpGridXml _grid = new();
    private readonly OdpShowPagesXml _pages = new();

    public XElement? Write(OdpWriteSession session)
    {
        var deck = session.Deck;
        var setup = deck.Setup;
        var settings = new XElement(Presentation + "settings");

        if (setup.Loop) settings.Add(new XAttribute(Presentation + "endless", "true"));
        if (setup.Advance == ShowAdvanceMode.Manual) settings.Add(new XAttribute(Presentation + "force-manual", "true"));
        if (setup.WithoutAnimation) settings.Add(new XAttribute(Presentation + "animations", "disabled"));
        if (setup.FromSlide > 0 && setup.FromSlide <= deck.Slides.Count)
            settings.Add(new XAttribute(Presentation + "start-page", session.PageName(deck.Slides[setup.FromSlide - 1].Id)));

        foreach (var show in deck.CustomShows)
        {
            var element = new XElement(Presentation + "show", new XAttribute(Presentation + "name", show.Name));
            _pages.Write(element, [.. show.SlideIds.Select(session.PageName)]);
            settings.Add(element);
        }

        if (deck.HeaderFooter.Any) settings.Add(new HeaderFooterXml().Element(deck.HeaderFooter));
        settings.Add(_grid.Element(deck.Grid));

        return settings.HasAttributes || settings.HasElements ? settings : null;
    }
}

internal sealed class OdpNamespaces : OdpXml
{
    public object[] Declarations() =>
    [
        new XAttribute(XNamespace.Xmlns + "office", Office.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "style", Style.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "text", Text.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "draw", Draw.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "presentation", Presentation.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "fo", Fo.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "svg", Svg.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "smil", Smil.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "anim", Anim.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "form", Form.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "script", OdfPackage.Namespaces.Script.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "dom", OdfPackage.Namespaces.Dom.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "ooo", OdfPackage.Namespaces.Ooo.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "drawooo", OdfPackage.Namespaces.DrawExt.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "loext", OdfPackage.Namespaces.Loext.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "table", OdfPackage.Namespaces.Table.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "xlink", OdfPackage.Namespaces.Xlink.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "dc", OdfPackage.Namespaces.Dc.NamespaceName),
        new XAttribute(XNamespace.Xmlns + "number", OdfPackage.Namespaces.Number.NamespaceName),
    ];
}

internal sealed class OdpContentBuilder(
    OdpPageWriter pages, OdpSettingsWriter settings, ListStyleBuilder lists, OdpNamespaces namespaces) : OdpXml
{
    public XDocument Build(OdpWriteSession session)
    {
        var presentation = new XElement(Office + "presentation");
        if (session.Deck.HeaderFooter.Any) presentation.Add(new OdfHeaderFooter().Declarations(session.Deck.HeaderFooter, session.AutomaticStyles));
        for (var index = 0; index < session.Deck.Slides.Count; index++)
            presentation.Add(pages.Write(session.Deck.Slides[index], index + 1, session));

        if (settings.Write(session) is { } written) presentation.Add(written);

        if (session.UsesListStyles) session.AutomaticStyles.Add(lists.Styles());

        var (fonts, files) = new OdfFontFaceWriter().Build(new PulseOffice.Presentations.PresentationFontNames().Used(session.Deck), session.Deck.Fonts);
        session.FontFiles = files;

        return new XDocument(new XElement(Office + "document-content",
            namespaces.Declarations(), fonts, session.AutomaticStyles, new XElement(Office + "body", presentation)));
    }
}

internal sealed class OdpStylesBuilder(OdfUnits units, ISlideLayoutRegistry layouts, SeedStyleBuilder seeds, OdpNamespaces namespaces)
    : OdpXml
{
    public XDocument Build(PulseOffice.Presentations.Presentation deck, OdpWriteSession session)
    {
        var automatic = new List<XElement>
        {
            new(Style + "page-layout",
                new XAttribute(Style + "name", "PL1"),
                new XElement(Style + "page-layout-properties",
                    new XAttribute(Fo + "page-width", units.Cm(deck.Size.Width)),
                    new XAttribute(Fo + "page-height", units.Cm(deck.Size.Height)),
                    new XAttribute(Style + "print-orientation", "landscape"))),
        };
        var masterPages = new OdfMasterPages();
        var masters = masterPages.MasterStyles(deck, automatic, session);
        var names = new OdfSeedNames();
        var masterLayouts = deck.Masters
            .Select((master, index) => (Master: master, Theme: index == 0 ? deck.Theme : master.Theme, Prefix: index == 0 ? string.Empty : names.MasterPrefix(master.Id)))
            .ToList();
        var graphicMapper = session.ShapeStyles;
        var graphicStyles = new OdfGraphicStyles().Build(deck.GraphicStyles, graphicMapper).ToList();

        return new(new XElement(Office + "document-styles",
            namespaces.Declarations(),
            new XElement(Office + "styles", new OdpTextDefaults().Write(deck.DefaultLanguages, deck.DefaultScriptFonts), masterLayouts.SelectMany(entry => entry.Master.Layouts.DistinctBy(layout => layout.Kind).Select(layout => PageLayout(entry.Prefix, layout))), seeds.Build(masterLayouts),
                graphicMapper.Definitions, session.FillImages(), graphicStyles, masterPages.NotesStyles(deck)),
            new XElement(Office + "automatic-styles", automatic),
            masters));
    }

    private XElement PageLayout(string prefix, SlideLayoutTemplate template) =>
        new(Style + "presentation-page-layout",
            new XAttribute(Style + "name", prefix + layouts.OdfName(template.Kind)),
            template.Placeholders.Select(placeholder => new XElement(Presentation + "placeholder",
                new XAttribute(Presentation + "object", ObjectOf(placeholder.Role)),
                units.Bounds(placeholder.Bounds, Svg))));

    private string ObjectOf(PlaceholderRole role) => role switch
    {
        PlaceholderRole.Title => "title",
        PlaceholderRole.Subtitle => "subtitle",
        PlaceholderRole.Body => "outline",
        PlaceholderRole.Picture => "graphic",
        _ => "text",
    };
}

internal sealed class OdpPackageWriter
{
    public void Write(OdfPackage package, OdpWriteSession session, XDocument content, XDocument styles, string mimeType)
    {
        new OdpSounds(new SoundTypes()).Write(session, content, styles);
        package.WriteXml("content.xml", content);
        package.WriteXml("styles.xml", styles);
        package.WriteXml("meta.xml", new OdfMetaMapper().Build(session.Deck.Properties));

        foreach (var entry in session.Media) package.WriteBytes(entry.Path, entry.Data);
        foreach (var document in session.Documents) package.WriteXml($"{document.Folder}/content.xml", document.Document);

        var manifest = new List<(string, string)> { ("content.xml", "text/xml"), ("styles.xml", "text/xml"), ("meta.xml", "text/xml") };
        manifest.AddRange(session.Media.Select(entry => (entry.Path, entry.MediaType)));
        foreach (var document in session.Documents)
        {
            manifest.Add(($"{document.Folder}/", document.MediaType));
            manifest.Add(($"{document.Folder}/content.xml", "text/xml"));
        }
        manifest.AddRange(new OdfFontFaceWriter().WriteAll(package, session.FontFiles, session.Deck.Fonts));
        manifest.AddRange(new OdfVersionMapper().Write(package, session.Deck.Versions, OdfPackage.MimeTypes.Presentation));
        manifest.AddRange(new OdfMacroMapper().Write(package, session.Deck.Macros, session.Deck.MacroSignature));
        package.WriteManifest(mimeType, manifest);
    }
}

internal sealed class OdpFidelityRules
{
    private readonly FidelityRuleSet<PulseOffice.Presentations.Presentation> _rules;

    private readonly SmilTransitionMapper _transitions = new();

    private readonly OdfChartVocabulary _charts = new();

    public OdpFidelityRules() => _rules = new(
    [
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Masters.Any(master => master.Layouts.GroupBy(layout => layout.Kind).Any(group => group.Count() > 1)),
            Diagnostic.Info("ODP107", "ODF has one page layout per layout type: further layouts of the same type, such as custom layouts read from PowerPoint files, are written as the first layout of their type, and their slides re-read with it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(slide => slide.Transition.Kind is TransitionKind.Cut or TransitionKind.Zoom or TransitionKind.Morph),
            Diagnostic.Info("ODP021", "Cut, zoom and morph transitions have no ODF equivalent; they are written and re-read as a crossfade.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Masters.SelectMany(master => master.Objects.Concat(master.Layouts.SelectMany(layout => layout.Objects)))
                .Any(element => element is not ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject or PulseOffice.Drawing.Assets.RasterImageObject or PulseOffice.Drawing.Assets.VectorGraphicObject }),
            Diagnostic.Info("ODP020", "Master and layout decoration other than preset shapes, images and SVG graphics is not written in ODP; preset shapes, images and SVG graphics round-trip on the master pages.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => Objects(deck).Any(o => o.Content is not (PulseOffice.Drawing.Shapes.ShapeObject
                or PulseOffice.Drawing.Assets.RasterImageObject or PulseOffice.Drawing.Assets.VectorGraphicObject or PulseOffice.Document.Embedding.DataTableObject
                or PulseOffice.Document.Embedding.FormulaObject or PulseOffice.Charts.ChartDefinition
                or PulseOffice.Drawing.Shapes.ShapeGroupObject or PulseOffice.Drawing.Freeform.FreeformObject
                or PulseOffice.Drawing.Shapes.ConnectorObject or PulseOffice.Document.Forms.FormControlObject
                or PulseOffice.Document.Embedding.PackagedObject or PulseOffice.Drawing.Fontwork.FontworkObject)),
            Diagnostic.Warning("ODP004", "Shapes, images, freeform, connectors, form controls and embedded packages (OLE) round-trip in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => Objects(deck).Select(o => o.Content).OfType<PulseOffice.Charts.ChartDefinition>().Any(chart => _charts.KeptOnlyByExtension(chart.Type)),
            Diagnostic.Info("ODP191", "Chart types that ODF does not define, such as waterfall, statistical and other special charts, keep their type in a PulseOffice extension: other applications show them as the nearest standard chart, usually columns.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => Objects(deck).Any(o => o.Content is PulseOffice.Drawing.Shapes.ConnectorObject),
            Diagnostic.Info("ODP017", "Connectors are written as draw:path; their live endpoint anchoring is not preserved and they re-read as freeform shapes.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Any(element => element.Content is PulseOffice.Drawing.Shapes.ShapeObject { Style.Extrusion.Kind: PulseOffice.Drawing.Shapes.ExtrusionKind.Lathe }),
            Diagnostic.Info("ODP018", "3D rotation objects are written as extruded shapes; the rotation body is not preserved.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>()
                .Any(text => text.Text.Frame.Autofit == TextAutofit.ShrinkText && (text.Text.Frame.FontScale < 0.9999 || text.Text.Frame.LineReduction > 0.0001)),
            Diagnostic.Info("ODP019", "Shrink-on-overflow round-trips, but the stored font scale and line spacing reduction have no ODF attribute; they are recomputed when the text is drawn.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => Objects(deck).Any(o => o.Content is PulseOffice.Drawing.Assets.RasterImageObject { Duotone: not null })
                || deck.Slides.Any(slide => slide.BackgroundImage?.Duotone is not null)
                || deck.Masters.Append(deck.Master).Any(master => master.BackgroundImage?.Duotone is not null),
            Diagnostic.Warning("ODP026", "Duotone images have no ODF equivalent: the original image is written and the two-colour recolouring is lost in ODP. PPTX keeps it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => Objects(deck).Any(o => o.Content is PulseOffice.Drawing.Assets.RasterImageObject image
                && image.Filter is not (PulseOffice.Drawing.Assets.ImageFilter.None or PulseOffice.Drawing.Assets.ImageFilter.Grayscale)),
            Diagnostic.Info("ODP015", "Image filters other than grayscale have no cross-format native representation and are shown on screen only; grayscale and crop round-trip.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(_ => true,
            Diagnostic.Info("ODP010", "Theme colour roles degrade to absolute colours and theme major/minor fonts to font names in ODP; the theme itself is not preserved.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Setup.ToSlide > 0 || deck.Setup.PenColor != new ShowSetup().PenColor,
            Diagnostic.Info("ODP012", "The show pen colour and the end of a slide range have no ODF equivalent; loop, manual advance and start slide round-trip.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.Any(slide => !_transitions.ReachesOdf(slide.Transition)),
            Diagnostic.Warning("ODP023", "PowerPoint transitions without an ODF counterpart (such as the 3D and dynamic content transitions) are written as their nearest basic kind in ODP. PPTX keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>().Any(text => text.Backdrop is { Shape: not PulseOffice.Drawing.Shapes.ShapeKind.Rectangle }),
            Diagnostic.Info("ODP024", "Placeholder frames in ODF are rectangles: a filled or outlined placeholder with another outline (such as rounded corners) keeps its fill and line but becomes rectangular in ODP. PPTX keeps it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(slide => slide.IntactDiagrams().Any()),
            Diagnostic.Info("ODP025", "SmartArt diagrams are written as the shapes of their drawing: they look the same in ODP, but the diagram data and layout are kept only in PPTX.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(CommaInCustomShow,
            Diagnostic.Info("ODF286", "ODF lists the slides of a custom show separated by commas: a slide whose name contains a comma is kept in its show by a PulseOffice extension, other applications leave it out of the show.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(CustomizedLayouts,
            Diagnostic.Info("ODP022", "Layouts with their own background or with moved placeholders have no ODF equivalent: they return as the standard layouts in ODP, slides keep their own placeholders. PPTX keeps them.")),
    ]);

    private bool CommaInCustomShow(PulseOffice.Presentations.Presentation deck)
    {
        var pages = new OdpShowPagesXml();
        var slides = deck.Slides.ToDictionary(slide => slide.Id, StringComparer.Ordinal);
        return deck.CustomShows.SelectMany(show => show.SlideIds)
            .Any(id => slides.TryGetValue(id, out var slide) && !pages.Representable(SlideNames.PageName(slide)));
    }

    private bool CustomizedLayouts(PulseOffice.Presentations.Presentation deck)
    {
        var standard = new MasterFactory().CreateStandard(deck.Theme, deck.Size);
        return deck.Masters.Append(deck.Master).SelectMany(master => master.Layouts).Any(layout =>
            layout.Background != standard.Template(layout.Kind).Background
            || !layout.Placeholders.Select(placeholder => (placeholder.Role, placeholder.Bounds))
                .SequenceEqual(standard.Template(layout.Kind).Placeholders.Select(placeholder => (placeholder.Role, placeholder.Bounds))));
    }

    public FidelityReport Check(PulseOffice.Presentations.Presentation deck) => _rules.Check(deck);

    private IEnumerable<ObjectElement> Objects(PulseOffice.Presentations.Presentation deck) =>
        deck.Slides.SelectMany(s => s.Elements).OfType<ObjectElement>();
}
