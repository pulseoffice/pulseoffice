using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxReader : IPresentationReader
{
    private readonly OoxmlAttributeSanitizer _sanitizer = new();
    private readonly OoxmlReadFailures _failures = new();
    private readonly IReadOnlyList<IPptxReadLoss> _readLosses = [new PptxEquations(), new PptxUnknownPresets(new UnknownPresetShapes(new PulseOffice.Drawing.Shapes.ShapePresets())), new PptxUnsupportedClickActions(), new PptxCommentThreads(), new PptxMixedTableRuns()];
    private readonly PptxThemeReader _theme;
    private readonly PptxMasterReader _master;
    private readonly SlideListReader _slides;
    private readonly IReadOnlyList<IPptxPresentationPartReader> _parts =
    [
        new SectionReader(),
        new CustomShowReader(),
        new ShowPropertiesReader(),
        new HeaderFooterReader(),
        new NativeHeaderFooterReader(),
        new PrintMastersReader(new PptxPrintMasters()),
    ];

    public PptxReader(IThemeCatalog themes, IMasterFactory masters)
        : this(themes, masters, PptxFlavour.Presentation)
    {
    }

    public PptxReader(IThemeCatalog themes, IMasterFactory masters, PptxFlavour flavour)
    {
        ArgumentNullException.ThrowIfNull(flavour);
        Format = flavour.Format;

        var values = new OoxmlValues();
        var layouts = new SlideLayoutRegistry();
        var classifier = new ShapeClassifier();
        var altText = new OoxmlAltTextMapper();
        var shapes = new PptxDeckShapeMapper();

        var schemes = new SchemeRoles();
        var frames = new BodyFrameReader(new DrawingTextAnchors());
        var maps = new ColorMapCodec();
        var styles = new LevelStyleReader(values, schemes);
        var inheritance = new PlaceholderInheritance(
            new PlaceholderMatcher(
                [new ExactMatchRule(), new IndexMatchRule(), new TypeMatchRule(), new FamilyMatchRule()],
                [new TypeMatchRule(), new FamilyMatchRule()]),
            styles, values);
        _theme = new PptxThemeReader(themes, values, maps);
        var colourModifiers = new DrawingColourModifiers();
        var colourReader = new RunColorReader(values, schemes, new PresetColors());
        var backgrounds = new PptxBackgroundFillReader(colourReader, colourModifiers, new DrawingDuotone(colourModifiers, new PresetColors(), colourReader));
        var normalizer = new DrawingShapeColourNormalizer(colourReader, colourModifiers);
        var identities = new PptxLayoutIdentity(layouts);
        _master = new PptxMasterReader(masters, values, classifier, schemes, inheritance, frames, maps, shapes, backgrounds, normalizer, identities);
        _slides = new SlideListReader(new PptxSlideReader(
            identities,
            schemes,
            maps,
            _theme,
            classifier,
            new PptxTransitionMapper(),
            new TextShapeReader(classifier, inheritance,
                new DrawingParagraphReader(new DrawingRunReader(new DrawingFieldTypes(), styles, new RunColorReader(values, schemes, new PresetColors())), styles, new DrawingParagraphLevels()),
                new PptxInteractionMapper(),
                frames,
                new PptxTextBackdrops(new XlsxShapeStyleMapper(), new DrawingPresetGeometry())),
            [
                new PictureReader(new PptxMediaMapper(), shapes, altText),
                new DeckShapeReader(classifier, shapes, altText),
                new ConnectionShapeReader(shapes, altText),
                new GraphicFrameReader(shapes, new PptxDeckChartMapper(), new PptxDeckTableMapper(new PptxTableStyleResolver(colourReader, colourModifiers, new PptxBuiltInTableStyles())), altText),
                new ObjectInteractionReader(new PptxInteractionMapper()),
                new PptxHiddenElementReader(new PptxElementVisibility()),
                new PptxSoundReader(new PptxSounds(new SoundTypes()), new PptxTransitionMapper()),
                new PptxGroupReader(),
                new PptxAnimationReader(new PptxAnimationMapper(), new PptxSounds(new SoundTypes())),
                new PptxCommentReader(new PptxCommentMapper(new PulseOffice.Formats.Packaging.XmlCharacterFilter())),
                new BackgroundImageReader(backgrounds),
                new NotesSlideReader(new PptxPrintMasters()),
            ],
            normalizer,
            backgrounds), new PptxDiagramExpander(new Diagrams.DiagramDrawingGenerator()));
    }

    public PresentationFormat Format { get; }

    public bool CanRead(Stream stream)
    {
        var start = stream.Position;
        try
        {
            var damage = new List<PartDamage>();
            using var repaired = new OoxmlPackageRepair().Repair(stream, damage);
            using var document = PresentationDocument.Open(repaired, false);
            return document.PresentationPart?.Presentation is not null;
        }
        catch (Exception ex) when (_failures.IsMalformedInput(ex))
        {
            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    public OperationResult<Presentation> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var damage = new List<PartDamage>();
            using var repaired = new OoxmlPackageRepair().Repair(stream, damage);
            using var document = PresentationDocument.Open(repaired, isEditable: true, new OpenSettings { AutoSave = false });
            _sanitizer.Sanitize(document);
            if (document.PresentationPart is not { Presentation: { } presentation } part)
                return OperationResult<Presentation>.Fail(Diagnostic.Error("PPTX100", "presentation.xml is missing."));

            var theme = _theme.Read(part);
            var size = _master.Size(presentation);
            var deck = new Presentation
            {
                Theme = theme,
                Master = _master.Read(part, theme, size),
                Size = size,
            };

            new OoxmlCorePropertiesMapper().Read(document, deck.Properties);
            var fontLosses = new List<Diagnostic>();
            deck.Fonts = new PptxEmbeddedFonts().Read(part, fontLosses);
            (deck.DefaultLanguages, deck.DefaultScriptFonts) = new PptxTextDefaults().Read(part);

            var masterParts = part.SlideMasterParts.ToList();
            var masterIds = new Dictionary<SlideMasterPart, string>();
            for (var position = 1; position < masterParts.Count; position++)
            {
                var masterTheme = _theme.Read(masterParts[position]);
                var extra = _master.Read(masterParts[position], masterTheme, size) with { Id = deck.NextMasterId() };
                deck.AddMaster(extra);
                masterIds[masterParts[position]] = extra.Id;
            }

            var readLosses = _readLosses.Where(loss => loss.Present(part)).Select(loss => loss.Loss).ToList();
            var index = _slides.Read(part, deck);
            var slidesById = deck.Slides.GroupBy(slide => slide.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var (relationship, slideId) in index.ByRelationship)
                if (masterIds.Count > 0 && part.TryGetPartById(relationship, out var found) && found is SlidePart slidePart
                    && slidePart.SlideLayoutPart?.SlideMasterPart is { } owner && masterIds.TryGetValue(owner, out var masterId)
                    && slidesById.TryGetValue(slideId, out var slide))
                    slide.MasterId = masterId;
            foreach (var reader in _parts) reader.Read(part, deck, index);

            new SlideBackgroundNormalizer().Normalize(deck);
            var diagnostics = damage.Select(part => part.Kind != PartDamageKind.Skipped
                ? Diagnostic.Warning("PPTX025", $"The part '{part.Part}' was damaged: what could be read was recovered.").With(part.Part)
                : Diagnostic.Warning("PPTX026", $"The part '{part.Part}' was unreadable and was skipped.").With(part.Part)).ToList();
            diagnostics.AddRange(readLosses);
            diagnostics.AddRange(fontLosses);
            return OperationResult<Presentation>.Ok(deck, diagnostics);
        }
        catch (Exception ex) when (_failures.IsMalformedInput(ex))
        {
            return OperationResult<Presentation>.Fail(Diagnostic.Error("PPTX101", $"Unreadable PPTX package: {ex.Message}").With(ex.Message));
        }
    }
}
