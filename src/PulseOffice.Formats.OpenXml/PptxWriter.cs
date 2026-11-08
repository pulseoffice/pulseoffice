using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class PptxWriter : IPresentationWriter
{
    private readonly OoxmlPendingImages _pendingImages = new();

    private readonly PptxThemeWriter _theme;
    private readonly PptxMasterWriter _master;
    private readonly SlideListWriter _slides;
    private readonly PptxCommentMapper _comments = new(new PulseOffice.Formats.Packaging.XmlCharacterFilter());
    private readonly ShowPropertiesWriter _show = new();
    private readonly PptxFidelityRules _fidelity;
    private readonly PptxPrintMasters _print = new();
    private readonly IReadOnlyList<IPptxPresentationPartWriter> _parts;

    public PptxWriter()
        : this(PptxFlavour.Presentation)
    {
    }

    private readonly PptxFlavour _flavour;

    public PptxWriter(PulseOffice.Drawing.Assets.IVectorRasterizer rasterizer)
        : this(PptxFlavour.Presentation, rasterizer)
    {
    }

    public PptxWriter(PptxFlavour flavour)
        : this(flavour, new PulseOffice.Drawing.Assets.NoVectorRasterizer())
    {
    }

    public PptxWriter(PptxFlavour flavour, PulseOffice.Drawing.Assets.IVectorRasterizer rasterizer)
    {
        ArgumentNullException.ThrowIfNull(flavour);
        ArgumentNullException.ThrowIfNull(rasterizer);
        _flavour = flavour;
        Format = flavour.Format;

        var targets = new SlideTargetResolver();
        _fidelity = new PptxFidelityRules(targets, new DrawingML.DrawingNumberSchemes());
        var units = new OoxmlWriteUnits(new SchemeRoles());
        var maps = new ColorMapCodec();
        var trees = new ShapeTreeFactory();
        var altText = new OoxmlAltTextMapper();
        var shapes = new PptxDeckShapeMapper(new VectorFallback(rasterizer));

        _theme = new PptxThemeWriter(units, maps);
        var bodies = new BodyPropertiesBuilder(new DrawingTextAnchors());
        _master = new PptxMasterWriter(units, trees, new SeedListStyleBuilder(units), new SlideLayoutRegistry(), bodies, maps, shapes);
        _slides = new SlideListWriter(_master, new PptxSlideWriter(
            units,
            trees,
            new TextShapeBuilder(units, trees, new ParagraphBuilder(units, new RunPropertiesBuilder(units), new DrawingFieldTypes()), bodies),
            new PptxInteractionMapper(),
            new PptxMediaMapper(),
            [
                new ChartWriter(new PptxDeckChartMapper(), altText),
                new TableWriter(new PptxDeckTableMapper(), altText),
                new PackageWriter(shapes, altText),
                new DrawingWriter(shapes, altText),
            ],
            new PptxTransitionMapper(),
            new PptxAnimationMapper(),
            _comments,
            new SlideGroupDecomposition()), units, targets);
        _parts =
        [
            new CustomShowsWriter(),
            new DefaultTextStyleWriter(new PptxTextDefaults()),
            new ExtensionsWriter([new SectionsExtension(), new HeaderFooterExtension()]),
        ];
    }

    public PresentationFormat Format { get; }

    public FidelityReport CheckFidelity(Presentation deck) => _fidelity.Check(deck);

    public OperationResult<bool> Write(Presentation deck, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            WritePackage(deck, stream);
            return OperationResult<bool>.Ok(true);
        }
        catch (Exception ex) when (ex is IOException or OpenXmlPackageException or InvalidOperationException or ArgumentException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("PPTX106", $"The PPTX file could not be written: {ex.Message}").With(ex.Message));
        }
    }

    private void WritePackage(Presentation deck, Stream stream)
    {
        using var document = PresentationDocument.Create(stream, _flavour.Type);
        _comments.Reset();

        var part = document.AddPresentationPart();
        part.Presentation = new P.Presentation();
        part.Presentation.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

        var session = new PptxWriteSession(document, part, deck);
        _master.Reset();
        var masters = new List<(SlideMaster Master, SlideMasterPart Part, uint Id)>();
        for (var index = 0; index < deck.Masters.Count; index++)
        {
            var model = deck.Masters[index];
            var id = _master.NextId();
            var masterPart = _master.Write(part, model, index == 0 ? deck.Theme : model.Theme);
            if (index == 0) masterPart.AddPart(_theme.Write(part, deck.Theme));
            else _theme.Write(masterPart, model.Theme);
            masters.Add((model, masterPart, id));
        }
        _slides.Write(session, masters);
        _print.Write(session, _theme);
        new PptxEmbeddedFonts().Write(part, deck.Fonts);
        foreach (var writer in _parts) writer.Write(session);

        new OoxmlCorePropertiesMapper().Write(document, deck.Properties);
        _show.Write(session);
        _pendingImages.Resolve(document);
    }
}
