using System.Text;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml;

internal interface IPptxPresentationPartWriter
{
    void Write(PptxWriteSession session);
}

internal sealed class CustomShowsWriter : IPptxPresentationPartWriter
{
    public void Write(PptxWriteSession session)
    {
        if (session.Deck.CustomShows.Count == 0) return;

        var list = new P.CustomShowList();
        uint id = 0;
        foreach (var show in session.Deck.CustomShows)
        {
            var slides = new P.SlideList();
            foreach (var slide in show.SlideIds)
                if (session.RelationshipBySlide.TryGetValue(slide, out var relationship))
                    slides.Append(new P.SlideListEntry { Id = relationship });
            list.Append(new P.CustomShow(slides) { Name = show.Name, Id = id++ });
        }
        session.Part.Presentation!.Append(list);
    }
}

internal interface IPresentationExtension
{
    string Xml(PptxWriteSession session);
}

internal abstract class PresentationExtension : IPresentationExtension
{
    protected const string PresentationNamespace = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public string Xml(PptxWriteSession session) =>
        Body(session) is { Length: > 0 } body
            ? $"<p:ext xmlns:p=\"{PresentationNamespace}\" uri=\"{Uri}\">{body}</p:ext>"
            : string.Empty;

    protected abstract string Uri { get; }

    protected abstract string Body(PptxWriteSession session);
}

internal sealed class SectionsExtension : PresentationExtension
{
    private const string P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    protected override string Uri => "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}";

    protected override string Body(PptxWriteSession session)
    {
        var groups = new SlideSections().Group(session.Deck.Slides);
        if (groups.Count == 0) return string.Empty;

        var xml = new StringBuilder().Append("<p14:sectionLst xmlns:p14=\"").Append(P14).Append("\">");
        var used = new HashSet<Guid>();
        for (var ordinal = 0; ordinal < groups.Count; ordinal++)
        {
            var group = groups[ordinal];
            xml.Append("<p14:section name=\"").Append(System.Security.SecurityElement.Escape(group.Name))
                .Append("\" id=\"{").Append(SectionId(group, ordinal, used).ToString().ToUpperInvariant()).Append("}\"><p14:sldIdLst>");
            foreach (var slide in group.Slides)
                if (session.NumberBySlide.TryGetValue(slide.Id, out var number))
                    xml.Append("<p14:sldId id=\"").Append(number).Append("\"/>");
            xml.Append("</p14:sldIdLst></p14:section>");
        }
        return xml.Append("</p14:sectionLst>").ToString();
    }

    private static Guid SectionId(DeckSection group, int ordinal, HashSet<Guid> used)
    {
        if (group.Slides[0].SectionId is { } kept && Guid.TryParse(kept, out var read) && read != Guid.Empty && used.Add(read)) return read;

        for (var attempt = 0; ; attempt++)
        {
            var seed = Encoding.UTF8.GetBytes($"{group.Slides[0].Id}\u0000{group.Name}\u0000{ordinal}\u0000{attempt}");
            var bytes = System.Security.Cryptography.SHA256.HashData(seed).AsSpan(0, 16).ToArray();
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            var derived = new Guid(bytes);
            if (used.Add(derived)) return derived;
        }
    }
}

internal sealed class HeaderFooterExtension : PresentationExtension
{
    protected override string Uri => "{QO-HEADERFOOTER}";

    protected override string Body(PptxWriteSession session) =>
        session.Deck.HeaderFooter.Any
            ? $"<qo:hf xmlns:qo=\"{HeaderFooterXml.Namespace}\" {new HeaderFooterXml().Attributes(session.Deck.HeaderFooter)}/>"
            : string.Empty;
}

internal sealed class ExtensionsWriter(IReadOnlyList<IPresentationExtension> extensions) : IPptxPresentationPartWriter
{
    public void Write(PptxWriteSession session)
    {
        var xml = string.Concat(extensions.Select(extension => extension.Xml(session)));
        if (xml.Length == 0) return;

        session.Part.Presentation!.Append(new DocumentFormat.OpenXml.OpenXmlUnknownElement(
            "p", "extLst", "http://schemas.openxmlformats.org/presentationml/2006/main") { InnerXml = xml });
    }
}

internal sealed class ShowPropertiesWriter
{
    public void Write(PptxWriteSession session)
    {
        var setup = session.Deck.Setup;
        var show = new P.ShowProperties
        {
            Loop = setup.Loop,
            ShowAnimation = !setup.WithoutAnimation,
            UseTimings = setup.Advance == ShowAdvanceMode.UseTimings,
        };

        var count = session.Deck.Slides.Count;
        var start = Math.Clamp(setup.FromSlide > 0 ? setup.FromSlide : 1, 1, Math.Max(count, 1));
        var end = Math.Clamp(setup.ToSlide > 0 ? setup.ToSlide : count, start, Math.Max(count, 1));
        show.Append(setup.HasRange && count > 0
            ? new P.SlideRange { Start = (uint)start, End = (uint)end }
            : new P.SlideAll());
        show.Append(new P.PenColor(new D.RgbColorModelHex { Val = Pen(setup.PenColor) }));

        var part = session.Part.AddNewPart<PresentationPropertiesPart>();
        part.PresentationProperties = new P.PresentationProperties(show);
    }

    private string Pen(string hex)
    {
        var trimmed = hex.TrimStart('#');
        return trimmed.Length >= 6 && trimmed[..6].All(char.IsAsciiHexDigit) ? trimmed[..6].ToUpperInvariant() : "E11D2E";
    }
}

internal sealed class SlideListWriter(PptxMasterWriter master, PptxSlideWriter slides, OoxmlWriteUnits units, ISlideTargetResolver targets)
{
    private readonly PptxSlideLinkResolver _links = new(targets);

    public void Write(PptxWriteSession session, IReadOnlyList<(SlideMaster Master, SlideMasterPart Part, uint Id)> masters)
    {
        var deck = session.Deck;
        var layoutsByMaster = masters.ToDictionary(
            entry => entry.Master.Id,
            entry => entry.Master.Layouts.DistinctBy(template => template.Id, StringComparer.Ordinal)
                .ToDictionary(template => template.Id, template => master.Layout(entry.Part, template), StringComparer.Ordinal));
        var list = new P.SlideIdList();
        uint number = 256;

        foreach (var slide in deck.Slides)
        {
            var model = deck.MasterOf(slide);
            var entry = masters.First(candidate => ReferenceEquals(candidate.Master, model));
            var layouts = layoutsByMaster[model.Id];
            var template = model.Template(slide);
            if (!layouts.TryGetValue(template.Id, out var layout))
                layouts[template.Id] = layout = master.Layout(entry.Part, template);

            var relationship = session.Part.GetIdOfPart(slides.Write(session, layout, slide));
            session.RelationshipBySlide[slide.Id] = relationship;
            session.NumberBySlide[slide.Id] = number;
            list.Append(new P.SlideId { Id = number++, RelationshipId = relationship });
        }

        _links.Resolve(session);

        session.Part.Presentation!.Append(
            new P.SlideMasterIdList(masters.Select(entry => new P.SlideMasterId { Id = entry.Id, RelationshipId = session.Part.GetIdOfPart(entry.Part) })),
            list,
            new P.SlideSize { Cx = SlideEmu(deck.Size.Width), Cy = SlideEmu(deck.Size.Height) },
            new P.NotesSize { Cx = SlideEmu(deck.Size.Height), Cy = SlideEmu(deck.Size.Width) });
    }

    private int SlideEmu(PulseOffice.Foundation.Units.Twip length) => (int)Math.Clamp(units.Emu(length), 914400L, 51206400L);
}

internal sealed class PptxFidelityRules
{
    private readonly FidelityRuleSet<PulseOffice.Presentations.Presentation> _rules;

    private readonly DrawingStyleFidelity _drawing = new();

    private readonly IAnimationPresetRegistry _presets;

    private readonly PptxTransitionMapper _transitions = new();

    private readonly IDateFieldFormats _dates;

    public PptxFidelityRules(ISlideTargetResolver targets, PulseOffice.Formats.OpenXml.DrawingML.IDrawingNumberSchemes numbers, IAnimationPresetRegistry? presets = null, IDateFieldFormats? dates = null)
    {
        _targets = targets;
        _numbers = numbers;
        _presets = presets ?? new AnimationPresetRegistry();
        _dates = dates ?? new DateFieldFormats();
        _rules = Rules();
    }

    private readonly OoxmlChartFidelity _charts = new();

    private readonly FormulaShapeFidelity _formulas = new();

    private readonly PptxDiagramDataProbe _diagrams = new();

    private readonly ISlideTargetResolver _targets;

    private IEnumerable<string> SlideJumps(PulseOffice.Presentations.Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Interactions)
            .Where(action => action is { Kind: InteractionKind.GoToSlide, Reference.Length: > 0 }).Select(action => action.Reference!)
            .Concat(deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>().SelectMany(text => text.Text.Paragraphs)
                .SelectMany(paragraph => paragraph.EffectiveRuns()).Select(run => run.Hyperlink)
                .Where(link => link is { Length: > 1 } && link[0] == '#').Select(link => link![1..]));

    private FidelityRuleSet<PulseOffice.Presentations.Presentation> Rules() => new(
    [
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Any(element => element.Content is PulseOffice.Drawing.Scenes.Scene3DObject),
            Diagnostic.Info("ODP102", "3D scenes have no PPTX equivalent: other programs show a placeholder in their place, while PulseOffice restores the scene from an extension. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Any(element => _formulas.LosesFormulas(element.Content)),
            Diagnostic.Warning("PPTX190", "Shapes whose geometry is computed from ODF equations are written to PPTX with their current outline: the equations, modifiers and handles are lost. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Select(element => element.Content).OfType<PulseOffice.Charts.ChartDefinition>().Any(_charts.LosesType),
            Diagnostic.Warning("PPTX042", "Chart types that DrawingML does not have, such as stepped lines, waterfall, statistical and other special charts, are written as column charts in PPTX. ODP keeps them. PulseOffice reads the type back from an extension.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Select(element => element.Content).OfType<PulseOffice.Charts.ChartDefinition>().Any(_charts.FlattensThreeDimensional),
            Diagnostic.Info("PPTX043", "DrawingML has no three-dimensional line or column-and-line charts: these 3D charts are written flat in PPTX. ODP keeps the 3D view.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.FormSettings != new PulseOffice.Document.Forms.FormDocumentSettings(),
            Diagnostic.Warning("PPTX027", "Open in design mode, automatic focus and the data source of forms have no PPTX equivalent; they are dropped in PPTX and kept in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Masters.SelectMany(master => master.Objects.Concat(master.Layouts.SelectMany(layout => layout.Objects)))
                .Any(element => element is not ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject }),
            Diagnostic.Info("PPTX023", "Master and layout decoration other than preset shapes is not written in PPTX; preset shapes round-trip in the master and layout shape trees.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Interactions).Any(a => !string.IsNullOrEmpty(a.SoundPath) && deck.Sounds.Find(a.SoundPath) is null),
            Diagnostic.Warning("PPTX003", "Click sounds that point to a file outside the presentation are not written to PPTX, which only plays sounds stored in the file; embedded click sounds and the other click actions round-trip. ODP keeps the link.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Any(o => o.Content is not (PulseOffice.Drawing.Shapes.ShapeObject
                or PulseOffice.Drawing.Assets.RasterImageObject or PulseOffice.Drawing.Assets.VectorGraphicObject or PulseOffice.Document.Embedding.DataTableObject
                or PulseOffice.Document.Embedding.FormulaObject or PulseOffice.Charts.ChartDefinition
                or PulseOffice.Drawing.Shapes.ShapeGroupObject or PulseOffice.Drawing.Freeform.FreeformObject
                or PulseOffice.Drawing.Shapes.ConnectorObject or PulseOffice.Document.Embedding.PackagedObject or PulseOffice.Drawing.Fontwork.FontworkObject
                or PulseOffice.Drawing.Scenes.Scene3DObject)),
            Diagnostic.Warning("PPTX004", "Form controls are not written to PPTX by design: PPTX form controls are ActiveX (Windows-only, disabled by default in Microsoft 365), unsupported across PulseOffice. Shapes, images, charts, tables, freeform, connectors and embedded packages (OLE) round-trip; form controls round-trip in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Any(o => o.Content is PulseOffice.Drawing.Shapes.ConnectorObject),
            Diagnostic.Info("PPTX012", "Connectors are written as custom geometry; their live endpoint anchoring is not preserved and they re-read as freeform shapes.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => Objects(deck).Any(o => o.Content is PulseOffice.Drawing.Assets.RasterImageObject image
                && image.Filter is not (PulseOffice.Drawing.Assets.ImageFilter.None or PulseOffice.Drawing.Assets.ImageFilter.Grayscale)),
            Diagnostic.Info("PPTX010", "Image filters other than grayscale have no cross-format native representation and are shown on screen only; grayscale and crop round-trip.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(s => !string.IsNullOrEmpty(s.Transition.SoundPath) && deck.Sounds.Find(s.Transition.SoundPath) is null),
            Diagnostic.Warning("PPTX006", "Transition sounds that point to a file outside the presentation are not written to PPTX, which only plays sounds stored in the file; embedded transition sounds round-trip. ODP keeps the link.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Elements).OfType<TextElement>()
                .SelectMany(t => t.Text.Paragraphs).SelectMany(p => p.EffectiveRuns()).Any(r => r.Overline || r.Shadow),
            Diagnostic.Info("PPTX008", "Text overline and shadow have no native DrawingML run attribute; they round-trip in ODP but not in PPTX.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Elements).OfType<TextElement>()
                .SelectMany(t => t.Text.Paragraphs).SelectMany(p => p.EffectiveRuns())
                .Any(r => r.Field is TextFieldKind.SlideCount or TextFieldKind.SlideTitle or TextFieldKind.Author or TextFieldKind.FileName),
            Diagnostic.Info("PPTX015", "Slide count, slide title, author and file name fields have no PowerPoint field type; they are written with their current text, round-trip in PulseOffice and ODP, and do not update in PowerPoint.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Elements).OfType<TextElement>()
                .SelectMany(t => t.Text.Paragraphs).SelectMany(p => p.EffectiveRuns())
                .Any(r => r.Field is TextFieldKind.Date or TextFieldKind.Time && !_dates.Reaches(r.Field, r.FieldDetail?.Format)),
            Diagnostic.Warning("PPTX037", "PowerPoint knows thirteen date and time field formats: a date or time field with another format is written with the nearest one and comes back in that format. ODP keeps it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.DefaultLanguages.Complex is not null,
            Diagnostic.Info("PPTX038", "PowerPoint has no default language for complex scripts (Arabic, Hebrew, Hindi...): the presentation's default complex-script language is lost in PPTX. ODP keeps it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Macros.All.Count > 0,
            Diagnostic.Warning("PPTX016", "JavaScript and Python macros and their signature cannot be stored in PPTX, which only accepts VBA in .pptm; they are kept in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>()
                .Any(element => element.Content is PulseOffice.Drawing.Assets.RasterImageObject { Map.IsEmpty: false }),
            Diagnostic.Warning("PPTX017", "Image maps have no OOXML equivalent; the clickable areas are dropped in PPTX and kept in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>().Any(element => element.Content is PulseOffice.Drawing.Shapes.ShapeObject { Style.Extrusion.Kind: PulseOffice.Drawing.Shapes.ExtrusionKind.Lathe }),
            Diagnostic.Info("PPTX018", "3D rotation objects are written as extruded shapes; the rotation body is not preserved.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Slides.Any(slide => !slide.ShowMasterBackground && slide.Background is null),
            Diagnostic.Info("PPTX019", "A slide that hides the master background is written with a white background; PowerPoint has no flag for it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Animations).Any(a => !string.IsNullOrEmpty(a.SoundPath) && deck.Sounds.Find(a.SoundPath) is null),
            Diagnostic.Warning("PPTX022", "Animation sounds that point to a file outside the presentation are not written to PPTX, which only plays sounds stored in the file; embedded animation sounds round-trip. ODP keeps the link.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.SelectMany(s => s.Animations).Any(a => !_presets.ReachesOoxml(a.Category, a.Preset)),
            Diagnostic.Warning("PPTX032", "Animation presets that exist only in ODF (such as the physics effects, snake and clock wipes) have no PowerPoint preset number: PPTX keeps their behaviour (physics excepted) under the default preset number of their class, so they come back as that preset. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(slide => !_transitions.ReachesOoxml(slide.Transition)),
            Diagnostic.Warning("PPTX033", "ODF transitions without a PowerPoint counterpart (such as clock, iris, snake and the 3D ones) are written as their nearest basic kind in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => ShapeStyles(deck).Any(_drawing.LosesGradient),
            Diagnostic.Info("PPTX029", "DrawingML has no ellipsoid or square gradients and no gradient border: they become radial or rectangular gradients without border in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => ShapeStyles(deck).Any(_drawing.LosesHatch),
            Diagnostic.Info("PPTX030", "DrawingML hatches are fixed patterns: the angle and spacing of a hatch are lost in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(slide => slide.BackgroundImage is not null && ReshapedInPptx(slide.BackgroundLayout))
                || deck.Masters.Append(deck.Master).Any(master => master.BackgroundImage is not null && ReshapedInPptx(master.BackgroundLayout)),
            Diagnostic.Info("PPTX036", "PowerPoint backgrounds stretch or tile a picture at its own size: a background picture shown once at its natural size, or tiled at a chosen size, is stretched or tiled at the picture's size in PPTX. ODP keeps it.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => ShapeStyles(deck).Any(_drawing.LosesBitmapTile),
            Diagnostic.Info("PPTX034", "DrawingML tiles a bitmap fill at the picture's own size: the tile width and height chosen for a shape are lost in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => ShapeStyles(deck).Any(_drawing.LosesDashShape),
            Diagnostic.Info("PPTX035", "DrawingML custom dashes are measured in line widths and have square ends: rounded dashes and dash lengths given in centimetres become relative lengths in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => ShapeStyles(deck).Any(_drawing.LosesArrowSize),
            Diagnostic.Info("PPTX031", "DrawingML arrowheads come in three sizes: other arrow scales become small, medium or large in PPTX. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => SlideJumps(deck).Any(target => _targets.Find(deck, target) is null),
            Diagnostic.Warning("PPTX039", "Links and click actions that jump to a slide or object that does not exist in the presentation are dropped in PPTX, which can only jump to an existing slide. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.GraphicStyles.All.Count > 0,
            Diagnostic.Info("PPTX020", "Graphic styles have no OOXML equivalent; objects keep their look but lose the link to the named style.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(deck => deck.Handout.SlidesPerPage != new HandoutMaster().SlidesPerPage,
            Diagnostic.Info("PPTX021", "The handout master's slides per page has no PowerPoint attribute; it round-trips as a PulseOffice extension and PowerPoint uses its own choice.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Slides.Any(slide => slide.TextEditedDiagrams().Any(_diagrams.LosesEdits)),
            Diagnostic.Warning("PPTX221", "Text edited inside a SmartArt whose data part cannot be read is not written to PPTX: the SmartArt keeps its original text.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => deck.Versions.Count > 0,
            Diagnostic.Warning("PPTX014", "Document versions have no OOXML equivalent; they are dropped in PPTX and kept in ODP.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => !deck.Grid.IsStandard,
            Diagnostic.Info("PPTX028", "Grid, snapping and guide settings are not written to PPTX: PowerPoint keeps them in its view properties and uses its own. ODP keeps them.")),
        new FidelityRule<PulseOffice.Presentations.Presentation>(
            deck => NumberFormats(deck).Any(format => !_numbers.Reaches(format)),
            Diagnostic.Info("PPTX222", "PowerPoint numbers lists with a fixed set of schemes (1. 1) (1) a. A. i. I. and their variants): numbered lists with other prefixes, suffixes or number formats are written with the nearest scheme in PPTX. ODP keeps them.")),
    ]);

    public FidelityReport Check(PulseOffice.Presentations.Presentation deck) => _rules.Check(deck);

    private bool ReshapedInPptx(BackgroundImageLayout layout) =>
        layout.Mode is BackgroundImageMode.Natural || layout.Width.Value > 0 || layout.Height.Value > 0;

    private IEnumerable<PulseOffice.Drawing.Shapes.ShapeStyle> ShapeStyles(PulseOffice.Presentations.Presentation deck) =>
        Objects(deck)
            .Concat(deck.Masters.SelectMany(master => master.Objects.Concat(master.Layouts.SelectMany(layout => layout.Objects))).OfType<ObjectElement>())
            .SelectMany(element => Drawn(element.Content, 0))
            .Concat(deck.Slides.Select(slide => slide.BackgroundFill).Concat(deck.Masters.Append(deck.Master).Select(master => master.BackgroundFill))
                .OfType<PulseOffice.Drawing.Shapes.ShapeFill>().Select(fill => PulseOffice.Drawing.Shapes.ShapeStyle.Default with { Fill = fill }));

    private const int MaximumGroupDepth = 64;

    private IEnumerable<PulseOffice.Drawing.Shapes.ShapeStyle> Drawn(PulseOffice.Document.Embedding.EmbeddedObject content, int depth) => content switch
    {
        PulseOffice.Drawing.Shapes.ShapeGroupObject group when depth < MaximumGroupDepth => group.Children.SelectMany(child => Drawn(child.Content, depth + 1)),
        PulseOffice.Drawing.Shapes.ShapeObject shape => [shape.Style],
        PulseOffice.Drawing.Freeform.FreeformObject freeform => [freeform.Style],
        PulseOffice.Drawing.Shapes.ConnectorObject connector => [connector.Style],
        PulseOffice.Drawing.Fontwork.FontworkObject fontwork => [fontwork.Style],
        _ => [],
    };

    private readonly PulseOffice.Formats.OpenXml.DrawingML.IDrawingNumberSchemes _numbers;

    private IEnumerable<PulseOffice.Text.Rich.ListNumberFormat?> NumberFormats(PulseOffice.Presentations.Presentation deck) =>
        deck.Slides.SelectMany(slide => slide.Elements).OfType<TextElement>().SelectMany(text => text.Text.Paragraphs)
            .Where(paragraph => paragraph.Numbered).Select(paragraph => paragraph.NumberFormat)
            .Concat(Objects(deck).Select(element => element.Content).OfType<PulseOffice.Drawing.Shapes.ShapeObject>()
                .SelectMany(shape => shape.Lists ?? []).Where(item => item is { Numbered: true }).Select(item => item!.NumberFormat));

    private IEnumerable<ObjectElement> Objects(PulseOffice.Presentations.Presentation deck) =>
        deck.Slides.SelectMany(s => s.Elements).OfType<ObjectElement>();
}
