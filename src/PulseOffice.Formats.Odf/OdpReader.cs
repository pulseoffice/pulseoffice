using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

public sealed class OdpReader : IPresentationReader
{
    private readonly OdpSounds _sounds = new(new SoundTypes());
    private readonly OdfPictureInliner _pictures = new([OdfPictureInliner.FillImage, OdfPictureInliner.ListImage]);

    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace TextNs = OdfPackage.Namespaces.Text;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;

    private readonly IThemeCatalog _themes;
    private readonly IMasterFactory _masters;
    private readonly OdfValues _values = new();
    private readonly OdpMissingObjects _missingObjects = new();
    private readonly OdpUnsupportedClickActions _unsupportedClickActions = new();
    private readonly OdpTableCellSpans _tableCellSpans = new();
    private readonly OdfLiteralEnhancedPath _literal = new(new PulseOffice.Drawing.Shapes.ShapePresets());
    private readonly OdpStyleSheetBuilder _styles;
    private readonly OdpMasterReader _master;
    private readonly OdpSlideReader _slides;
    private readonly OdpSettingsReader _settings = new();

    public OdpReader(IThemeCatalog themes, IMasterFactory masters)
    {
        _themes = themes;
        _masters = masters;

        var layouts = new SlideLayoutRegistry();
        var altText = new OdfAltTextMapper();
        var shapes = new OdfDeckShapeMapper();
        var freeText = new OdfFreeTextShape();
        var paragraphText = new OdfParagraphText();
        var paragraphs = new ParagraphReader(new ListPlacement(_values), new RunReader(new FieldKinds(new OdfFieldElements()), paragraphText), paragraphText);

        _styles = new OdpStyleSheetBuilder(_values, new SmilTransitionMapper(), new OdfPageFill(new OdfShapeStyleReader()), new OdfDateStyleCodec());
        _master = new OdpMasterReader(masters, layouts, _values, new OdfTextFrameReader(_values, new OdfTextAnchors()));
        _slides = new OdpSlideReader(
            layouts,
            new PlaceholderRoles(freeText),
            new TextFrameReader(new OdfInteractionMapper(), freeText, paragraphs),
            new PageStyleApplier(),
            [
                new MediaReader(new OdfMediaMapper()),
                new PathReader(shapes, altText),
                new FormControlReader(new OdfFormControlMapper()),
                new CustomShapeReader(shapes, altText, freeText),
                new PrimitiveShapeReader(shapes, altText),
                new SceneReader(shapes, altText),
                new ImageReader(shapes, altText),
                new OleReader(shapes, altText),
                new FormulaReader(shapes, altText),
                new ChartReader(new OdfDeckChartMapper(), altText),
                new TableReader(shapes, altText),
                new ObjectInteractionReader(new OdfInteractionMapper()),
                new OdpHiddenShapes(new OdpElementVisibility()),
                new GroupReader(),
                new AnimationReader(new OdfAnimationMapper()),
                new NotesReader(),
                new CommentReader(paragraphText),
            ]);
    }

    public PresentationFormat Format { get; } = new()
    {
        Id = "odp",
        DisplayName = "OpenDocument Presentation",
        Extensions = [".odp", ".fodp", ".otp", ".sxi", ".sti"],
        MimeType = OdfPackage.MimeTypes.Presentation,
        IsNative = true,
    };

    private readonly OdpShapeIds _shapeIds = new();

    public bool CanRead(Stream stream)
    {
        if (_flat.IsFlat(stream))
            return _flat.MimeTypeOf(stream) is OdfPackage.MimeTypes.Presentation or OdfPackage.MimeTypes.PresentationTemplate;

        var start = stream.Position;
        try
        {
            using var package = OdfPackage.OpenRead(stream);
            return package.ReadMimeType() is OdfPackage.MimeTypes.Presentation or OdfPackage.MimeTypes.PresentationTemplate
                or LegacyImpress or LegacyImpressTemplate;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return false;
        }
        finally
        {
            stream.Position = start;
        }
    }

    private const string LegacyImpress = "application/vnd.sun.xml.impress";
    private const string LegacyImpressTemplate = "application/vnd.sun.xml.impress.template";

    private readonly Formats.Packaging.Encryption.OdfPackageEncryption _encryption = new();
    private readonly Formats.Packaging.FlatOdfUnpacker _flat = new();
    private readonly Formats.Packaging.Legacy.LegacyOdfUpgrader _legacy = new();

    public OperationResult<Presentation> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (_legacy.IsLegacy(stream))
        {
            MemoryStream upgraded;
            try
            {
                upgraded = _legacy.Upgrade(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or System.Xml.XmlException)
            {
                return OperationResult<Presentation>.Fail(Diagnostic.Error("ODP100", ex.Message));
            }

            using (upgraded) return Read(upgraded);
        }

        if (_encryption.RequiresOpenPgp(stream))
            return OperationResult<Presentation>.Fail(Diagnostic.Error("ODF030", "The document is encrypted for OpenPGP recipients: it opens only with the secret key of a recipient."));
        if (_encryption.IsEncrypted(stream))
            return OperationResult<Presentation>.Fail(Diagnostic.Error("ODF004", "The document is protected by a password."));
        if (_flat.IsFlat(stream))
        {
            MemoryStream unpacked;
            try
            {
                unpacked = _flat.Unpack(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
            {
                return OperationResult<Presentation>.Fail(Diagnostic.Error("ODP100", ex.Message));
            }

            using (unpacked) return Read(unpacked);
        }

        try
        {
            using var package = OdfPackage.OpenRead(stream);

            if (package.ReadXml("content.xml")?.Root is not { } content)
                return OperationResult<Presentation>.Fail(Diagnostic.Error("ODP100", "content.xml is missing or empty."));

            var styles = package.ReadXml("styles.xml");
            _sounds.Unpack(content.Document);
            _sounds.Unpack(styles);
            _pictures.Inline(content.Document, package.ReadBytes);
            _pictures.Inline(styles, package.ReadBytes);
            var theme = new OdfMasterPages().PrimaryTheme(styles, _themes);
            var deck = new Presentation
            {
                Theme = theme,
                Master = _master.Read(styles, theme, _master.Size(styles)),
                Size = _master.Size(styles),
            };

            new OdfMetaMapper().Read(package.ReadXml("meta.xml"), deck.Properties);
            (deck.DefaultLanguages, deck.DefaultScriptFonts) = new OdpTextDefaults().Read(styles);
            deck.Fonts = new OdfFontFaceReader().Read(package, content.Document, styles);
            new OdfVersionMapper().Read(package, deck.Versions);
            var macroMapper = new OdfMacroMapper();
            deck.MacroSignature = macroMapper.Read(package, deck.Macros);
            var basicLoss = macroMapper.BasicLoss(package);

            var context = new OdpReadContext(_styles.Build(content, styles?.Root), _values, package.ReadBytes, package.ReadXml);
            var body = content.Element(Office + "body")?.Element(Office + "presentation");

            var ids = new OdpSlideIds(deck.Slides.Select(slide => slide.Id));
            foreach (var page in body?.Elements(Draw + "page") ?? [])
                deck.AddExisting(_slides.Read(_shapeIds.Assign(new OdpGroupFlattener().Flatten(page)), context, ids));

            _settings.Read(body?.Element(Presentation + "settings"), deck);
            if (!deck.HeaderFooter.Any && body is not null
                && new OdfHeaderFooter().Read(body, PageStyles(content), DataStyles(content, styles)) is { } headerFooter)
                deck.HeaderFooter = headerFooter;
            new OdfMasterPages().Read(styles, body, deck, _themes, (masterTheme, masterName) => _master.Read(styles, masterTheme, deck.Size, masterName), package.ReadBytes);
            new OdfGraphicStyles().Read(styles, deck);
            _sounds.Read(deck, package.ReadBytes);

            if (body?.Elements(Draw + "page").Select(page => page.Element(Office + "forms")).OfType<XElement>().FirstOrDefault() is { } forms
                && new OdfFormControlMapper().ReadSettings(forms) is { } formSettings)
                deck.FormSettings = formSettings;

            if (deck.Slides.Count == 0)
                deck.AddExisting(new SlideFactory().Create("slide1", _masters.CreateStandard(theme, deck.Size), SlideLayout.TitleSlide));

            new SlideBackgroundNormalizer().Normalize(deck);
            var losses = new List<Diagnostic>();
            if (body is not null && new OdfHeaderFooter().DateFormatApproximated(body, DataStyles(content, styles)))
                losses.Add(Diagnostic.Info("ODP215", "The date format of the header and footer is not one of the thirteen formats Deck offers: the nearest one is used."));
            if (_missingObjects.Present(body, package.OpenEntry)) losses.Add(_missingObjects.Loss);
            if (_unsupportedClickActions.Present(body)) losses.Add(_unsupportedClickActions.Loss);
            if (_tableCellSpans.Present(body)) losses.Add(_tableCellSpans.Loss);
            if (body?.Descendants(Draw + "custom-shape").Any(_literal.Approximated) == true)
                losses.Add(Diagnostic.Warning("ODP183", "Shapes whose geometry formulas cannot be evaluated, because their equations or path are malformed, are imported as rectangles."));
            if (content.Descendants(TextNs + "list-level-style-image").Concat(styles?.Descendants(TextNs + "list-level-style-image") ?? []).Any(image => image.Element(Office + "binary-data") is null))
                losses.Add(Diagnostic.Info("ODP103", "Picture bullets whose picture is linked or missing from the package are imported as character bullets."));
            if (basicLoss is not null) losses.Add(basicLoss);
            return OperationResult<Presentation>.Ok(deck, losses);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Xml.XmlException
            or FormatException or ArgumentException or InvalidOperationException or ArithmeticException)
        {
            return OperationResult<Presentation>.Fail(Diagnostic.Error("ODP101", $"Unreadable ODP package: {ex.Message}").With(ex.Message));
        }
    }

    private Dictionary<string, XElement> DataStyles(XElement content, XDocument? styles) =>
        new[]
        {
            content.Element(Office + "automatic-styles"),
            styles?.Root?.Element(Office + "styles"),
            styles?.Root?.Element(Office + "automatic-styles"),
        }
        .OfType<XElement>()
        .SelectMany(container => container.Elements())
        .Where(style => style.Name == OdfPackage.Namespaces.Number + "date-style" || style.Name == OdfPackage.Namespaces.Number + "time-style")
        .Where(style => style.Attribute(OdfPackage.Namespaces.Style + "name") is not null)
        .GroupBy(style => (string)style.Attribute(OdfPackage.Namespaces.Style + "name")!)
        .ToDictionary(group => group.Key, group => group.First());

    private Dictionary<string, XElement> PageStyles(XElement content) =>
        content.Element(Office + "automatic-styles")?.Elements(OdfPackage.Namespaces.Style + "style")
            .Where(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "family") == "drawing-page")
            .Where(style => style.Attribute(OdfPackage.Namespaces.Style + "name") is not null)
            .GroupBy(style => (string)style.Attribute(OdfPackage.Namespaces.Style + "name")!)
            .ToDictionary(group => group.Key, group => group.First()) ?? [];
}
