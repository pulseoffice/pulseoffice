using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdpWriter : IPresentationWriter
{
    private readonly OdpContentBuilder _content;
    private readonly OdpStylesBuilder _styles;
    private readonly OdpPackageWriter _package = new();
    private readonly OdpFidelityRules _fidelity = new();
    private readonly VectorFallback _vectors;
    private readonly PulseOffice.Text.Rich.ITextCultures _cultures;

    public OdpWriter(bool template = false)
        : this(template, new PulseOffice.Drawing.Assets.NoVectorRasterizer())
    {
    }

    public OdpWriter(bool template, PulseOffice.Drawing.Assets.IVectorRasterizer rasterizer)
    {
        ArgumentNullException.ThrowIfNull(rasterizer);
        _cultures = new PulseOffice.Text.Rich.TextCultures(System.Globalization.CultureInfo.InvariantCulture);
        _vectors = new VectorFallback(rasterizer);
        Format = template
            ? new()
            {
                Id = "otp",
                DisplayName = "OpenDocument Presentation Template",
                Extensions = [".otp"],
                MimeType = OdfPackage.MimeTypes.PresentationTemplate,
                IsNative = false,
            }
            : new()
            {
                Id = "odp",
                DisplayName = "OpenDocument Presentation",
                Extensions = [".odp"],
                MimeType = OdfPackage.MimeTypes.Presentation,
                IsNative = true,
            };

        var units = new OdfUnits();
        var layouts = new SlideLayoutRegistry();
        var lists = new ListStyleBuilder();
        var frames = new OdfTextFrameWriter(units, new OdfTextAnchors());
        var paragraphText = new OdfParagraphText();
        var richText = new OdfRichTextWriter(new ParagraphStyleBuilder(units, frames), new SpanStyleBuilder(units), lists, new OdfFieldElements(), paragraphText, new OdfDateStyleCodec(), new PulseOffice.Text.Rich.BulletPictureSizing(), new PulseOffice.Text.Rich.ListNumberFormats());
        var namespaces = new OdpNamespaces();
        var altText = new OdfAltTextMapper();
        var shapes = new OdfDeckShapeMapper();

        var listeners = new InteractionListenerWriter(new OdfInteractionMapper());
        _content = new OdpContentBuilder(
            new OdpPageWriter(
                layouts,
                new PageStyleBuilder(units, new SmilTransitionMapper()),
                new TextFrameWriter(units, richText, listeners, frames, new OdfFreeTextShape()),
                new MediaFrameWriter(units, new OdfMediaMapper()),
                new ObjectWriterChain(
                [
                    new ChartObjectWriter(new OdfDeckChartMapper(), altText),
                    new FormulaObjectWriter(shapes, new PulseOffice.Formulas.FormulaParser(), new PulseOffice.Formulas.MathMlSyntax(), altText),
                    new TableObjectWriter(shapes, altText),
                    new ShapeObjectWriter(shapes, altText),
                    new SceneObjectWriter(shapes, altText),
                    new FontworkObjectWriter(shapes, altText),
                    new FreeformObjectWriter(shapes, altText),
                    new ConnectorObjectWriter(shapes, altText),
                    new ImageObjectWriter(shapes, units, altText),
                    new VectorObjectWriter(new OdpVectorFrame(shapes), altText),
                    new PackagedObjectWriter(shapes, units, altText),
                ]),
                new OdfAnimationMapper(),
                new FormControlsWriter(units, new OdfFormControlMapper()),
                new CommentsWriter(units, paragraphText),
                listeners,
                new SlideGroupDecomposition()),
            new OdpSettingsWriter(),
            lists,
            namespaces);
        _styles = new OdpStylesBuilder(units, layouts, new SeedStyleBuilder(units, frames), namespaces);
    }

    public PresentationFormat Format { get; }

    public FidelityReport CheckFidelity(Presentation deck) => _fidelity.Check(deck);

    public OperationResult<bool> Write(Presentation deck, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(stream);

        using var package = OdfPackage.Create(stream, Format.MimeType);
        var session = new OdpWriteSession(deck, _vectors, _cultures.For(deck.TextLanguage));
        _package.Write(package, session, _content.Build(session), _styles.Build(deck, session), Format.MimeType);
        return OperationResult<bool>.Ok(true);
    }
}

public sealed class FlatOdpWriter(OdpWriter package) : IPresentationWriter
{
    private readonly Formats.Packaging.FlatOdfPacker _packer = new();

    public FlatOdpWriter() : this(new OdpWriter())
    {
    }

    public PresentationFormat Format { get; } = new()
    {
        Id = "fodp",
        DisplayName = "Flat OpenDocument Presentation",
        Extensions = [".fodp"],
        MimeType = "application/vnd.oasis.opendocument.presentation-flat-xml",
        IsNative = false,
    };

    public FidelityReport CheckFidelity(Presentation deck) => package.CheckFidelity(deck);

    public OperationResult<bool> Write(Presentation deck, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var buffer = new MemoryStream();
        var result = package.Write(deck, buffer);
        if (!result.Succeeded) return result;

        buffer.Position = 0;
        try
        {
            using var flat = _packer.Pack(buffer);
            flat.CopyTo(stream);
            return result;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or IOException)
        {
            return OperationResult<bool>.Fail(Diagnostic.Error("ODP190", $"Writing the flat ODP failed: {ex.Message}").With(ex.Message));
        }
    }
}
